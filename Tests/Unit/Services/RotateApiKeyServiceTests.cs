namespace Unit.Services;

using Application.Commands;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class RotateApiKeyServiceTests
{
    private const string IDEMPOTENCY_KEY_PLAINTEXT = "plaintext-idempotency-key";
    private const string IDEMPOTENCY_KEY_HASH = "hashed-idempotency-key";

    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyActionRepository> _actionRepo;
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepo;
    private readonly Mock<ICreateApiKeyService> _createApiKeyService;
    private readonly Mock<IDeleteApiKeyService> _deleteApiKeyService;
    private readonly Mock<ICryptoService> _cryptoService;
    private readonly RotateApiKeyService _sut;

    public RotateApiKeyServiceTests()
    {
        _actionRepo = new Mock<IApiKeyActionRepository>();
        _idempotencyKeyRepo = new Mock<IIdempotencyKeyRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeyActions).Returns(_actionRepo.Object);
        _unitOfWork.Setup(u => u.IdempotencyKeys).Returns(_idempotencyKeyRepo.Object);
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((operation, _) => operation());

        _createApiKeyService = new Mock<ICreateApiKeyService>();
        _deleteApiKeyService = new Mock<IDeleteApiKeyService>();

        _cryptoService = new Mock<ICryptoService>();
        _cryptoService.Setup(c => c.HashForLookup(IDEMPOTENCY_KEY_PLAINTEXT)).Returns(IDEMPOTENCY_KEY_HASH);

        _sut = new RotateApiKeyService(
            _createApiKeyService.Object,
            _deleteApiKeyService.Object,
            _unitOfWork.Object,
            _cryptoService.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyValid_DeletesTheOldKey()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);
        SetUpActiveActions(apiKeyId);
        SetUpCreateResult();

        await _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, CancellationToken.None);

        _deleteApiKeyService.Verify(
            s => s.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyValid_CreatesNewKeyWithSameActiveActions()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);
        SetUpActiveActions(apiKeyId, ApiKeyActionEnum.Read, ApiKeyActionEnum.Write);
        SetUpCreateResult();

        await _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, CancellationToken.None);

        _createApiKeyService.Verify(
            s => s.ExecuteAsync(
                It.Is<CreateApiKeyCommand>(c =>
                    c.ExpiresAt == null
                    && c.Actions.Contains(ApiKeyActionEnum.Read)
                    && c.Actions.Contains(ApiKeyActionEnum.Write)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyValid_RunsInsideSingleTransaction()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);
        SetUpActiveActions(apiKeyId);
        SetUpCreateResult();

        await _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, CancellationToken.None);

        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyValid_ReturnsTheCreateResult()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);
        SetUpActiveActions(apiKeyId);
        CreateApiKeyResult expected = SetUpCreateResult();

        CreateApiKeyResult result = await _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task ExecuteAsync_WhenIdempotencyKeyInvalid_ThrowsNotFoundException()
    {
        _idempotencyKeyRepo
            .Setup(r => r.GetByHashAsync(IDEMPOTENCY_KEY_HASH, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _deleteApiKeyService.Verify(
            s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _createApiKeyService.Verify(
            s => s.ExecuteAsync(It.IsAny<CreateApiKeyCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransactionCompletesWithoutCreatingAKey_ThrowsConflictException()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpIdempotencyKey(apiKeyId);
        SetUpActiveActions(apiKeyId);

        // Simulates a transaction that commits without the create step ever assigning a result.
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Func<Task> act = () => _sut.ExecuteAsync(IDEMPOTENCY_KEY_PLAINTEXT, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
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

    private void SetUpActiveActions(Guid apiKeyId, params ApiKeyActionEnum[] actions)
    {
        List<ApiKeyAction> actionEntities = actions
            .Select(a => new ApiKeyAction
            {
                Id = Guid.NewGuid(),
                ApiKeyId = apiKeyId,
                Action = a,
                CreatedBy = "caller",
                CreatedAt = DateTime.UtcNow,
                DeletedAt = null,
                ApiKey = ApiKeyTestData.BuildApiKey(apiKeyId),
            })
            .ToList();

        _actionRepo
            .Setup(r => r.GetActiveByApiKeyIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actionEntities);
    }

    private CreateApiKeyResult SetUpCreateResult()
    {
        var result = new CreateApiKeyResult(Guid.NewGuid(), "new-secret", "new-idempotency-key");

        _createApiKeyService
            .Setup(s => s.ExecuteAsync(It.IsAny<CreateApiKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return result;
    }
}
