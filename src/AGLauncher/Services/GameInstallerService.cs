using System.IO;
using System.Net.Http;
using AGLauncher.Models;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace AGLauncher.Services;

public sealed class GameInstallerService
{
    private readonly HttpClient _http = new();

    public GameInstallerService() =>
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AGLauncher/0.4.0");

    public string GetInstallDir(GameCatalogItem game, GameManifest manifest)
    {
        var folder = string.IsNullOrWhiteSpace(manifest.InstallFolder) ? game.Id : manifest.InstallFolder;
        if (folder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || folder is "." or "..") throw new InvalidDataException("Invalid installation folder.");
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Atenoct Games", "Games", folder);
    }

    public GameState? ReadState(string dir)
    {
        try
        {
            var p = Path.Combine(dir, ".aglauncher-state.json");
            return File.Exists(p)
                ? JsonSerializer.Deserialize<GameState>(File.ReadAllText(p), JsonUtil.Options)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task InstallOrUpdateAsync(
        GameCatalogItem game,
        GameManifest manifest,
        IProgress<(double, string)> progress)
    {
        var dir = GetInstallDir(game, manifest);
        if (manifest.Packages.Count == 0) throw new InvalidDataException("No downloadable game packages are available.");
        var staged = dir + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staged);

        var temp = Path.Combine(
            Path.GetTempPath(),
            "AGLauncherGame",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var count = Math.Max(1, manifest.Packages.Count);

            for (var i = 0; i < manifest.Packages.Count; i++)
            {
                var package = manifest.Packages[i];
                var zip = Path.Combine(temp, $"package-{i}.zip");
                if (!Uri.TryCreate(package.Url, UriKind.Absolute, out var downloadUri) || downloadUri.Scheme != "https") throw new InvalidDataException("Game downloads must use HTTPS.");

                using (var response = await _http.GetAsync(
                    package.Url,
                    HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength ?? 1;

                    await using (var src = await response.Content.ReadAsStreamAsync())
                    await using (var dst = File.Create(zip))
                    {
                        var buffer = new byte[131072];
                        long read = 0;
                        int n;

                        while ((n = await src.ReadAsync(buffer)) > 0)
                        {
                            await dst.WriteAsync(buffer.AsMemory(0, n));
                            read += n;
                            progress.Report((
                                (i + (double)read / total) / count,
                                $"Downloading {package.Name}..."));
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(package.Sha256))
                {
                    string actual;
                    await using (var fs = File.OpenRead(zip))
                    {
                        actual = Convert
                            .ToHexString(await SHA256.HashDataAsync(fs))
                            .ToLowerInvariant();
                    }

                    if (!actual.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"SHA-256 mismatch: {package.Name}");
                }

                progress.Report(((i + 0.95) / count, $"Extracting {package.Name}..."));
                await Task.Run(() => ZipFile.ExtractToDirectory(zip, staged, true));

                try { File.Delete(zip); } catch { }
            }

            var executable = ResolveExecutable(staged, manifest.Executable);
            if (!File.Exists(executable)) throw new FileNotFoundException("The ZIP does not contain the configured game executable. The installed version was preserved.");
            // Keep user-created files while replacing the complete new build atomically.
            if (Directory.Exists(dir))
                foreach (var previous in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var destination = Path.Combine(staged, Path.GetRelativePath(dir, previous));
                    if (!File.Exists(destination)) { Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(previous, destination); }
                }
            File.WriteAllText(
                Path.Combine(staged, ".aglauncher-state.json"),
                JsonSerializer.Serialize(
                    new GameState { Id = game.Id, Version = manifest.Version },
                    JsonUtil.Options));

            var backup = dir + ".backup-" + Guid.NewGuid().ToString("N");
            var existed = Directory.Exists(dir);
            if (existed) Directory.Move(dir, backup);
            try { Directory.Move(staged, dir); }
            catch { if (existed) Directory.Move(backup, dir); throw; }
            try { if (existed) Directory.Delete(backup, true); } catch { }
            progress.Report((1, "Ready"));
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
            try { if (Directory.Exists(staged)) Directory.Delete(staged, true); } catch { }
        }
    }

    private static string ResolveExecutable(string dir, string executable)
    {
        var root = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(dir, executable.Replace('/', Path.DirectorySeparatorChar)));
        if (string.IsNullOrWhiteSpace(executable) || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid game executable path.");
        if (File.Exists(path)) return path;
        // Support releases wrapped in a single top-level folder.
        var matches = Directory.EnumerateFiles(dir, Path.GetFileName(executable), SearchOption.AllDirectories).ToList();
        return matches.Count == 1 ? matches[0] : path;
    }

    public void Play(GameCatalogItem game, GameManifest manifest)
    {
        var dir = GetInstallDir(game, manifest);
        var exe = ResolveExecutable(dir, manifest.Executable);

        if (!File.Exists(exe))
            throw new FileNotFoundException("Game executable not found.", exe);

        Process.Start(new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe)!
        });
    }
}
