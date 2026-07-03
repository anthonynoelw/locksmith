namespace Unit.Services.Status;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Services.Status;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class GetApiKeyStatusServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyStatusRepository> _statusRepo;
    private readonly GetApiKeyStatusService _sut;

    public GetApiKeyStatusServiceTests()
    {
        _statusRepo = new Mock<IApiKeyStatusRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeyStatuses).Returns(_statusRepo.Object);

        _sut = new GetApiKeyStatusService(_unitOfWork.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStatusExists_ReturnsStatus()
    {
        Guid apiKeyId = Guid.NewGuid();
        ApiKeyStatus status = BuildStatus(apiKeyId, ApiKeyStatusEnum.Active);
        _statusRepo
            .Setup(r => r.GetCurrentStatusAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        ApiKeyStatus result = await _sut.ExecuteAsync(apiKeyId);

        result.Should().Be(status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoStatusExists_ThrowsNotFoundException()
    {
        Guid apiKeyId = Guid.NewGuid();
        _statusRepo
            .Setup(r => r.GetCurrentStatusAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKeyStatus?)null);

        Func<Task> act = () => _sut.ExecuteAsync(apiKeyId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static ApiKeyStatus BuildStatus(Guid apiKeyId, ApiKeyStatusEnum status) =>
        new ()
        {
            Id = Guid.NewGuid(),
            ApiKeyId = apiKeyId,
            Status = status,
            ApiKey = new ApiKey
            {
                Id = apiKeyId,
                Secret = "encrypted",
                SecretHash = "hash",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "caller",
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Statuses = new List<ApiKeyStatus>(),
                Actions = new List<ApiKeyAction>(),
            },
        };
}
