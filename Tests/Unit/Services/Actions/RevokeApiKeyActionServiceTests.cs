namespace Unit.Services.Actions;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services.Actions;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class RevokeApiKeyActionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepo;
    private readonly Mock<IApiKeyActionRepository> _actionRepo;
    private readonly Mock<ICryptoService> _cryptoService;
    private readonly RevokeApiKeyActionService _sut;

    public RevokeApiKeyActionServiceTests()
    {
        _idempotencyKeyRepo = new Mock<IIdempotencyKeyRepository>();
        _actionRepo = new Mock<IApiKeyActionRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeyActions).Returns(_actionRepo.Object);

        // Identity hashing keeps the raw key equal to the lookup hash the repository is set up against.
        _cryptoService = new Mock<ICryptoService>();
        _cryptoService.Setup(c => c.HashForLookup(It.IsAny<string>())).Returns<string>(s => s);

        _sut = new RevokeApiKeyActionService(_unitOfWork.Object, _idempotencyKeyRepo.Object, _cryptoService.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionGranted_RemovesAction()
    {
        Guid apiKeyId = Guid.NewGuid();
        string idempotencyKeyHash = "test-hash-1";
        SetUpIdempotencyKey(idempotencyKeyHash, apiKeyId);
        _actionRepo
            .Setup(r => r.RemoveAsync(apiKeyId, ApiKeyActionEnum.Write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ExecuteAsync(idempotencyKeyHash, "Write");

        _actionRepo.Verify(
            r => r.RemoveAsync(apiKeyId, ApiKeyActionEnum.Write, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionNotGranted_ThrowsNotFoundException()
    {
        Guid apiKeyId = Guid.NewGuid();
        string idempotencyKeyHash = "test-hash-1";
        SetUpIdempotencyKey(idempotencyKeyHash, apiKeyId);
        _actionRepo
            .Setup(r => r.RemoveAsync(apiKeyId, ApiKeyActionEnum.Write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Func<Task> act = () => _sut.ExecuteAsync(idempotencyKeyHash, "Write");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyDoesNotExist_ThrowsNotFoundException()
    {
        string idempotencyKeyHash = "nonexistent-hash";
        _idempotencyKeyRepo
            .Setup(r => r.GetByHashAsync(idempotencyKeyHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(idempotencyKeyHash, "Write");

        await act.Should().ThrowAsync<NotFoundException>();
        _actionRepo.Verify(
            r => r.RemoveAsync(It.IsAny<Guid>(), It.IsAny<ApiKeyActionEnum>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetUpIdempotencyKey(string idempotencyKeyHash, Guid apiKeyId)
    {
        var apiKey = ApiKeyTestData.BuildApiKey(apiKeyId);
        var idempotencyKey = new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            IdempotencyKeyHash = idempotencyKeyHash,
            Salt = "test-salt",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-user",
            ApiKey = apiKey,
        };

        _idempotencyKeyRepo
            .Setup(r => r.GetByHashAsync(idempotencyKeyHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(idempotencyKey);
    }
}
