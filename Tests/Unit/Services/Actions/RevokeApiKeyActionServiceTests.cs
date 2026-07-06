namespace Unit.Services.Actions;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Services.Actions;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class RevokeApiKeyActionServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyRepository> _apiKeyRepo;
    private readonly Mock<IApiKeyActionRepository> _actionRepo;
    private readonly RevokeApiKeyActionService _sut;

    public RevokeApiKeyActionServiceTests()
    {
        _apiKeyRepo = new Mock<IApiKeyRepository>();
        _actionRepo = new Mock<IApiKeyActionRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeys).Returns(_apiKeyRepo.Object);
        _unitOfWork.Setup(u => u.ApiKeyActions).Returns(_actionRepo.Object);

        _sut = new RevokeApiKeyActionService(_unitOfWork.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionGranted_RemovesAction()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        _actionRepo
            .Setup(r => r.RemoveAsync(apiKeyId, ApiKeyActionEnum.Write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ExecuteAsync(apiKeyId, ApiKeyActionEnum.Write);

        _actionRepo.Verify(
            r => r.RemoveAsync(apiKeyId, ApiKeyActionEnum.Write, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionNotGranted_ThrowsNotFoundException()
    {
        Guid apiKeyId = Guid.NewGuid();
        SetUpApiKey(apiKeyId);
        _actionRepo
            .Setup(r => r.RemoveAsync(apiKeyId, ApiKeyActionEnum.Write, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Func<Task> act = () => _sut.ExecuteAsync(apiKeyId, ApiKeyActionEnum.Write);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyDoesNotExist_ThrowsNotFoundException()
    {
        Guid apiKeyId = Guid.NewGuid();
        _apiKeyRepo
            .Setup(r => r.GetByIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync(apiKeyId, ApiKeyActionEnum.Write);

        await act.Should().ThrowAsync<NotFoundException>();
        _actionRepo.Verify(
            r => r.RemoveAsync(It.IsAny<Guid>(), It.IsAny<ApiKeyActionEnum>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetUpApiKey(Guid apiKeyId)
    {
        _apiKeyRepo
            .Setup(r => r.GetByIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiKeyTestData.BuildApiKey(apiKeyId));
    }
}
