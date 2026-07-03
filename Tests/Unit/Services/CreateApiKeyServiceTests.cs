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
    public async Task ExecuteAsync_ReturnsNonEmptyApiKeyId()
    {
        CreateApiKeyCommand command = BuildCommand();

        CreateApiKeyResult result = await _sut.ExecuteAsync(command);

        result.ApiKeyId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsIdempotencyKeyFromCryptoService()
    {
        CreateApiKeyCommand command = BuildCommand();

        CreateApiKeyResult result = await _sut.ExecuteAsync(command);

        result.IdempotencyKey.Should().Be("test-idempotency-key");
    }

    [Fact]
    public async Task ExecuteAsync_PlaintextSecretStartsWithLkPrefix()
    {
        CreateApiKeyCommand command = BuildCommand();

        CreateApiKeyResult result = await _sut.ExecuteAsync(command);

        result.PlaintextSecret.Should().StartWith("lk_");
    }

    [Fact]
    public async Task ExecuteAsync_CallsGenerateApiKeySecret()
    {
        CreateApiKeyCommand command = BuildCommand();

        await _sut.ExecuteAsync(command);

        _cryptoService.Verify(c => c.GenerateApiKeySecret(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SetsSecretHashOnApiKey()
    {
        CreateApiKeyCommand command = BuildCommand();
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.ExecuteAsync(command);

        captured!.SecretHash.Should().Be("test-hash");
    }

    [Fact]
    public async Task ExecuteAsync_SetsEncryptedSecretOnApiKey()
    {
        CreateApiKeyCommand command = BuildCommand();
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.ExecuteAsync(command);

        captured!.Secret.Should().Be("encrypted");
    }

    [Fact]
    public async Task ExecuteAsync_CallsAddAsyncOnApiKeyRepositoryOnce()
    {
        CreateApiKeyCommand command = BuildCommand();

        await _sut.ExecuteAsync(command);

        _apiKeyRepo.Verify(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AddsIdempotencyKeyToRepository()
    {
        CreateApiKeyCommand command = BuildCommand();

        await _sut.ExecuteAsync(command);

        _idempotencyKeyRepo.Verify(
            r => r.AddAsync(
                It.Is<IdempotencyKey>(k => k.IdempotencyKeyHash == "test-hash"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LinksIdempotencyKeyToCreatedApiKey()
    {
        CreateApiKeyCommand command = BuildCommand();
        ApiKey? capturedApiKey = null;
        IdempotencyKey? capturedIdempotencyKey = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => capturedApiKey = key);
        _idempotencyKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<IdempotencyKey>(), It.IsAny<CancellationToken>()))
            .Callback<IdempotencyKey, CancellationToken>((key, _) => capturedIdempotencyKey = key);

        await _sut.ExecuteAsync(command);

        capturedIdempotencyKey!.ApiKeyId.Should().Be(capturedApiKey!.Id);
    }

    [Fact]
    public async Task ExecuteAsync_AddsApiKeyStatusAsInactive()
    {
        CreateApiKeyCommand command = BuildCommand();

        await _sut.ExecuteAsync(command);

        _statusRepo.Verify(
            r => r.AddAsync(
                It.Is<ApiKeyStatus>(s => s.Status == ApiKeyStatusEnum.Inactive),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_LinksApiKeyStatusToCreatedApiKey()
    {
        CreateApiKeyCommand command = BuildCommand();
        ApiKey? capturedApiKey = null;
        ApiKeyStatus? capturedStatus = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => capturedApiKey = key);
        _statusRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKeyStatus>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKeyStatus, CancellationToken>((status, _) => capturedStatus = status);

        await _sut.ExecuteAsync(command);

        capturedStatus!.ApiKeyId.Should().Be(capturedApiKey!.Id);
    }

    [Fact]
    public async Task ExecuteAsync_CallsSaveChangesOnce()
    {
        CreateApiKeyCommand command = BuildCommand();

        await _sut.ExecuteAsync(command);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionsProvided_AddsEachAction()
    {
        CreateApiKeyCommand command = BuildCommand(actions: new List<ApiKeyActionEnum> { ApiKeyActionEnum.Read, ApiKeyActionEnum.Write });

        await _sut.ExecuteAsync(command);

        _actionRepo.Verify(
            r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoActions_DoesNotAddAnyActions()
    {
        CreateApiKeyCommand command = BuildCommand();

        await _sut.ExecuteAsync(command);

        _actionRepo.Verify(
            r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActionsProvided_LinksEachActionToCreatedApiKey()
    {
        CreateApiKeyCommand command = BuildCommand(actions: new List<ApiKeyActionEnum> { ApiKeyActionEnum.Read });
        ApiKey? capturedApiKey = null;
        ApiKeyAction? capturedAction = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => capturedApiKey = key);
        _actionRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKeyAction>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKeyAction, CancellationToken>((action, _) => capturedAction = action);

        await _sut.ExecuteAsync(command);

        capturedAction!.ApiKeyId.Should().Be(capturedApiKey!.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiresAtInPast_ThrowsValidationException()
    {
        CreateApiKeyCommand command = BuildCommand(expiresAt: DateTime.UtcNow.AddDays(-1));

        Func<Task> act = () => _sut.ExecuteAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Validation failed.");
    }

    [Fact]
    public async Task ExecuteAsync_ExpiresAtEqualsNow_ThrowsValidationException()
    {
        CreateApiKeyCommand command = BuildCommand(expiresAt: DateTime.UtcNow);

        Func<Task> act = () => _sut.ExecuteAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Validation failed.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenExpiresAtProvided_StoresProvidedExpiry()
    {
        DateTime expiry = DateTime.UtcNow.AddDays(60);
        CreateApiKeyCommand command = BuildCommand(expiresAt: expiry);
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.ExecuteAsync(command);

        captured!.ExpiresAt.Should().BeCloseTo(expiry, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_WhenExpiresAtOmitted_DefaultsToThirtyDaysFromNow()
    {
        CreateApiKeyCommand command = BuildCommand();
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.ExecuteAsync(command);

        captured!.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExecuteAsync_SetsCreatedAtCloseToNow()
    {
        CreateApiKeyCommand command = BuildCommand();
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.ExecuteAsync(command);

        captured!.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExecuteAsync_SetsCreatedByFromCommand()
    {
        CreateApiKeyCommand command = BuildCommand(createdBy: "my-service");
        ApiKey? captured = null;
        _apiKeyRepo
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => captured = key);

        await _sut.ExecuteAsync(command);

        captured!.CreatedBy.Should().Be("my-service");
    }

    private static CreateApiKeyCommand BuildCommand(
        string createdBy = "caller",
        DateTime? expiresAt = null,
        List<ApiKeyActionEnum>? actions = null) =>
        new (createdBy, expiresAt, actions ?? new List<ApiKeyActionEnum>());
}
