# Application Tests Reference

Application tests boot the **entire application** using `WebApplicationFactory<TProgram>`.
They exercise the real HTTP pipeline, real DI graph, real middleware, and a real database
(Testcontainers). Third-party HTTP APIs are stubbed with WireMock.Net.

These are the highest-confidence tests — they verify that the system actually works
end-to-end, not just in isolation. They are also the slowest; write them for
critical user-facing flows, not for every edge case.

---

## ApplicationFixture

The fixture boots the application once per test collection, starts a real database,
and keeps the `WebApplicationFactory` alive for the lifetime of the collection.

```csharp
// tests/MyApp.ApplicationTests/Fixtures/ApplicationFixture.cs
namespace MyApp.ApplicationTests.Fixtures;

public sealed class ApplicationFixture : IAsyncLifetime
{
    // --- database ---
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("apptest")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    // --- wiremock for stubbing external HTTP calls ---
    public WireMockServer ExternalApiServer { get; private set; } = null!;

    // --- the application under test ---
    public ApplicationTestFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        ExternalApiServer = WireMockServer.Start();

        Factory = new ApplicationTestFactory(
            _dbContainer.GetConnectionString(),
            ExternalApiServer.Url!);

        // Trigger WebApplicationFactory initialization and run migrations
        _ = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Factory.Dispose();
        ExternalApiServer.Stop();
        await _dbContainer.DisposeAsync();
    }
}
```

```csharp
// tests/MyApp.ApplicationTests/Fixtures/ApplicationFixtureCollection.cs
namespace MyApp.ApplicationTests.Fixtures;

[CollectionDefinition(nameof(ApplicationFixtureCollection))]
public sealed class ApplicationFixtureCollection
    : ICollectionFixture<ApplicationFixture>;
```

---

## ApplicationTestFactory

Overrides the real application's configuration to point at the test database
and WireMock stubs instead of real external services.

```csharp
// tests/MyApp.ApplicationTests/Fixtures/ApplicationTestFactory.cs
namespace MyApp.ApplicationTests.Fixtures;

public sealed class ApplicationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _externalApiBaseUrl;

    public ApplicationTestFactory(string connectionString, string externalApiBaseUrl)
    {
        _connectionString = connectionString;
        _externalApiBaseUrl = externalApiBaseUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Override any appsettings values for the test run
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["ExternalApi:BaseUrl"]        = _externalApiBaseUrl,
                ["FeatureFlags:NewCheckout"]   = "true",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace any service registrations that should behave differently in tests.
            // Example: replace a real email sender with a no-op fake.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, FakeEmailSender>();

            // Replace time provider to make time-dependent tests deterministic
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(
                DateTimeOffset.Parse("2024-06-15T10:00:00Z")));
        });
    }

    /// <summary>Creates an authenticated HTTP client for a given user identity.</summary>
    public HttpClient CreateAuthenticatedClient(Guid userId, string role = "User")
    {
        var client = CreateClient();
        var token = JwtTestHelper.GenerateToken(userId, role);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Opens a scoped DbContext connected to the test database.</summary>
    public AsyncServiceScope CreateScope() =>
        Services.CreateAsyncScope();
}
```

---

## Base class for application tests

Provides per-test database cleanup, a scoped DbContext for seeding,
and typed HTTP client helpers.

```csharp
// tests/MyApp.ApplicationTests/ApplicationTestBase.cs
namespace MyApp.ApplicationTests;

[Collection(nameof(ApplicationFixtureCollection))]
public abstract class ApplicationTestBase : IAsyncLifetime
{
    protected ApplicationFixture Fixture { get; }
    protected HttpClient Client { get; private set; } = null!;
    protected AppDbContext Db { get; private set; } = null!;

    private AsyncServiceScope _scope;

    protected ApplicationTestBase(ApplicationFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Create a fresh DI scope (and DbContext) per test
        _scope = Fixture.Factory.CreateScope();
        Db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Unauthenticated client by default; override per-test as needed
        Client = Fixture.Factory.CreateClient();

        await CleanDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
        Client.Dispose();
    }

    /// <summary>
    /// Replace <see cref="Client"/> with one that carries a JWT for the given user.
    /// Call this in the Arrange section of tests that require authentication.
    /// </summary>
    protected void AuthenticateAs(Guid userId, string role = "User")
    {
        Client.Dispose();
        Client = Fixture.Factory.CreateAuthenticatedClient(userId, role);
    }

    private async Task CleanDatabaseAsync()
    {
        await Db.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE "OrderLines", "Orders", "Products", "Customers" RESTART IDENTITY CASCADE;
            """);
    }
}
```

