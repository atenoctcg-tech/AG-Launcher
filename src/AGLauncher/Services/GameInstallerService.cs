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
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AGLauncher/0.3.0");

    public string GetInstallDir(GameCatalogItem game, GameManifest manifest)
    {
        var folder = string.IsNullOrWhiteSpace(manifest.InstallFolder) ? game.Id : manifest.InstallFolder;
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
        Directory.CreateDirectory(dir);

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
                var zip = Path.Combine(temp, package.Name);

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
                ZipFile.ExtractToDirectory(zip, dir, true);

                try { File.Delete(zip); } catch { }
            }

            File.WriteAllText(
                Path.Combine(dir, ".aglauncher-state.json"),
                JsonSerializer.Serialize(
                    new GameState { Id = game.Id, Version = manifest.Version },
                    JsonUtil.Options));

            progress.Report((1, "Ready"));
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    public void Play(GameCatalogItem game, GameManifest manifest)
    {
        var dir = GetInstallDir(game, manifest);
        var exe = Path.Combine(
            dir,
            manifest.Executable.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(exe))
            throw new FileNotFoundException("Game executable not found.", exe);

        Process.Start(new ProcessStartInfo(exe)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe)!
        });
    }
}
