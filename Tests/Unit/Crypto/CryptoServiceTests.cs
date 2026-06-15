namespace Unit.Crypto;

using Application.Services;
using Application.Settings;
using FluentAssertions;

public sealed class CryptoServiceTests
{
    private static CryptoService BuildSut() =>
        new CryptoService(new CryptoSettings { DegreeOfParallelism = 1, MemorySize = 64, Iterations = 1 });

    [Fact]
    public void GenerateIdempotencyKey_Returns128CharacterString()
    {
        CryptoService sut = BuildSut();

        string key = sut.GenerateIdempotencyKey();

        key.Length.Should().Be(128);
    }

    [Fact]
    public void GenerateIdempotencyKey_EachCallReturnsDifferentKey()
    {
        CryptoService sut = BuildSut();

        string key1 = sut.GenerateIdempotencyKey();
        string key2 = sut.GenerateIdempotencyKey();

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void HashForLookup_SameInput_ReturnsSameHash()
    {
        CryptoService sut = BuildSut();

        string hash1 = sut.HashForLookup("some-idempotency-key");
        string hash2 = sut.HashForLookup("some-idempotency-key");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashForLookup_DifferentInputs_ReturnDifferentHashes()
    {
        CryptoService sut = BuildSut();

        string hash1 = sut.HashForLookup("key-one");
        string hash2 = sut.HashForLookup("key-two");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void DeriveEncryptionKey_Returns32Bytes()
    {
        CryptoService sut = BuildSut();
        byte[] salt = new byte[32];

        byte[] key = sut.DeriveEncryptionKey("idempotency-key", salt);

        key.Should().HaveCount(32);
    }

    [Fact]
    public void DeriveEncryptionKey_SameInputAndSalt_ReturnsSameKey()
    {
        CryptoService sut = BuildSut();
        byte[] salt = new byte[32];

        byte[] key1 = sut.DeriveEncryptionKey("idempotency-key", salt);
        byte[] key2 = sut.DeriveEncryptionKey("idempotency-key", salt);

        key1.Should().Equal(key2);
    }

    [Fact]
    public void DeriveEncryptionKey_DifferentSalts_ReturnDifferentKeys()
    {
        CryptoService sut = BuildSut();
        byte[] salt1 = new byte[32];
        byte[] salt2 = new byte[32];
        salt2[0] = 1;

        byte[] key1 = sut.DeriveEncryptionKey("idempotency-key", salt1);
        byte[] key2 = sut.DeriveEncryptionKey("idempotency-key", salt2);

        key1.Should().NotEqual(key2);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
    {
        CryptoService sut = BuildSut();
        byte[] key = new byte[32];
        const string plaintext = "lk_supersecretvalue";

        string ciphertext = sut.Encrypt(plaintext, key);
        string recovered = sut.Decrypt(ciphertext, key);

        recovered.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachCall()
    {
        CryptoService sut = BuildSut();
        byte[] key = new byte[32];

        string ct1 = sut.Encrypt("same plaintext", key);
        string ct2 = sut.Encrypt("same plaintext", key);

        ct1.Should().NotBe(ct2);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsCryptographicException()
    {
        CryptoService sut = BuildSut();
        byte[] encryptKey = new byte[32];
        byte[] wrongKey = new byte[32];
        wrongKey[0] = 1;

        string ciphertext = sut.Encrypt("secret", encryptKey);

        Action act = () => sut.Decrypt(ciphertext, wrongKey);
        act.Should().Throw<Exception>();
    }
}
