using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace AGLauncher.Services;

public class UserProfile
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

internal sealed class StoredUserProfile : UserProfile
{
    public string PasswordSalt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public sealed class ProfileService
{
    private readonly string _path;

    public ProfileService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Atenoct Games", "AGLauncher");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "profiles.json");
    }

    public (bool Ok, string Message, UserProfile? Profile) Register(string username, string email, string password)
    {
        username = username.Trim();
        email = email.Trim();
        if (username.Length < 3) return (false, "Username must contain at least 3 characters.", null);
        if (!email.Contains('@') || email.Length < 5) return (false, "Enter a valid email address.", null);
        if (password.Length < 8) return (false, "Password must contain at least 8 characters.", null);

        var users = Load();
        if (users.Any(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase))) return (false, "This username already exists on this PC.", null);
        if (users.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase))) return (false, "This email is already registered on this PC.", null);

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, 32);
        var stored = new StoredUserProfile
        {
            Username = username,
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(hash)
        };
        users.Add(stored);
        Save(users);
        return (true, "Profile created.", ToPublic(stored));
    }

    public (bool Ok, string Message, UserProfile? Profile) Login(string usernameOrEmail, string password)
    {
        var users = Load();
        var value = usernameOrEmail.Trim();
        var user = users.FirstOrDefault(x => x.Username.Equals(value, StringComparison.OrdinalIgnoreCase) || x.Email.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (user == null) return (false, "Profile not found.", null);

        try
        {
            var salt = Convert.FromBase64String(user.PasswordSalt);
            var expected = Convert.FromBase64String(user.PasswordHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, expected.Length);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual)) return (false, "Incorrect password.", null);
            return (true, "Logged in.", ToPublic(user));
        }
        catch { return (false, "The local profile database is damaged.", null); }
    }

    private List<StoredUserProfile> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            return JsonSerializer.Deserialize<List<StoredUserProfile>>(File.ReadAllText(_path), JsonUtil.Options) ?? new();
        }
        catch { return new(); }
    }

    private void Save(List<StoredUserProfile> users) => File.WriteAllText(_path, JsonSerializer.Serialize(users, JsonUtil.Options));

    private static UserProfile ToPublic(StoredUserProfile x) => new() { Username = x.Username, Email = x.Email, CreatedAtUtc = x.CreatedAtUtc };
}
