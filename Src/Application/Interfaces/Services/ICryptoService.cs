namespace Application.Interfaces.Services;

/// <summary>
/// Provides cryptographic operations for API key security, including key generation,
/// hashing, encryption, and decryption.
/// </summary>
/// <remarks>
/// This service is responsible for all cryptographic operations related to API key lifecycle:
/// generating unique idempotency keys, creating secure hashes for database lookups,
/// deriving encryption keys, and encrypting/decrypting sensitive key material.
/// </remarks>
public interface ICryptoService
{
    /// <summary>
    /// Generates a unique, random idempotency key for API key creation.
    /// </summary>
    /// <returns>A cryptographically secure random string suitable for use as an idempotency key.</returns>
    public string GenerateIdempotencyKey();

    /// <summary>
    /// Creates a cryptographic hash of an idempotency key for secure database lookup.
    /// </summary>
    /// <remarks>
    /// The hash is stored in the database instead of the plaintext idempotency key
    /// to prevent exposure in case of database compromise. The plaintext key is returned
    /// to the client and never stored.
    /// </remarks>
    /// <param name="idempotencyKey">The plaintext idempotency key to hash.</param>
    /// <returns>A deterministic hash suitable for database indexing and lookups.</returns>
    public string HashForLookup(string idempotencyKey);

    /// <summary>
    /// Derives a cryptographic key suitable for encryption from an idempotency key and salt.
    /// </summary>
    /// <remarks>
    /// Uses a key derivation function (KDF) to stretch the idempotency key combined with
    /// a salt value, producing a cryptographically suitable encryption key.
    /// </remarks>
    /// <param name="idempotencyKey">The plaintext idempotency key to derive from.</param>
    /// <param name="salt">A unique salt value to strengthen the derivation.</param>
    /// <returns>A byte array containing the derived encryption key.</returns>
    public byte[] DeriveEncryptionKey(string idempotencyKey, byte[] salt);

    /// <summary>
    /// Encrypts plaintext data using the provided encryption key.
    /// </summary>
    /// <remarks>
    /// Returns a ciphertext string in a format suitable for storage in the database
    /// (typically base64-encoded with IV embedded).
    /// </remarks>
    /// <param name="plaintext">The data to encrypt.</param>
    /// <param name="key">The encryption key (typically from <see cref="DeriveEncryptionKey"/>).</param>
    /// <returns>The encrypted ciphertext, ready for storage.</returns>
    public string Encrypt(string plaintext, byte[] key);

    /// <summary>
    /// Decrypts ciphertext data using the provided encryption key.
    /// </summary>
    /// <param name="ciphertext">The encrypted data to decrypt.</param>
    /// <param name="key">The encryption key (must be the same key used in <see cref="Encrypt"/>).</param>
    /// <returns>The decrypted plaintext.</returns>
    public string Decrypt(string ciphertext, byte[] key);
}
