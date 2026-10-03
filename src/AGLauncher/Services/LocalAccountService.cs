using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AGLauncher.Services;

public sealed class LocalAccountService
{
    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Atenoct Games", "AGLauncher");

    private string AccountsPath => Path.Combine(_root, "accounts.json");
    private string SessionPath => Path.Combine(_root, "session.json");

    public LocalAccountService() => Directory.CreateDirectory(_root);

    public string? CurrentUsername
    {
        get
        {
            try
            {
                if (!File.Exists(SessionPath)) return null;
                var s = JsonSerializer.Deserialize<SessionState>(File.ReadAllText(SessionPath), JsonUtil.Options);
                return string.IsNullOrWhiteSpace(s?.Username) ? null : s.Username;
            }
            catch { return null; }
        }
    }

    public (bool Ok, string Message) Register(string username, string password)
    {
        username = (username ?? "").Trim();
        if (username.Length < 3) return (false, "Username must contain at least 3 characters.");
        if (password.Length < 6) return (false, "Password must contain at least 6 characters.");

        var db = Load();
        if (db.Users.Any(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            return (false, "This username already exists on this PC.");

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Hash(password, salt);
        db.Users.Add(new LocalUser
        {
            Username = username,
            Salt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(hash),
            CreatedAtUtc = DateTime.UtcNow
        });
        Save(db);
        SaveSession(username);
        return (true, "Profile created and signed in.");
    }

    public (bool Ok, string Message) Login(string username, string password)
    {
        var db = Load();
        var user = db.Users.FirstOrDefault(x => x.Username.Equals((username ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        if (user == null) return (false, "Profile not found on this PC.");

        try
        {
            var salt = Convert.FromBase64String(user.Salt);
            var expected = Convert.FromBase64String(user.PasswordHash);
            var actual = Hash(password, salt);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected))
                return (false, "Incorrect password.");
        }
        catch { return (false, "Profile data is invalid."); }

        SaveSession(user.Username);
        return (true, "Signed in.");
    }

    public void Logout()
    {
        try { if (File.Exists(SessionPath)) File.Delete(SessionPath); } catch { }
    }

    private byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 120000, HashAlgorithmName.SHA256, 32);

    private AccountDb Load()
    {
        try
        {
            if (File.Exists(AccountsPath))
                return JsonSerializer.Deserialize<AccountDb>(File.ReadAllText(AccountsPath), JsonUtil.Options) ?? new AccountDb();
        }
        catch { }
        return new AccountDb();
    }

    private void Save(AccountDb db) =>
        File.WriteAllText(AccountsPath, JsonSerializer.Serialize(db, JsonUtil.Options));

    private void SaveSession(string username) =>
        File.WriteAllText(SessionPath, JsonSerializer.Serialize(new SessionState { Username = username }, JsonUtil.Options));

    private sealed class AccountDb { public List<LocalUser> Users { get; set; } = new(); }
    private sealed class LocalUser
    {
        public string Username { get; set; } = "";
        public string Salt { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
    }
    private sealed class SessionState { public string Username { get; set; } = ""; }
}
