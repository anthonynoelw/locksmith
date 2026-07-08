namespace Unit.Services.Actions;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Services.Actions;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class ListApiKeyActionsServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyRepository> _apiKeyRepo;
    private readonly Mock<IApiKeyActionRepository> _actionRepo;
    private readonly ListApiKeyActionsService _sut;

    public ListApiKeyActionsServiceTests()
    {
        _apiKeyRepo = new Mock<IApiKeyRepository>();
        _actionRepo = new Mock<IApiKeyActionRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeys).Returns(_apiKeyRepo.Object);
        _unitOfWork.Setup(u => u.ApiKeyActions).Returns(_actionRepo.Object);

        _sut = new ListApiKeyActionsService(_unitOfWork.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyExists_ReturnsActiveActions()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        _actionRepo
            .Setup(r => r.GetActiveByApiKeyIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKeyAction>
            {
                ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Read),
                ActionsTestData.BuildAction(apiKeyId, ApiKeyActionEnum.Write),
            });

        IReadOnlyList<ApiKeyAction> result = await _sut.ExecuteAsync(apiKeyId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoActionsExist_ReturnsEmptyList()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        _actionRepo
            .Setup(r => r.GetActiveByApiKeyIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKeyAction>());

        IReadOnlyList<ApiKeyAction> result = await _sut.ExecuteAsync(apiKeyId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyDoesNotExist_ThrowsNotFoundException()
    {
        Guid apiKeyId = Guid.NewGuid();
        _apiKeyRepo
            .Setup(r => r.GetByIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(apiKeyId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private void SetUpApiKey(Guid apiKeyId)
    {
        _apiKeyRepo
            .Setup(r => r.GetByIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiKeyTestData.BuildApiKey(apiKeyId));
    }
}
