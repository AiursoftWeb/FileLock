using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aiursoft.FileLock;

public class ZeroTrustVault
{
    private const int Pbkdf2Iterations = 600_000;
    private const int SaltSize = 32;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int UuidSize = 16;

    private class VaultConfig
    {
        public string VaultSaltBase64 { get; init; } = string.Empty;
        public string VerifierNonceBase64 { get; set; } = string.Empty;
        public string VerifierTagBase64 { get; set; } = string.Empty;
        public string VerifierCipherBase64 { get; set; } = string.Empty;
    }

    public async Task Encrypt(string sourceFolder, string encryptedOutputFolder, string userKey)
    {
        var masterKey = InitOrLoadVault(encryptedOutputFolder, userKey);
        var sourceDir = new DirectoryInfo(sourceFolder);

        if (!sourceDir.Exists) throw new DirectoryNotFoundException($"Source not found: {sourceFolder}");

        var files = sourceDir.GetFiles("*", SearchOption.AllDirectories);
        var total = files.Length;
        var current = 0;

        foreach (var file in files)
        {
            current++;
            var relativePath = Path.GetRelativePath(sourceFolder, file.FullName);
            var fileUuid = RandomNumberGenerator.GetBytes(UuidSize);
            var uuidString = BitConverter.ToString(fileUuid).Replace("-", "").ToLower();
            var destPath = Path.Combine(encryptedOutputFolder, uuidString + ".enc");

            // 简单的进度显示
            Console.Write($"\r[{current}/{total}] Encrypting: {relativePath} -> {uuidString}.enc   ");

            await EncryptSingleFile(file.FullName, relativePath, destPath, masterKey, fileUuid);
        }
    }

    public async Task Decrypt(string sourceFolder, string decryptedOutputFolder, string userKey)
    {
        var masterKey = InitOrLoadVault(sourceFolder, userKey, allowCreate: false);
        var sourceDir = new DirectoryInfo(sourceFolder);

        if (!sourceDir.Exists) throw new DirectoryNotFoundException($"Vault not found: {sourceFolder}");

        var files = sourceDir.GetFiles("*.enc", SearchOption.TopDirectoryOnly);
        var total = files.Length;
        var current = 0;

        foreach (var file in files)
        {
            current++;
            Console.Write($"\r[{current}/{total}] Decrypting object: {file.Name}...   ");
            try
            {
                await DecryptSingleFile(file.FullName, decryptedOutputFolder, masterKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine(); // 换行
                Console.WriteLine($"[Warning] Failed to decrypt {file.Name}: {ex.Message}");
                throw;
            }
        }
    }

    private async Task EncryptSingleFile(string inputPath, string relativePath, string outputPath, byte[] masterKey, byte[] fileUuid)
    {
        var fileContent = await File.ReadAllBytesAsync(inputPath);
        var pathBytes = Encoding.UTF8.GetBytes(relativePath);
        var pathLengthBytes = BitConverter.GetBytes(pathBytes.Length);

        var payloadSize = 4 + pathBytes.Length + fileContent.Length;
        var payload = new byte[payloadSize];

        Buffer.BlockCopy(pathLengthBytes, 0, payload, 0, 4);
        Buffer.BlockCopy(pathBytes, 0, payload, 4, pathBytes.Length);
        Buffer.BlockCopy(fileContent, 0, payload, 4 + pathBytes.Length, fileContent.Length);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var fileKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, KeySize, fileUuid);

        var ciphertext = new byte[payload.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(fileKey, TagSize))
        {
            aes.Encrypt(nonce, payload, ciphertext, tag);
        }

        await using var fs = new FileStream(outputPath, FileMode.Create);
        await fs.WriteAsync(fileUuid);
        await fs.WriteAsync(nonce);
        await fs.WriteAsync(tag);
        await fs.WriteAsync(ciphertext);
    }

    private async Task DecryptSingleFile(string inputPath, string outputRoot, byte[] masterKey)
    {
        await using var fs = new FileStream(inputPath, FileMode.Open);

        var fileUuid = new byte[UuidSize];
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];

        if (await fs.ReadAsync(fileUuid) != UuidSize) throw new InvalidDataException("Header Error");
        if (await fs.ReadAsync(nonce) != NonceSize) throw new InvalidDataException("Header Error");
        if (await fs.ReadAsync(tag) != TagSize) throw new InvalidDataException("Header Error");

        var ciphertext = new byte[fs.Length - fs.Position];
        await fs.ReadExactlyAsync(ciphertext);

        var fileKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey, KeySize, fileUuid);
        var payload = new byte[ciphertext.Length];

        using (var aes = new AesGcm(fileKey, TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, payload);
        }

        var pathLength = BitConverter.ToInt32(payload, 0);
        if (pathLength < 0 || pathLength > payload.Length - 4)
            throw new InvalidDataException("Corrupted path data");

        var relativePath = Encoding.UTF8.GetString(payload, 4, pathLength);
        var fullRestoredPath = Path.Combine(outputRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullRestoredPath)!);

        var contentOffset = 4 + pathLength;
        var contentLength = payload.Length - contentOffset;

        await using var outFs = new FileStream(fullRestoredPath, FileMode.Create);
        await outFs.WriteAsync(payload, contentOffset, contentLength);
    }

    private byte[] InitOrLoadVault(string vaultRoot, string userPassword, bool allowCreate = true)
    {
        var configPath = Path.Combine(vaultRoot, "vault.config");
        Directory.CreateDirectory(vaultRoot);

        VaultConfig config;
        byte[] vaultSalt;

        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<VaultConfig>(json)!;
            vaultSalt = Convert.FromBase64String(config.VaultSaltBase64);
        }
        else
        {
            if (!allowCreate) throw new FileNotFoundException("Vault config not found.");
            vaultSalt = RandomNumberGenerator.GetBytes(SaltSize);
            config = new VaultConfig { VaultSaltBase64 = Convert.ToBase64String(vaultSalt) };
        }

        var masterKey = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(userPassword), vaultSalt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);

        if (File.Exists(configPath))
        {
            try { ValidatePassword(masterKey, config); }
            catch (CryptographicException) { throw new UnauthorizedAccessException(); }
        }
        else
        {
            CreateVerifier(masterKey, config);
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }

        return masterKey;
    }

    private void CreateVerifier(byte[] masterKey, VaultConfig config)
    {
        var plain = Encoding.UTF8.GetBytes("VALID");
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(masterKey, TagSize)) aes.Encrypt(nonce, plain, cipher, tag);
        config.VerifierNonceBase64 = Convert.ToBase64String(nonce);
        config.VerifierTagBase64 = Convert.ToBase64String(tag);
        config.VerifierCipherBase64 = Convert.ToBase64String(cipher);
    }

    private void ValidatePassword(byte[] masterKey, VaultConfig config)
    {
        var nonce = Convert.FromBase64String(config.VerifierNonceBase64);
        var tag = Convert.FromBase64String(config.VerifierTagBase64);
        var cipher = Convert.FromBase64String(config.VerifierCipherBase64);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(masterKey, TagSize)) aes.Decrypt(nonce, cipher, tag, plain);
        if (Encoding.UTF8.GetString(plain) != "VALID") throw new CryptographicException();
    }
}