---

## Writing application tests

```csharp
namespace MyApp.ApplicationTests.Orders;

public sealed class CreateOrderTests(ApplicationFixture fixture)
    : ApplicationTestBase(fixture)
{
    [Fact]
    public async Task POST_Orders_WhenRequestIsValid_Returns201WithCreatedOrder()
    {
        // Arrange
        var customer = await new CustomerSeeder(Db).SeedAsync();
        var product  = await new ProductSeeder(Db).WithStock(10).SeedAsync();

        AuthenticateAs(customer.UserId);

        var request = new
        {
            customerId = customer.Id,
            lines = new[] { new { productId = product.Id, quantity = 2 } }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<OrderDto>();
        body.Should().NotBeNull();
        body!.CustomerId.Should().Be(customer.Id);
        body.Lines.Should().HaveCount(1);
        body.Lines[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task POST_Orders_WhenUnauthenticated_Returns401()
    {
        // Arrange — no AuthenticateAs call; Client is anonymous
        var request = new { customerId = Guid.NewGuid(), lines = Array.Empty<object>() };

        // Act
        var response = await Client.PostAsJsonAsync("/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_Orders_WhenStockInsufficient_Returns422()
    {
        // Arrange
        var customer = await new CustomerSeeder(Db).SeedAsync();
        var product  = await new ProductSeeder(Db).WithStock(1).SeedAsync();

        AuthenticateAs(customer.UserId);

        var request = new
        {
            customerId = customer.Id,
            lines = new[] { new { productId = product.Id, quantity = 999 } }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
```

---

## Stubbing external HTTP calls with WireMock

When your application calls an external API, stub it on the WireMock server
rather than letting it hit the real endpoint.

```csharp
[Fact]
public async Task POST_Orders_WhenPaymentGatewayApproves_Returns201()
{
    // Arrange
    var customer = await new CustomerSeeder(Db).SeedAsync();
    AuthenticateAs(customer.UserId);

    // Stub the external payment gateway on the WireMock server
    Fixture.ExternalApiServer
        .Given(Request.Create()
            .WithPath("/payments/authorize")
            .UsingPost())
        .RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithBodyAsJson(new { approved = true, transactionId = Guid.NewGuid() }));

    var request = BuildValidOrderRequest(customer.Id);

    // Act
    var response = await Client.PostAsJsonAsync("/orders", request);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

---

## JWT helper for test tokens

```csharp
// tests/MyApp.ApplicationTests/Helpers/JwtTestHelper.cs
namespace MyApp.ApplicationTests.Helpers;

public static class JwtTestHelper
{
    // Must match the key in appsettings.Testing.json
    private const string TestSigningKey = "this-is-a-test-secret-key-min-32-chars!!";

    public static string GenerateToken(Guid userId, string role = "User")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        };

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

---

## FakeEmailSender (test double for IEmailSender)

```csharp
// tests/MyApp.ApplicationTests/Fakes/FakeEmailSender.cs
namespace MyApp.ApplicationTests.Fakes;

public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> SentEmails { get; } = [];

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        SentEmails.Add((to, subject, body));
        return Task.CompletedTask;
    }
}
```

Resolve it from the DI scope in tests that need to assert on sent emails:

```csharp
var emailSender = (FakeEmailSender)_scope.ServiceProvider.GetRequiredService<IEmailSender>();
emailSender.SentEmails.Should().ContainSingle(e => e.To == customer.Email);
```
