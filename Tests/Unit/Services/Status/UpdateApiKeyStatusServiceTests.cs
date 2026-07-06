namespace Unit.Services.Status;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services.Status;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class UpdateApiKeyStatusServiceTests
{
    private const string IDEMPOTENCY_KEY_PLAINTEXT = "plaintext-idempotency-key";
    private const string IDEMPOTENCY_KEY_HASH = "hashed-idempotency-key";

    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyStatusRepository> _statusRepo;
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepo;
    private readonly Mock<ICryptoService> _cryptoService;
    private readonly UpdateApiKeyStatusService _sut;

    public UpdateApiKeyStatusServiceTests()
    {
        _statusRepo = new Mock<IApiKeyStatusRepository>();
        _idempotencyKeyRepo = new Mock<IIdempotencyKeyRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeyStatuses).Returns(_statusRepo.Object);
        _unitOfWork.Setup(u => u.IdempotencyKeys).Returns(_idempotencyKeyRepo.Object);

        _cryptoService = new Mock<ICryptoService>();
        _cryptoService.Setup(c => c.HashForLookup(IDEMPOTENCY_KEY_PLAINTEXT)).Returns(IDEMPOTENCY_KEY_HASH);

        _sut = new UpdateApiKeyStatusService(_unitOfWork.Object, _cryptoService.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyValid_SoftDeletesCurrentStatus()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);

        await _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, ApiKeyStatusEnum.Active);

        _statusRepo.Verify(r => r.SoftDeleteAsync(apiKeyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyValid_AddsNewStatusForResolvedApiKey()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);

        await _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, ApiKeyStatusEnum.Active);

        _statusRepo.Verify(
            r => r.AddAsync(
                It.Is<ApiKeyStatus>(s => s.ApiKeyId == apiKeyId && s.Status == ApiKeyStatusEnum.Active),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyInvalid_ThrowsNotFoundException()
    {
        _idempotencyKeyRepo
            .Setup(r => r.GetByHashAsync(IDEMPOTENCY_KEY_HASH, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, ApiKeyStatusEnum.Active);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyInvalid_DoesNotAddNewStatus()
    {
        _idempotencyKeyRepo
            .Setup(r => r.GetByHashAsync(IDEMPOTENCY_KEY_HASH, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, ApiKeyStatusEnum.Active);

        await act.Should().ThrowAsync<NotFoundException>();
        _statusRepo.Verify(r => r.AddAsync(It.IsAny<ApiKeyStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrentStatusIsRevoked_PropagatesConflictExceptionAndDoesNotAddNewStatus()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);
        _statusRepo
            .Setup(r => r.SoftDeleteAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("The current Status: Revoked of the ApiKey cannot be changed"));

        Func<Task> act = () => _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, ApiKeyStatusEnum.Active);

        await act.Should().ThrowAsync<ConflictException>();
        _statusRepo.Verify(r => r.AddAsync(It.IsAny<ApiKeyStatus>(), It.IsAny<CancellationToken>()), Times.Never);
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
