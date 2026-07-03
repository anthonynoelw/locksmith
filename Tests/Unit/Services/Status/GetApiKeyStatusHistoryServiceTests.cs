namespace Unit.Services.Status;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Services.Status;
using Domain.Enums;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class GetApiKeyStatusHistoryServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyStatusRepository> _statusRepo;
    private readonly GetApiKeyStatusHistoryService _sut;

    public GetApiKeyStatusHistoryServiceTests()
    {
        _statusRepo = new Mock<IApiKeyStatusRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeyStatuses).Returns(_statusRepo.Object);

        _sut = new GetApiKeyStatusHistoryService(_unitOfWork.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHistoryExists_ReturnsAllStatuses()
    {
        Guid apiKeyId = Guid.NewGuid();
        IReadOnlyList<ApiKeyStatus> history = new List<ApiKeyStatus>
        {
            BuildStatus(apiKeyId, ApiKeyStatusEnum.Inactive),
            BuildStatus(apiKeyId, ApiKeyStatusEnum.Active),
        };
        _statusRepo
            .Setup(r => r.GetByApiKeyIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        IReadOnlyList<ApiKeyStatus> result = await _sut.ExecuteAsync(apiKeyId);

        result.Should().BeEquivalentTo(history);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoHistoryExists_ReturnsEmptyList()
    {
        Guid apiKeyId = Guid.NewGuid();
        _statusRepo
            .Setup(r => r.GetByApiKeyIdAsync(apiKeyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKeyStatus>());

        IReadOnlyList<ApiKeyStatus> result = await _sut.ExecuteAsync(apiKeyId);

        result.Should().BeEmpty();
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
