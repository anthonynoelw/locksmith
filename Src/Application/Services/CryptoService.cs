namespace Application.Services;

using System.Security.Cryptography;
using System.Text;
using Application.Interfaces.Services;
using Application.Settings;
using Konscious.Security.Cryptography;

/// <summary>
/// Provides cryptographic operations for API key generation and secret protection.
/// Uses SHA-256 for deterministic idempotency-key lookup hashes and Argon2id + AES-GCM
/// for deriving per-key encryption keys and encrypting secrets at rest.
/// </summary>
public sealed class CryptoService : ICryptoService
{
    private const int IDEMPOTENCY_KEY_BYTES = 96;
    private const int API_KEY_SECRET_BYTES = 32;
    private const int NONCE_SIZE = 12;
    private const int TAG_SIZE = 16;
    private const int DERIVED_KEY_SIZE = 32;

    private readonly CryptoSettings _settings;

    /// <summary>Initializes a new instance of the <see cref="CryptoService"/> class.</summary>
    /// <param name="settings">Argon2id tuning parameters.</param>
    public CryptoService(CryptoSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public string GenerateIdempotencyKey()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(IDEMPOTENCY_KEY_BYTES);
        return Base64UrlEncode(bytes);
    }

    /// <inheritdoc/>
    public string GenerateApiKeySecret()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(API_KEY_SECRET_BYTES);
        return $"lk_{Base64UrlEncode(bytes)}";
    }

    /// <inheritdoc/>
    public string HashForLookup(string secret)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(secret);
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <inheritdoc/>
    public byte[] DeriveEncryptionKey(string idempotencyKey, byte[] salt)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(idempotencyKey);
        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            DegreeOfParallelism = _settings.DegreeOfParallelism,
            MemorySize = _settings.MemorySize,
            Iterations = _settings.Iterations,
        };
        return argon2.GetBytes(DERIVED_KEY_SIZE);
    }

    /// <inheritdoc/>
    public string Encrypt(string plaintext, byte[] key)
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] nonce = RandomNumberGenerator.GetBytes(NONCE_SIZE);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[TAG_SIZE];

        using var aesGcm = new AesGcm(key, TAG_SIZE);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        byte[] result = new byte[NONCE_SIZE + ciphertext.Length + TAG_SIZE];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, NONCE_SIZE);
        tag.CopyTo(result, NONCE_SIZE + ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext, byte[] key)
    {
        byte[] data = Convert.FromBase64String(ciphertext);
        byte[] nonce = data[..NONCE_SIZE];
        byte[] tag = data[^TAG_SIZE..];
        byte[] encryptedData = data[NONCE_SIZE..^TAG_SIZE];
        byte[] plaintext = new byte[encryptedData.Length];

        using var aesGcm = new AesGcm(key, TAG_SIZE);
        aesGcm.Decrypt(nonce, encryptedData, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
