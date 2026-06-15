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

public sealed class CreateApiKeyServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Mock<IApiKeyRepository> _apiKeyRepo;
    private readonly Mock<IApiKeyStatusRepository> _statusRepo;
    private readonly Mock<IApiKeyActionRepository> _actionRepo;
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepo;
    private readonly Mock<ICryptoService> _cryptoService;
    private readonly CreateApiKeyService _sut;

    public CreateApiKeyServiceTests()
    {
        _apiKeyRepo = new Mock<IApiKeyRepository>();
        _statusRepo = new Mock<IApiKeyStatusRepository>();
        _actionRepo = new Mock<IApiKeyActionRepository>();
        _idempotencyKeyRepo = new Mock<IIdempotencyKeyRepository>();

        _unitOfWork = new Mock<IUnitOfWork>();
        _unitOfWork.Setup(u => u.ApiKeys).Returns(_apiKeyRepo.Object);
        _unitOfWork.Setup(u => u.ApiKeyStatuses).Returns(_statusRepo.Object);
        _unitOfWork.Setup(u => u.ApiKeyActions).Returns(_actionRepo.Object);
        _unitOfWork.Setup(u => u.IdempotencyKeys).Returns(_idempotencyKeyRepo.Object);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _cryptoService = new Mock<ICryptoService>();
        _cryptoService.Setup(c => c.GenerateIdempotencyKey()).Returns("test-idempotency-key");
        _cryptoService.Setup(c => c.GenerateApiKeySecret()).Returns("lk_test-secret");
        _cryptoService.Setup(c => c.HashForLookup(It.IsAny<string>())).Returns("test-hash");
        _cryptoService.Setup(c => c.DeriveEncryptionKey(It.IsAny<string>(), It.IsAny<byte[]>())).Returns(new byte[32]);
        _cryptoService.Setup(c => c.Encrypt(It.IsAny<string>(), It.IsAny<byte[]>())).Returns("encrypted");

        _sut = new CreateApiKeyService(_unitOfWork.Object, _cryptoService.Object);
    }

    [Fact]
    public async Task Execute_ReturnsNonEmptyApiKeyId()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());

        CreateApiKeyResult result = await _sut.Execute(command);

        result.ApiKeyId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Execute_ReturnsIdempotencyKeyFromCryptoService()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());

        CreateApiKeyResult result = await _sut.Execute(command);

        result.IdempotencyKey.Should().Be("test-idempotency-key");
    }

    [Fact]
    public async Task Execute_PlaintextSecretStartsWithLkPrefix()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());

        CreateApiKeyResult result = await _sut.Execute(command);

        result.PlaintextSecret.Should().StartWith("lk_");
    }

    [Fact]
    public async Task Execute_CallsGenerateApiKeySecret()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());

        await _sut.Execute(command);

        _cryptoService.Verify(c => c.GenerateApiKeySecret(), Times.Once);
    }

    [Fact]
    public async Task Execute_SetsSecretHashOnApiKey()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.Execute(command);

        captured!.SecretHash.Should().Be("test-hash");
    }

    [Fact]
    public async Task Execute_AddsIdempotencyKeyToRepository()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());

        await _sut.Execute(command);

        _idempotencyKeyRepo.Verify(
            r => r.AddAsync(
                It.Is<IdempotencyKey>(k => k.IdempotencyKeyHash == "test-hash"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_AddsApiKeyStatusAsInactive()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());

        await _sut.Execute(command);

        _statusRepo.Verify(
            r => r.AddAsync(
                It.Is<ApiKeyStatus>(s => s.Status == ApiKeyStatusEnum.Inactive),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_CallsSaveChangesOnce()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());

        await _sut.Execute(command);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenActionsProvided_AddsEachAction()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand(
            "caller",
            null,
            new List<ApiKeyActionEnum> { ApiKeyActionEnum.Read, ApiKeyActionEnum.Write });

        await _sut.Execute(command);

        _actionRepo.Verify(
            r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_WhenNoActions_DoesNotAddAnyActions()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());

        await _sut.Execute(command);

        _actionRepo.Verify(
            r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Execute_ExpiresAtInPast_ThrowsValidationException()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", DateTime.UtcNow.AddDays(-1), new List<ApiKeyActionEnum>());

        Func<Task> act = () => _sut.Execute(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Validation failed.");
    }

    [Fact]
    public async Task Execute_WhenExpiresAtProvided_StoresProvidedExpiry()
    {
        DateTime expiry = DateTime.UtcNow.AddDays(60);
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", expiry, new List<ApiKeyActionEnum>());
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.Execute(command);

        captured!.ExpiresAt.Should().BeCloseTo(expiry, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Execute_WhenExpiresAtOmitted_DefaultsToThirtyDaysFromNow()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("caller", null, new List<ApiKeyActionEnum>());
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.Execute(command);

        captured!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Execute_SetsCreatedByFromCommand()
    {
        CreateApiKeyCommand command = new CreateApiKeyCommand("my-service", null, new List<ApiKeyActionEnum>());
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.Execute(command);

        captured!.CreatedBy.Should().Be("my-service");
    }
}
