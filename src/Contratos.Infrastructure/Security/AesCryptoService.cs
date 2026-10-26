using System.Security.Cryptography;
using System.Text;
using Contratos.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Contratos.Infrastructure.Security;

/// <summary>
/// Serviço de criptografia corporativa AES-256 (CBC/PKCS7 + IV Dinâmico + HMAC-SHA256)
/// com suporte nativo a tokens {ENC:...} e hashing de senhas PBKDF2-SHA256 com Salt.
/// </summary>
public class AesCryptoService : ICryptoService
{
    public const string TokenPrefix = "{ENC:";
    public const string TokenSuffix = "}";

    private const string DefaultMasterSecret = "CLM-Corporativo-MasterKey-2026-AES256-HmacSha256-SecKey!";
    private static readonly byte[] KeyDerivationSalt = Encoding.UTF8.GetBytes("Contratos.CLM.Salt.v1.2026");

    private readonly byte[] _aesKey;
    private readonly byte[] _hmacKey;

    public AesCryptoService(IConfiguration? configuration = null)
    {
        var configuredSecret = configuration?["Security:MasterEncryptionKey"];
        var secret = string.IsNullOrWhiteSpace(configuredSecret) ? DefaultMasterSecret : configuredSecret;

        using var kdf = new Rfc2898DeriveBytes(secret, KeyDerivationSalt, 50_000, HashAlgorithmName.SHA256);
        _aesKey = kdf.GetBytes(32);  // 256 bits para AES-256
        _hmacKey = kdf.GetBytes(32); // 256 bits para HMAC-SHA256
    }

    public AesCryptoService(string customMasterKey)
    {
        if (string.IsNullOrWhiteSpace(customMasterKey))
            throw new ArgumentException("A chave mestra não pode ser vazia.", nameof(customMasterKey));

        using var kdf = new Rfc2898DeriveBytes(customMasterKey, KeyDerivationSalt, 50_000, HashAlgorithmName.SHA256);
        _aesKey = kdf.GetBytes(32);
        _hmacKey = kdf.GetBytes(32);
    }

    public bool IsEncryptedToken(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();
        return trimmed.StartsWith(TokenPrefix, StringComparison.Ordinal)
            && trimmed.EndsWith(TokenSuffix, StringComparison.Ordinal)
            && trimmed.Length > (TokenPrefix.Length + TokenSuffix.Length);
    }

    public string Encrypt(string plainText)
    {
        if (plainText == null)
            throw new ArgumentNullException(nameof(plainText));

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _aesKey;
        aes.GenerateIV();

        var iv = aes.IV;
        byte[] cipherBytes;

        using (var encryptor = aes.CreateEncryptor(aes.Key, iv))
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        // Payload: [IV (16 bytes)] + [CipherText (N bytes)] + [HMAC-SHA256 (32 bytes)]
        var ivAndCipher = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, ivAndCipher, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, ivAndCipher, iv.Length, cipherBytes.Length);

        byte[] mac;
        using (var hmac = new HMACSHA256(_hmacKey))
        {
            mac = hmac.ComputeHash(ivAndCipher);
        }

        var finalPayload = new byte[ivAndCipher.Length + mac.Length];
        Buffer.BlockCopy(ivAndCipher, 0, finalPayload, 0, ivAndCipher.Length);
        Buffer.BlockCopy(mac, 0, finalPayload, ivAndCipher.Length, mac.Length);

        return Convert.ToBase64String(finalPayload);
    }

    public string EncryptWithToken(string plainText)
    {
        if (IsEncryptedToken(plainText))
            return plainText.Trim();

        return $"{TokenPrefix}{Encrypt(plainText)}{TokenSuffix}";
    }

    public string Decrypt(string cipherTextOrToken)
    {
        if (string.IsNullOrWhiteSpace(cipherTextOrToken))
            return string.Empty;

        var raw = cipherTextOrToken.Trim();
        if (IsEncryptedToken(raw))
        {
            raw = raw.Substring(TokenPrefix.Length, raw.Length - TokenPrefix.Length - TokenSuffix.Length);
        }

        var fullBytes = Convert.FromBase64String(raw);
        if (fullBytes.Length < 16 + 16 + 32)
        {
            throw new CryptographicException("Payload criptografado inválido ou truncado.");
        }

        var macOffset = fullBytes.Length - 32;
        var ivAndCipher = new byte[macOffset];
        var receivedMac = new byte[32];

        Buffer.BlockCopy(fullBytes, 0, ivAndCipher, 0, macOffset);
        Buffer.BlockCopy(fullBytes, macOffset, receivedMac, 0, 32);

        using (var hmac = new HMACSHA256(_hmacKey))
        {
            var computedMac = hmac.ComputeHash(ivAndCipher);
            if (!CryptographicOperations.FixedTimeEquals(computedMac, receivedMac))
            {
                throw new CryptographicException("Falha na verificação de integridade HMAC-SHA256 do token criptografado.");
            }
        }

        var iv = new byte[16];
        var cipherBytes = new byte[macOffset - 16];
        Buffer.BlockCopy(ivAndCipher, 0, iv, 0, 16);
        Buffer.BlockCopy(ivAndCipher, 16, cipherBytes, 0, cipherBytes.Length);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _aesKey;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Resolve tokens {ENC:...} embutidos dentro de uma ConnectionString ou configuração.
    /// </summary>
    public string ResolveConnectionStringTokens(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;

        var result = connectionString;
        int startIndex = 0;

        while ((startIndex = result.IndexOf(TokenPrefix, startIndex, StringComparison.Ordinal)) >= 0)
        {
            int endIndex = result.IndexOf(TokenSuffix, startIndex + TokenPrefix.Length, StringComparison.Ordinal);
            if (endIndex < 0)
                break;

            var token = result.Substring(startIndex, endIndex - startIndex + TokenSuffix.Length);
            var decrypted = Decrypt(token);
            result = result.Remove(startIndex, token.Length).Insert(startIndex, decrypted);
            startIndex += decrypted.Length;
        }

        return result;
    }

    public string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("A senha não pode ser vazia.", nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(16);
        const int iterations = 100_000;

        byte[] subkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return $"PBKDF2-SHA256${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}";
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2-SHA256")
            return false;

        if (!int.TryParse(parts[1], out int iterations))
            return false;

        byte[] salt;
        byte[] expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedSubkey = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        byte[] actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedSubkey.Length);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}
