using System.IO;
using System.Net.Http;
using AGLauncher.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AGLauncher.Services;

public sealed class GitHubAdminService
{
    private HttpClient Client(string token)
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("AGLauncher-Admin/0.3");
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        c.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return c;
    }

    public async Task<(bool Ok, string Message)> VerifyAsync(BootstrapConfig cfg, string token)
    {
        try
        {
            using var c = Client(token);
            using var u = await c.GetAsync("https://api.github.com/user");
            if (!u.IsSuccessStatusCode) return (false, "GitHub token is invalid.");
            using var uj = JsonDocument.Parse(await u.Content.ReadAsStringAsync());
            var login = uj.RootElement.GetProperty("login").GetString() ?? "";
            if (!login.Equals(cfg.AdminLogin, StringComparison.OrdinalIgnoreCase)) return (false, $"This GitHub account is not launcher admin ({login}).");
            using var r = await c.GetAsync($"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}");
            if (!r.IsSuccessStatusCode) return (false, "Launcher repository is not accessible.");
            using var rj = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            var canPush = rj.RootElement.TryGetProperty("permissions", out var p) && p.TryGetProperty("push", out var push) && push.GetBoolean();
            return canPush ? (true, $"Admin verified: {login}") : (false, "No write permission.");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task SaveManifestAsync(BootstrapConfig cfg, string token, LauncherManifest manifest)
    {
        using var c = Client(token);
        var json = JsonSerializer.Serialize(manifest, JsonUtil.Options);
        var message = $"AG Launcher admin update {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";

        await SaveJsonFileAsync(c, cfg, cfg.ManifestPath, json, message);
        // Keep the static GitHub Pages fallback catalog synchronized with the same content.
        await SaveJsonFileAsync(c, cfg, "docs/catalog.json", json, message + " (website fallback)");
    }

    private static async Task SaveJsonFileAsync(HttpClient c, BootstrapConfig cfg, string path, string json, string message)
    {
        var getUrl = $"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}/contents/{path}?ref={Uri.EscapeDataString(cfg.Branch)}";
        using var get = await c.GetAsync(getUrl);
        if (!get.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub read failed for {path}: {(int)get.StatusCode} {await get.Content.ReadAsStringAsync()}");

        using var current = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        var sha = current.RootElement.GetProperty("sha").GetString()!;
        var payload = JsonSerializer.Serialize(new
        {
            message,
            content = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
            sha,
            branch = cfg.Branch
        });

        using var put = await c.PutAsync(
            $"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}/contents/{path}",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        if (!put.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub save failed for {path}: {(int)put.StatusCode} {await put.Content.ReadAsStringAsync()}");
    }

    public async Task<string> UploadImageAsync(BootstrapConfig cfg, string token, byte[] bytes, string originalName, string folder)
    {
        using var c = Client(token);
        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        if (ext is not ".png" and not ".jpg" and not ".jpeg" and not ".webp") throw new InvalidDataException("Only PNG, JPG, JPEG and WEBP images are supported.");
        var stem = Path.GetFileNameWithoutExtension(originalName);
        stem = Regex.Replace(stem, "[^a-zA-Z0-9_-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(stem)) stem = "image";
        var safeFolder = Regex.Replace(folder, "[^a-zA-Z0-9_-]+", "-");
        var path = $"docs/media/{safeFolder}/{DateTime.UtcNow:yyyyMMdd-HHmmss}-{stem}{ext}";
        var payload = JsonSerializer.Serialize(new
        {
            message = $"Upload {safeFolder} image for AG Launcher",
            content = Convert.ToBase64String(bytes),
            branch = cfg.Branch
        });
        using var put = await c.PutAsync($"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}/contents/{path}", new StringContent(payload, Encoding.UTF8, "application/json"));
        if (!put.IsSuccessStatusCode) throw new HttpRequestException($"Image upload failed: {(int)put.StatusCode} {await put.Content.ReadAsStringAsync()}");
        return $"https://raw.githubusercontent.com/{cfg.Owner}/{cfg.Repo}/{cfg.Branch}/{path}";
    }
    public async Task<string> UploadMediaAsync(BootstrapConfig cfg, string token, byte[] bytes, string originalName, string folder)
    {
        using var c = Client(token);
        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        if (ext is not ".gif" and not ".mp4")
            throw new InvalidDataException("Only GIF and MP4 animated banners are supported.");

        var stem = Path.GetFileNameWithoutExtension(originalName);
        stem = Regex.Replace(stem, "[^a-zA-Z0-9_-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(stem)) stem = "animated-banner";

        var safeFolder = Regex.Replace(folder, "[^a-zA-Z0-9_-]+", "-");
        var path = $"docs/media/{safeFolder}/{DateTime.UtcNow:yyyyMMdd-HHmmss}-{stem}{ext}";
        var payload = JsonSerializer.Serialize(new
        {
            message = $"Upload {safeFolder} media for AG Launcher",
            content = Convert.ToBase64String(bytes),
            branch = cfg.Branch
        });

        using var put = await c.PutAsync(
            $"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}/contents/{path}",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        if (!put.IsSuccessStatusCode)
            throw new HttpRequestException($"Media upload failed: {(int)put.StatusCode} {await put.Content.ReadAsStringAsync()}");

        return $"https://raw.githubusercontent.com/{cfg.Owner}/{cfg.Repo}/{cfg.Branch}/{path}";
    }

    public async Task<string> UploadWorkshopFileAsync(BootstrapConfig cfg, string token, byte[] bytes, string originalName)
    {
        using var c = Client(token);
        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        if (ext != ".zip") throw new InvalidDataException("Workshop packages must be ZIP files.");

        var stem = Path.GetFileNameWithoutExtension(originalName);
        stem = Regex.Replace(stem, "[^a-zA-Z0-9_-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(stem)) stem = "workshop-item";

        var path = $"docs/workshop/files/{DateTime.UtcNow:yyyyMMdd-HHmmss}-{stem}.zip";
        var payload = JsonSerializer.Serialize(new
        {
            message = "Upload Workshop ZIP for AG Launcher",
            content = Convert.ToBase64String(bytes),
            branch = cfg.Branch
        });

        using var put = await c.PutAsync(
            $"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}/contents/{path}",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        if (!put.IsSuccessStatusCode)
            throw new HttpRequestException($"Workshop ZIP upload failed: {(int)put.StatusCode} {await put.Content.ReadAsStringAsync()}");

        return $"https://raw.githubusercontent.com/{cfg.Owner}/{cfg.Repo}/{cfg.Branch}/{path}";
    }

}
