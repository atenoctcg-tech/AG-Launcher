using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AGLauncher.Services;

internal sealed class AdminCredentialFile
{
    public string TokenSalt { get; set; } = "";
    public string TokenNonce { get; set; } = "";
    public string TokenTag { get; set; } = "";
    public string TokenCipher { get; set; } = "";
}

public sealed class AdminCredentialService
{
    private readonly string _path;

    // PBKDF2 verifier for the studio password. The plaintext password is never stored in the repository.
    private static readonly byte[] AdminSalt = Convert.FromBase64String("QUdMYXVuY2hlckFkbWluLXYx");
    private static readonly byte[] AdminHash = Convert.FromBase64String("jW5moYnX6rA0/7PVpYsRQkPkhbdeoA9KvGlXbO4z1P0=");

    public AdminCredentialService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Atenoct Games", "AGLauncher");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "admin-credentials.json");
    }

    public bool IsConfigured => true;

    public (bool Ok, string Message) SetupPassword(string password) =>
        (false, "The studio admin password is managed by Atenoct Games.");

    public bool VerifyPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            AdminSalt,
            200_000,
            HashAlgorithmName.SHA256,
            32);
        return CryptographicOperations.FixedTimeEquals(AdminHash, actual);
    }

    public string LoadToken(string password)
    {
        if (!VerifyPassword(password)) return "";
        var file = Load();
        if (file == null || string.IsNullOrWhiteSpace(file.TokenCipher)) return "";

        try
        {
            var salt = Convert.FromBase64String(file.TokenSalt);
            var nonce = Convert.FromBase64String(file.TokenNonce);
            var tag = Convert.FromBase64String(file.TokenTag);
            var cipher = Convert.FromBase64String(file.TokenCipher);
            var key = DeriveEncryptionKey(password, salt);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return ""; }
    }

    public (bool Ok, string Message) SaveToken(string password, string token)
    {
        if (!VerifyPassword(password)) return (false, "Admin password is not valid.");
        if (string.IsNullOrWhiteSpace(token)) return (false, "GitHub token cannot be empty.");

        var file = Load() ?? new AdminCredentialFile();
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = DeriveEncryptionKey(password, salt);
        var plain = Encoding.UTF8.GetBytes(token.Trim());
        var cipher = new byte[plain.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(key, 16))
            aes.Encrypt(nonce, plain, cipher, tag);

        file.TokenSalt = Convert.ToBase64String(salt);
        file.TokenNonce = Convert.ToBase64String(nonce);
        file.TokenTag = Convert.ToBase64String(tag);
        file.TokenCipher = Convert.ToBase64String(cipher);
        Save(file);
        return (true, "GitHub token saved encrypted on this PC.");
    }

    private static byte[] DeriveEncryptionKey(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 180_000, HashAlgorithmName.SHA256, 32);

    private AdminCredentialFile? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<AdminCredentialFile>(File.ReadAllText(_path), JsonUtil.Options);
        }
        catch { return null; }
    }

    private void Save(AdminCredentialFile file) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(file, JsonUtil.Options));
}
