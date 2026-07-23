namespace Unit.Requests;

using System.Text.Json;
using Api.Requests;
using Domain.Enums;
using FluentAssertions;

public sealed class RateLimitCredentialContractTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new (JsonSerializerDefaults.Web);

    public static TheoryData<IRateLimitCredential> CredentialRequests => new ()
    {
        new UpdateApiKeyRequest { IdempotencyKey = "idem-key" },
        new RetrieveSecretRequest { IdempotencyKey = "idem-key" },
        new UpdateApiKeyActionsRequest { IdempotencyKey = "idem-key" },
        new GrantApiKeyActionRequest { IdempotencyKey = "idem-key" },
        new RevokeApiKeyActionRequest { IdempotencyKey = "idem-key" },
        new UpdateApiKeyStatusRequest { IdempotencyKey = "idem-key", Status = ApiKeyStatusEnum.Active },
        new ValidateApiKeySecretRequest { Secret = "lk_test-secret" },
    };

    [Theory]
    [MemberData(nameof(CredentialRequests))]
    public void Serialize_WhenRequestImplementsIRateLimitCredential_DoesNotExposeRateLimitCredentialProperty(
        IRateLimitCredential request)
    {
        string json = JsonSerializer.Serialize(request, request.GetType(), _jsonOptions);

        json.Should().NotContain(
            "ateLimitCredential",
            "the explicit interface implementation must stay out of the public request/OpenAPI contract");
    }
}
