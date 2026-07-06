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
    private readonly Mock<IApiKeyRepository> _apiKeyRepo;
    private readonly Mock<IApiKeyActionRepository> _actionRepo;
    private readonly GrantApiKeyActionService _sut;

    public GrantApiKeyActionServiceTests()
    {
        _apiKeyRepo = new Mock<IApiKeyRepository>();
        _actionRepo = new Mock<IApiKeyActionRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeys).Returns(_apiKeyRepo.Object);
        _unitOfWork.Setup(u => u.ApiKeyActions).Returns(_actionRepo.Object);

        _sut = new GrantApiKeyActionService(_unitOfWork.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionNotGranted_AddsNewAction()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        SetUpExistingActions(apiKeyId);

        await _sut.ExecuteAsync(apiKeyId, ApiKeyActionEnum.Write, CREATED_BY);

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
        SetUpApiKey(apiKeyId);
        SetUpExistingActions(apiKeyId);

        ApiKeyAction result = await _sut.ExecuteAsync(apiKeyId, ApiKeyActionEnum.Write, CREATED_BY);

        result.Action.Should().Be(ApiKeyActionEnum.Write);
        result.ApiKeyId.Should().Be(apiKeyId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionAlreadyGranted_ThrowsConflictException()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        SetUpExistingActions(apiKeyId, ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Write));

        Func<Task> act = () => _sut.ExecuteAsync(apiKeyId, ApiKeyActionEnum.Write, CREATED_BY);

        await act.Should().ThrowAsync<ConflictException>();
        _actionRepo.Verify(r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionWasRevoked_AllowsReGrant()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);

        // A revoked grant is soft-deleted and therefore absent from the active set.
        SetUpExistingActions(apiKeyId);

        await _sut.ExecuteAsync(apiKeyId, ApiKeyActionEnum.Write, CREATED_BY);

        _actionRepo.Verify(
            r => r.AddAsync(
                It.Is<ApiKeyAction>(a => a.Action == ApiKeyActionEnum.Write && a.DeletedAt == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyDoesNotExist_ThrowsNotFoundException()
    {
        Guid apiKeyId = Guid.NewGuid();
        _apiKeyRepo
            .Setup(r => r.GetByIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(apiKeyId, ApiKeyActionEnum.Write, CREATED_BY);

        await act.Should().ThrowAsync<NotFoundException>();
        _actionRepo.Verify(r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetUpApiKey(Guid apiKeyId)
    {
        _apiKeyRepo
            .Setup(r => r.GetByIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiKeyTestData.BuildApiKey(apiKeyId));
    }

    private void SetUpExistingActions(Guid apiKeyId, params ApiKeyAction[] actions)
    {
        _actionRepo
            .Setup(r => r.GetActiveByApiKeyIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions.ToList());
    }
}
