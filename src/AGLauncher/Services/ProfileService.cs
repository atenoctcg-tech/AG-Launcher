using System.IO;
using System.Text.Json;

namespace AGLauncher.Services;

public sealed class LocalProfile
{
    public string Nickname { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProfileService
{
    private readonly string _root;
    private readonly string _path;
    private readonly string _legacyPath;

    public ProfileService()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Atenoct Games", "AGLauncher");
        Directory.CreateDirectory(_root);
        _path = Path.Combine(_root, "profile.json");
        _legacyPath = Path.Combine(_root, "profiles.json");
        MigrateLegacyProfile();
    }

    public LocalProfile Load()
    {
        try
        {
            if (!File.Exists(_path)) return new LocalProfile();
            return JsonSerializer.Deserialize<LocalProfile>(File.ReadAllText(_path), JsonUtil.Options) ?? new LocalProfile();
        }
        catch
        {
            return new LocalProfile();
        }
    }

    public (bool Ok, string Message) SaveNickname(string nickname)
    {
        nickname = (nickname ?? "").Trim();
        if (nickname.Length > 24) return (false, "Nickname must be 24 characters or fewer.");
        if (nickname.Length > 0 && nickname.Length < 2) return (false, "Nickname must contain at least 2 characters.");

        File.WriteAllText(_path, JsonSerializer.Serialize(new LocalProfile
        {
            Nickname = nickname,
            UpdatedAtUtc = DateTime.UtcNow
        }, JsonUtil.Options));

        return (true, string.IsNullOrWhiteSpace(nickname) ? "Nickname cleared." : "Nickname saved.");
    }

    private void MigrateLegacyProfile()
    {
        if (File.Exists(_path) || !File.Exists(_legacyPath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(_legacyPath));
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                if (first.TryGetProperty("username", out var username))
                {
                    var nickname = username.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(nickname)) SaveNickname(nickname);
                }
            }
            File.Delete(_legacyPath);
        }
        catch { }
    }
}
