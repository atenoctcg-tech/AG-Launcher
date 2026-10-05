using System.IO;
using System.IO.Compression;
using System.Net.Http;
using AGLauncher.Models;

namespace AGLauncher.Services;

public sealed class WorkshopDownloadResult
{
    public string Path { get; set; } = "";
    public bool Imported { get; set; }
}

public sealed class WorkshopService
{
    private readonly HttpClient _http = new();

    public WorkshopService() =>
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AGLauncher/1.0");

    public async Task<WorkshopDownloadResult> DownloadAsync(
        WorkshopItem item,
        GameCatalogItem? game,
        GameManifest? manifest,
        GameInstallerService installer,
        IProgress<(double Value, string Status)> progress)
    {
        if (!Uri.TryCreate(item.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new InvalidDataException("Workshop downloads must use HTTPS.");

        var fileName = SafeFileName(
            string.IsNullOrWhiteSpace(item.FileName)
                ? $"{item.Name}.zip"
                : item.FileName);

        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            fileName += ".zip";

        var tempRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Atenoct Games", "AGLauncher", "Workshop", "Temp");
        Directory.CreateDirectory(tempRoot);
        var tempFile = Path.Combine(tempRoot, $"{Guid.NewGuid():N}-{fileName}");

        try
        {
            using var response = await _http.GetAsync(item.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 1L;

            await using (var src = await response.Content.ReadAsStreamAsync())
            await using (var dst = File.Create(tempFile))
            {
                var buffer = new byte[131072];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n));
                    read += n;
                    progress.Report((Math.Clamp((double)read / total, 0, 1), $"Downloading {item.Name}..."));
                }
            }

            if (item.Category.Equals("Mod", StringComparison.OrdinalIgnoreCase)
                && item.AutoImport
                && game != null
                && manifest != null
                && !string.IsNullOrWhiteSpace(item.ImportPath))
            {
                var gameDir = installer.GetInstallDir(game, manifest);
                if (installer.ReadState(gameDir) != null)
                {
                    var importDir = ResolveSafeImportPath(gameDir, item.ImportPath);
                    Directory.CreateDirectory(importDir);
                    progress.Report((0.96, "Importing mod files..."));
                    SafeExtractZip(tempFile, importDir);
                    progress.Report((1, "Mod imported."));
                    return new WorkshopDownloadResult { Path = importDir, Imported = true };
                }
            }

            var downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "AG Launcher Workshop");
            Directory.CreateDirectory(downloads);
            var destination = UniquePath(downloads, fileName);
            File.Copy(tempFile, destination, false);
            progress.Report((1, "Workshop download complete."));
            return new WorkshopDownloadResult { Path = destination, Imported = false };
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
        }
    }

    private static string ResolveSafeImportPath(string gameDir, string importPath)
    {
        importPath = (importPath ?? "").Trim().Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(importPath) || string.IsNullOrWhiteSpace(importPath))
            throw new InvalidDataException("Mod Import Path must be a relative folder inside the installed game.");

        var root = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(gameDir, importPath)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Mod Import Path must stay inside the installed game folder.");

        return target.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static void SafeExtractZip(string zipPath, string destination)
    {
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(zipPath);

        foreach (var entry in zip.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Workshop ZIP contains an unsafe path.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
    }

    private static string SafeFileName(string value)
    {
        var name = string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
        return string.IsNullOrWhiteSpace(name) ? "workshop-item.zip" : name;
    }

    private static string UniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path)) return path;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 2; i < 1000; i++)
        {
            path = Path.Combine(folder, $"{stem} ({i}){ext}");
            if (!File.Exists(path)) return path;
        }
        return Path.Combine(folder, $"{stem}-{Guid.NewGuid():N}{ext}");
    }
}
