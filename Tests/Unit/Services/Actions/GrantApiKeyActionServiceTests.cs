namespace Unit.Services.Actions;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Services.Actions;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class GrantApiKeyActionServiceTests
{
    private const string CREATED_BY = "caller";

    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepo;
    private readonly Mock<IApiKeyActionRepository> _actionRepo;
    private readonly GrantApiKeyActionService _sut;

    public GrantApiKeyActionServiceTests()
    {
        _idempotencyKeyRepo = new Mock<IIdempotencyKeyRepository>();
        _actionRepo = new Mock<IApiKeyActionRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeyActions).Returns(_actionRepo.Object);

        _sut = new GrantApiKeyActionService(_unitOfWork.Object, _idempotencyKeyRepo.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionNotGranted_AddsNewAction()
    {
        Guid apiKeyId = Guid.NewGuid();
        string idempotencyKeyHash = "test-hash-1";
        SetUpIdempotencyKey(idempotencyKeyHash, apiKeyId);
        SetUpExistingActions(apiKeyId);

        await _sut.ExecuteAsync(idempotencyKeyHash, ApiKeyActionEnum.Write, CREATED_BY);

        _actionRepo.Verify(
            r => r.AddAsync(
                It.Is<ApiKeyAction>(a =>
                    a.ApiKeyId == apiKeyId && a.Action == ApiKeyActionEnum.Write && a.CreatedBy == CREATED_BY),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionNotGranted_ReturnsGrantedAction()
    {
        Guid apiKeyId = Guid.NewGuid();
        string idempotencyKeyHash = "test-hash-1";
        SetUpIdempotencyKey(idempotencyKeyHash, apiKeyId);
        SetUpExistingActions(apiKeyId);

        ApiKeyAction result = await _sut.ExecuteAsync(idempotencyKeyHash, ApiKeyActionEnum.Write, CREATED_BY);

        result.Action.Should().Be(ApiKeyActionEnum.Write);
        result.ApiKeyId.Should().Be(apiKeyId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionAlreadyGranted_ThrowsConflictException()
    {
        Guid apiKeyId = Guid.NewGuid();
        string idempotencyKeyHash = "test-hash-1";
        SetUpIdempotencyKey(idempotencyKeyHash, apiKeyId);
        SetUpExistingActions(apiKeyId, ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Write));

        Func<Task> act = () => _sut.ExecuteAsync(idempotencyKeyHash, ApiKeyActionEnum.Write, CREATED_BY);

        await act.Should().ThrowAsync<ConflictException>();
        _actionRepo.Verify(r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionWasRevoked_AllowsReGrant()
    {
        Guid apiKeyId = Guid.NewGuid();
        string idempotencyKeyHash = "test-hash-1";
        SetUpIdempotencyKey(idempotencyKeyHash, apiKeyId);

        // A revoked grant is soft-deleted and therefore absent from the active set.
        SetUpExistingActions(apiKeyId);

        await _sut.ExecuteAsync(idempotencyKeyHash, ApiKeyActionEnum.Write, CREATED_BY);

        _actionRepo.Verify(
            r => r.AddAsync(
                It.Is<ApiKeyAction>(a => a.Action == ApiKeyActionEnum.Write && a.DeletedAt == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyDoesNotExist_ThrowsNotFoundException()
    {
        string idempotencyKeyHash = "nonexistent-hash";
        _idempotencyKeyRepo
            .Setup(r => r.GetByHashAsync(idempotencyKeyHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdempotencyKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(idempotencyKeyHash, ApiKeyActionEnum.Write, CREATED_BY);

        await act.Should().ThrowAsync<NotFoundException>();
        _actionRepo.Verify(r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private void SetUpExistingActions(Guid apiKeyId, params ApiKeyAction[] actions)
    {
        _actionRepo
            .Setup(r => r.GetActiveByApiKeyIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions.ToList());
    }
}
