using System.IO;
using AGLauncher.Models;
using System.Text.Json;

namespace AGLauncher.Services;

public sealed class LauncherNotification
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Date { get; set; } = "";
    public string LinkUrl { get; set; } = "";
}

public sealed class NotificationService
{
    private readonly string _statePath;

    public NotificationService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Atenoct Games", "AGLauncher");
        Directory.CreateDirectory(dir);
        _statePath = Path.Combine(dir, "notification-state.json");
    }

    public async Task<List<LauncherNotification>> CollectNewAsync(LauncherManifest manifest, ManifestService manifestService)
    {
        var candidates = new List<LauncherNotification>();
        if (!string.IsNullOrWhiteSpace(manifest.Launcher.LatestVersion))
            candidates.Add(new LauncherNotification { Key = $"launcher:{manifest.Launcher.LatestVersion}", Title = $"AG Launcher v{manifest.Launcher.LatestVersion}", Message = manifest.Launcher.ReleaseNotes, Date = DateTime.Now.ToString("yyyy-MM-dd") });

        foreach (var item in manifest.News.Where(x => x.Visible))
            candidates.Add(new LauncherNotification { Key = $"news:{item.Id}:{item.Date}", Title = item.Title, Message = item.Summary, Date = item.Date, LinkUrl = item.LinkUrl });

        foreach (var game in manifest.Games.Where(x => x.Visible))
        {
            candidates.Add(new LauncherNotification { Key = $"game:{game.Id}", Title = $"Game available: {game.Name}", Message = game.Description, Date = DateTime.Now.ToString("yyyy-MM-dd"), LinkUrl = game.WebsiteUrl });
            if (string.IsNullOrWhiteSpace(game.ManifestUrl)) continue;
            try
            {
                var gm = await manifestService.LoadGameAsync(game.ManifestUrl);
                candidates.Add(new LauncherNotification { Key = $"game-version:{game.Id}:{gm.Version}", Title = $"{game.Name} v{gm.Version}", Message = "A new game version is available in AG Launcher.", Date = DateTime.Now.ToString("yyyy-MM-dd"), LinkUrl = game.WebsiteUrl });
            }
            catch { }
        }

        var known = LoadKnown();
        var fresh = candidates.Where(x => !known.Contains(x.Key)).OrderByDescending(x => x.Date).Take(12).ToList();
        SaveKnown(candidates.Select(x => x.Key));
        return fresh;
    }

    private HashSet<string> LoadKnown()
    {
        try
        {
            if (!File.Exists(_statePath)) return new(StringComparer.OrdinalIgnoreCase);
            var keys = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_statePath), JsonUtil.Options) ?? new();
            return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        }
        catch { return new(StringComparer.OrdinalIgnoreCase); }
    }

    private void SaveKnown(IEnumerable<string> keys)
    {
        try { File.WriteAllText(_statePath, JsonSerializer.Serialize(keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), JsonUtil.Options)); }
        catch { }
    }
}
