# Integration Tests Reference

Integration tests verify that a slice of the real application works together — typically
a service or repository wired to a real database running in a Docker container via Testcontainers.
No mocks for persistence; the full EF Core pipeline runs against a real engine.

---

## When to write an integration test (not a unit test)

- Testing an EF Core repository — queries, includes, filters, ordering.
- Testing a complex LINQ query that must produce the correct SQL.
- Testing EF Core migrations produce the expected schema.
- Testing a service that has non-trivial transactional behavior.
- Testing a DbContext configuration (owned types, value converters, shadow properties).

---

## Database fixture with Testcontainers

A `DatabaseFixture` boots a real PostgreSQL (or SQL Server) container once per test collection,
runs migrations, and provides a clean connection string.

```csharp
// tests/MyApp.IntegrationTests/Fixtures/DatabaseFixture.cs
namespace MyApp.IntegrationTests.Fixtures;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("testdb")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await MigrateAsync();
    }

    public async Task DisposeAsync() =>
        await _container.DisposeAsync();

    private async Task MigrateAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }
}
```

```csharp
// tests/MyApp.IntegrationTests/Fixtures/DatabaseFixtureCollection.cs
namespace MyApp.IntegrationTests.Fixtures;

[CollectionDefinition(nameof(DatabaseFixtureCollection))]
public sealed class DatabaseFixtureCollection : ICollectionFixture<DatabaseFixture>;
```

---

## Base class for integration tests

Provides a fresh `DbContext` per test and handles cleanup.

```csharp
// tests/MyApp.IntegrationTests/IntegrationTestBase.cs
namespace MyApp.IntegrationTests;

[Collection(nameof(DatabaseFixtureCollection))]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    protected AppDbContext Db { get; private set; } = null!;

    protected IntegrationTestBase(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        Db = new AppDbContext(options);

        // Clean all tables before each test for isolation
        await CleanDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
    }

    private async Task CleanDatabaseAsync()
    {
        // Truncate all tables in dependency order — adjust to your schema
        await Db.Database.ExecuteSqlRawAsync("""
            TRUNCATE TABLE "OrderLines", "Orders", "Products", "Customers" RESTART IDENTITY CASCADE;
            """);
    }
}
```

---

## Repository integration test

```csharp
namespace MyApp.IntegrationTests.Repositories;

public sealed class OrderRepositoryTests(DatabaseFixture fixture)
    : IntegrationTestBase(fixture)
{
    private OrderRepository CreateSut() =>
        new(Db, Mock.Of<ILogger<OrderRepository>>());

    [Fact]
    public async Task GetByIdAsync_WhenOrderExists_ReturnsOrderWithLines()
    {
        // Arrange
        var customer = new Customer { Id = Guid.NewGuid(), Name = "Alice" };
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Status = OrderStatus.Pending,
            Lines = [new OrderLine { ProductId = Guid.NewGuid(), Quantity = 3 }]
        };

        Db.Customers.Add(customer);
        Db.Orders.Add(order);
        await Db.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Lines.Should().HaveCount(1);
        result.Lines[0].Quantity.Should().Be(3);
    }

    [Fact]
    public async Task GetByCustomerAsync_WhenMultipleOrders_ReturnsOnlyMatchingOrders()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();

        Db.Orders.AddRange(
            new Order { Id = Guid.NewGuid(), CustomerId = customerId, Status = OrderStatus.Pending },
            new Order { Id = Guid.NewGuid(), CustomerId = customerId, Status = OrderStatus.Confirmed },
            new Order { Id = Guid.NewGuid(), CustomerId = otherCustomerId, Status = OrderStatus.Pending });

        await Db.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var results = await sut.GetByCustomerAsync(customerId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(o => o.CustomerId.Should().Be(customerId));
    }

    [Fact]
    public async Task AddAsync_WhenCalled_PersistsOrderToDatabase()
    {
        // Arrange
        var order = new Order { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid() };
        var sut = CreateSut();

        // Act
        await sut.AddAsync(order);

        // Assert — query via a fresh context to avoid first-level cache false positives
        await using var verifyDb = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(/* connection */).Options);
        var persisted = await verifyDb.Orders.FindAsync(order.Id);

        persisted.Should().NotBeNull();
        persisted!.CustomerId.Should().Be(order.CustomerId);
    }
}
```

---

## Using DataSeeders in integration tests

See `references/data-seeders.md` for the full seeder pattern.
Quick usage in an integration test:

```csharp
[Fact]
public async Task GetPendingOrdersAsync_WhenMixedStatuses_ReturnsOnlyPendingOrders()
{
    // Arrange — use fluent seeders for readable, maintainable test data
    var customer = await new CustomerSeeder(Db).SeedAsync();

    await new OrderSeeder(Db)
        .WithCustomer(customer)
        .WithStatus(OrderStatus.Pending)
        .SeedAsync();

    await new OrderSeeder(Db)
        .WithCustomer(customer)
        .WithStatus(OrderStatus.Confirmed)
        .SeedAsync();

    // Act
    var results = await CreateSut().GetPendingAsync();

    // Assert
    results.Should().HaveCount(1);
    results[0].Status.Should().Be(OrderStatus.Pending);
}
```
