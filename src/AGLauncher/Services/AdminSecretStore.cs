using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AGLauncher.Services;

public sealed class AdminSecretStore
{
    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Atenoct Games", "AGLauncher", "Admin");

    private string PasswordPath => Path.Combine(_root, "admin-password.json");
    private string TokenPath => Path.Combine(_root, "github-token.bin");

    public AdminSecretStore() => Directory.CreateDirectory(_root);

    public bool HasPassword => File.Exists(PasswordPath);
    public bool HasToken => File.Exists(TokenPath);

    public bool SetInitialPassword(string password)
    {
        if (HasPassword || password.Length < 6) return false;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Hash(password, salt);
        var data = new PasswordData
        {
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash)
        };
        File.WriteAllText(PasswordPath, JsonSerializer.Serialize(data, JsonUtil.Options));
        return true;
    }

    public bool VerifyPassword(string password)
    {
        try
        {
            if (!File.Exists(PasswordPath)) return false;
            var data = JsonSerializer.Deserialize<PasswordData>(File.ReadAllText(PasswordPath), JsonUtil.Options);
            if (data == null) return false;
            var salt = Convert.FromBase64String(data.Salt);
            var expected = Convert.FromBase64String(data.Hash);
            var actual = Hash(password, salt);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }

    public void SaveToken(string token)
    {
        var raw = Encoding.UTF8.GetBytes(token.Trim());
        var encrypted = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(TokenPath, encrypted);
    }

    public string LoadToken()
    {
        try
        {
            if (!File.Exists(TokenPath)) return "";
            var encrypted = File.ReadAllBytes(TokenPath);
            var raw = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(raw);
        }
        catch { return ""; }
    }

    public void ClearToken()
    {
        try { if (File.Exists(TokenPath)) File.Delete(TokenPath); } catch { }
    }

    private static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 150000, HashAlgorithmName.SHA256, 32);

    private sealed class PasswordData
    {
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
    }
}
