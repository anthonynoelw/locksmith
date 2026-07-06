namespace Unit.Services.Actions;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Services.Actions;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class ReplaceApiKeyActionsServiceTests
{
    private const string CREATED_BY = "caller";

    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyRepository> _apiKeyRepo;
    private readonly Mock<IApiKeyActionRepository> _actionRepo;
    private readonly ReplaceApiKeyActionsService _sut;

    public ReplaceApiKeyActionsServiceTests()
    {
        _apiKeyRepo = new Mock<IApiKeyRepository>();
        _actionRepo = new Mock<IApiKeyActionRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeys).Returns(_apiKeyRepo.Object);
        _unitOfWork.Setup(u => u.ApiKeyActions).Returns(_actionRepo.Object);
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((operation, _) => operation());

        _sut = new ReplaceApiKeyActionsService(_unitOfWork.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestedActionIsNew_AddsIt()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        SetUpActiveActions(apiKeyId, ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Read));

        await _sut.ExecuteAsync(apiKeyId, new[] { ApiKeyActionEnum.Read, ApiKeyActionEnum.Write }, CREATED_BY);

        _actionRepo.Verify(
            r => r.AddAsync(
                It.Is<ApiKeyAction>(a => a.Action == ApiKeyActionEnum.Write && a.CreatedBy == CREATED_BY),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveActionNotRequested_RemovesIt()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        SetUpActiveActions(
            apiKeyId,
            ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Read),
            ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Write));

        await _sut.ExecuteAsync(apiKeyId, new[] { ApiKeyActionEnum.Read }, CREATED_BY);

        _actionRepo.Verify(
            r => r.RemoveAsync(apiKeyId, ApiKeyActionEnum.Write, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionAlreadyActive_DoesNotReAddOrRemoveIt()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        SetUpActiveActions(apiKeyId, ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Read));

        await _sut.ExecuteAsync(apiKeyId, new[] { ApiKeyActionEnum.Read }, CREATED_BY);

        _actionRepo.Verify(r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()), Times.Never);
        _actionRepo.Verify(
            r => r.RemoveAsync(It.IsAny<Guid>(), It.IsAny<ApiKeyActionEnum>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmptySetRequested_RemovesAllActiveActions()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        SetUpActiveActions(
            apiKeyId,
            ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Read),
            ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Write));

        await _sut.ExecuteAsync(apiKeyId, Array.Empty<ApiKeyActionEnum>(), CREATED_BY);

        _actionRepo.Verify(
            r => r.RemoveAsync(apiKeyId, It.IsAny<ApiKeyActionEnum>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenRevokedActionIsRequested_ReGrantsIt()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);

        // A revoked grant is soft-deleted and therefore absent from the active set.
        SetUpActiveActions(apiKeyId);

        await _sut.ExecuteAsync(apiKeyId, new[] { ApiKeyActionEnum.Read }, CREATED_BY);

        _actionRepo.Verify(
            r => r.AddAsync(
                It.Is<ApiKeyAction>(a => a.Action == ApiKeyActionEnum.Read && a.DeletedAt == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsKeptAndNewlyGrantedActions()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        SetUpActiveActions(
            apiKeyId,
            ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Read),
            ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Delete));

        IReadOnlyList<ApiKeyAction> result = await _sut.ExecuteAsync(
            apiKeyId,
            new[] { ApiKeyActionEnum.Read, ApiKeyActionEnum.Write },
            CREATED_BY);

        result.Should().HaveCount(2);
        result.Should().ContainSingle(a => a.Action == ApiKeyActionEnum.Read);
        result.Should().ContainSingle(a => a.Action == ApiKeyActionEnum.Write);
    }

    [Fact]
    public async Task ExecuteAsync_RunsAllChangesInsideSingleTransaction()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        SetUpActiveActions(apiKeyId, ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Read));

        await _sut.ExecuteAsync(apiKeyId, new[] { ApiKeyActionEnum.Write }, CREATED_BY);

        _unitOfWork.Verify(
            u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyDoesNotExist_ThrowsNotFoundException()
    {
        Guid apiKeyId = Guid.NewGuid();
        _apiKeyRepo
            .Setup(r => r.GetByIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(apiKeyId, new[] { ApiKeyActionEnum.Read }, CREATED_BY);

        await act.Should().ThrowAsync<NotFoundException>();
        _actionRepo.Verify(r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetUpApiKey(Guid apiKeyId)
    {
        _apiKeyRepo
            .Setup(r => r.GetByIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiKeyTestData.BuildApiKey(apiKeyId));
    }

    private void SetUpActiveActions(Guid apiKeyId, params ApiKeyAction[] actions)
    {
        _actionRepo
            .Setup(r => r.GetActiveByApiKeyIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions.ToList());
    }
}
