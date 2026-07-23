namespace Unit.Services;

using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Domain.Exceptions;
using Domain.Models;
using FluentAssertions;
using Moq;

public sealed class GetApiKeyBySecretServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyRepository> _apiKeyRepo;
    private readonly Mock<ICryptoService> _cryptoService;
    private readonly GetApiKeyBySecretService _sut;

    public GetApiKeyBySecretServiceTests()
    {
        _apiKeyRepo = new Mock<IApiKeyRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeys).Returns(_apiKeyRepo.Object);

        _cryptoService = new Mock<ICryptoService>();
        _cryptoService.Setup(c => c.HashForLookup(It.IsAny<string>())).Returns<string>(s => $"hash:{s}");

        _sut = new GetApiKeyBySecretService(_unitOfWork.Object, _cryptoService.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithKnownSecret_ReturnsApiKeyId()
    {
        Guid apiKeyId = Guid.NewGuid();
        ApiKey apiKey = ApiKeyTestData.BuildApiKey(apiKeyId);
        _apiKeyRepo
            .Setup(r => r.GetBySecretHashAsync("hash:lk_secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiKey);

        Guid result = await _sut.ExecuteAsync("lk_secret");

        result.Should().Be(apiKeyId);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownSecret_ThrowsNotFoundException()
    {
        _apiKeyRepo
            .Setup(r => r.GetBySecretHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        Func<Task> act = () => _sut.ExecuteAsync("lk_unknown");

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
