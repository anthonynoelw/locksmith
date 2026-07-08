namespace Unit.Services;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class DeleteApiKeyServiceTests
{
    private const string IDEMPOTENCY_KEY_PLAINTEXT = "plaintext-idempotency-key";
    private const string IDEMPOTENCY_KEY_HASH = "hashed-idempotency-key";

    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyRepository> _apiKeyRepo;
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepo;
    private readonly Mock<ICryptoService> _cryptoService;
    private readonly DeleteApiKeyService _sut;

    public DeleteApiKeyServiceTests()
    {
        _apiKeyRepo = new Mock<IApiKeyRepository>();
        _idempotencyKeyRepo = new Mock<IIdempotencyKeyRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeys).Returns(_apiKeyRepo.Object);
        _unitOfWork.Setup(u => u.IdempotencyKeys).Returns(_idempotencyKeyRepo.Object);

        _cryptoService = new Mock<ICryptoService>();
        _cryptoService.Setup(c => c.HashForLookup(IDEMPOTENCY_KEY_PLAINTEXT)).Returns(IDEMPOTENCY_KEY_HASH);

        _sut = new DeleteApiKeyService(_unitOfWork.Object, _cryptoService.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyValid_DeletesResolvedApiKey()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);

        await _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, CancellationToken.None);

        _apiKeyRepo.Verify(r => r.DeleteAsync(apiKeyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyValid_ReturnsTrue()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);

        bool result = await _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyInvalid_ThrowsNotFoundException()
    {
        _idempotencyKeyRepo
            .Setup(r => r.GetByHashAsync(IDEMPOTENCY_KEY_HASH, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _apiKeyRepo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetUpIdempotencyKey(Guid apiKeyId)
    {
        ApiKey apiKey = ApiKeyTestData.BuildApiKey(apiKeyId);

        var idempotencyKeyEntity = new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            IdempotencyKeyHash = IDEMPOTENCY_KEY_HASH,
            Salt = "salt",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "caller",
            ApiKey = apiKey,
        };

        _idempotencyKeyRepo
            .Setup(r => r.GetByHashAsync(IDEMPOTENCY_KEY_HASH, It.IsAny<CancellationToken>()))
            .ReturnsAsync(idempotencyKeyEntity);
    }
}
