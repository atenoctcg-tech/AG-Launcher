using System.IO;
using System.Net.Http;
using AGLauncher.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AGLauncher.Services;

public sealed class ManifestService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public ManifestService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AGLauncher/0.4.2");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<(LauncherManifest Manifest, bool Online)> LoadLauncherAsync(BootstrapConfig cfg)
    {
        try
        {
            var json = await _http.GetStringAsync(CacheBust(cfg.ManifestUrl));
            var m = JsonSerializer.Deserialize<LauncherManifest>(json, JsonUtil.Options);
            if (m != null) return (m, true);
        }
        catch { }

        var path = Path.Combine(AppContext.BaseDirectory, "fallback-manifest.json");
        return (
            JsonSerializer.Deserialize<LauncherManifest>(File.ReadAllText(path), JsonUtil.Options) ?? new LauncherManifest(),
            false);
    }

    public async Task<GameManifest> LoadGameAsync(GameCatalogItem game)
    {
        if (!string.IsNullOrWhiteSpace(game.ManifestUrl)) return await LoadGameAsync(game.ManifestUrl);
        var manifest = new GameManifest { Id = game.Id, ReleaseRepo = game.ReleaseRepo, ReleaseAssetPattern = game.ReleaseAssetPattern, Executable = game.Executable, InstallFolder = string.IsNullOrWhiteSpace(game.InstallFolder) ? game.Id : game.InstallFolder };
        await ApplyLatestGitHubReleaseAsync(manifest);
        return manifest;
    }

    public async Task<GameManifest> LoadGameAsync(string url)
    {
        var json = await _http.GetStringAsync(CacheBust(url));
        var manifest = JsonSerializer.Deserialize<GameManifest>(json, JsonUtil.Options)
            ?? throw new InvalidOperationException("Game manifest is invalid.");

        if (!string.IsNullOrWhiteSpace(manifest.ReleaseRepo))
        {
            try
            {
                await ApplyLatestGitHubReleaseAsync(manifest);
            }
            catch
            {
                // Keep the static version/package in the manifest as a fallback.
                // This allows installs even when GitHub API is temporarily unavailable.
            }
        }

        return manifest;
    }

    private async Task ApplyLatestGitHubReleaseAsync(GameManifest manifest)
    {
        var repo = manifest.ReleaseRepo.Trim().Trim('/');
        if (repo.Split('/').Length != 2)
            throw new InvalidDataException("releaseRepo must be in owner/repository format.");

        var json = await _http.GetStringAsync($"https://api.github.com/repos/{repo}/releases/latest");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagNode)
            ? tagNode.GetString() ?? ""
            : "";
        var version = NormalizeVersion(tag);
        if (string.IsNullOrWhiteSpace(version))
            throw new InvalidDataException("Latest release has no valid tag.");

        var patternText = string.IsNullOrWhiteSpace(manifest.ReleaseAssetPattern)
            ? @".*\.zip$"
            : manifest.ReleaseAssetPattern;
        var pattern = new Regex(patternText, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        JsonElement? selected = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (pattern.IsMatch(name) && !name.Contains("source", StringComparison.OrdinalIgnoreCase))
                {
                    selected = asset;
                    break;
                }
            }


        }

        if (selected == null)
            throw new InvalidDataException("No matching ZIP asset found in the latest release.");

        var assetValue = selected.Value;
        var assetName = assetValue.GetProperty("name").GetString() ?? "game.zip";
        var assetUrl = assetValue.GetProperty("browser_download_url").GetString() ?? "";
        var digest = assetValue.TryGetProperty("digest", out var digestNode)
            ? digestNode.GetString() ?? ""
            : "";
        var sha = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? digest["sha256:".Length..]
            : "";

        if (string.IsNullOrWhiteSpace(assetUrl))
            throw new InvalidDataException("Latest release asset has no download URL.");

        manifest.Version = version;
        manifest.Packages = new List<GamePackage>
        {
            new()
            {
                Name = assetName,
                Url = assetUrl,
                Sha256 = sha
            }
        };
    }

    private static string CacheBust(string url)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return url + separator + "_ag=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static string NormalizeVersion(string tag)
    {
        var value = (tag ?? "").Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value[1..];
        return value;
    }
}
