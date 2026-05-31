# DataSeeders Reference

DataSeeders provide a fluent, expressive API for creating test data directly in the database.
They replace raw object construction in test Arrange sections with readable, maintainable
builder-style calls that reveal intent and hide irrelevant defaults.

---

## The pattern

A seeder is a class that:
1. Holds a `DbContext`.
2. Exposes fluent `With*` methods to override specific properties.
3. Exposes a `SeedAsync()` method that persists and returns the entity.
4. Provides sensible defaults for every property so callers only specify what matters.

---

## Base seeder (optional but recommended)

```csharp
// tests/MyApp.ApplicationTests/DataSeeders/SeederBase.cs
namespace MyApp.ApplicationTests.DataSeeders;

public abstract class SeederBase<TEntity, TSeeder>
    where TEntity : class
    where TSeeder : SeederBase<TEntity, TSeeder>
{
    protected readonly AppDbContext Db;

    protected SeederBase(AppDbContext db)
    {
        Db = db;
    }

    protected abstract TEntity Build();

    public async Task<TEntity> SeedAsync(CancellationToken ct = default)
    {
        var entity = Build();
        Db.Set<TEntity>().Add(entity);
        await Db.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>Seed multiple entities with the same configuration.</summary>
    public async Task<IReadOnlyList<TEntity>> SeedManyAsync(int count, CancellationToken ct = default)
    {
        var entities = Enumerable.Range(0, count).Select(_ => Build()).ToList();
        Db.Set<TEntity>().AddRange(entities);
        await Db.SaveChangesAsync(ct);
        return entities;
    }
}
```

---

## CustomerSeeder

```csharp
// tests/MyApp.ApplicationTests/DataSeeders/CustomerSeeder.cs
namespace MyApp.ApplicationTests.DataSeeders;

public sealed class CustomerSeeder : SeederBase<Customer, CustomerSeeder>
{
    private Guid   _id     = Guid.NewGuid();
    private string _name   = "Test Customer";
    private string _email  = $"customer-{Guid.NewGuid():N}@test.com";
    private Guid   _userId = Guid.NewGuid();
    private bool   _isActive = true;

    public CustomerSeeder(AppDbContext db) : base(db) { }

    public CustomerSeeder WithId(Guid id)           { _id = id;           return this; }
    public CustomerSeeder WithName(string name)      { _name = name;       return this; }
    public CustomerSeeder WithEmail(string email)    { _email = email;     return this; }
    public CustomerSeeder WithUserId(Guid userId)    { _userId = userId;   return this; }
    public CustomerSeeder AsInactive()               { _isActive = false;  return this; }

    protected override Customer Build() => new()
    {
        Id       = _id,
        Name     = _name,
        Email    = _email,
        UserId   = _userId,
        IsActive = _isActive,
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
```

---

## ProductSeeder

```csharp
// tests/MyApp.ApplicationTests/DataSeeders/ProductSeeder.cs
namespace MyApp.ApplicationTests.DataSeeders;

public sealed class ProductSeeder : SeederBase<Product, ProductSeeder>
{
    private Guid    _id    = Guid.NewGuid();
    private string  _name  = "Test Product";
    private decimal _price = 9.99m;
    private int     _stock = 100;

    public ProductSeeder(AppDbContext db) : base(db) { }

    public ProductSeeder WithId(Guid id)        { _id = id;       return this; }
    public ProductSeeder WithName(string name)   { _name = name;   return this; }
    public ProductSeeder WithPrice(decimal price){ _price = price; return this; }
    public ProductSeeder WithStock(int stock)    { _stock = stock; return this; }
    public ProductSeeder OutOfStock()            { _stock = 0;     return this; }

    protected override Product Build() => new()
    {
        Id        = _id,
        Name      = _name,
        Price     = _price,
        Stock     = _stock,
        CreatedAt = DateTimeOffset.UtcNow,
    };
}
```

---

## OrderSeeder

```csharp
// tests/MyApp.ApplicationTests/DataSeeders/OrderSeeder.cs
namespace MyApp.ApplicationTests.DataSeeders;

public sealed class OrderSeeder : SeederBase<Order, OrderSeeder>
{
    private Guid        _id          = Guid.NewGuid();
    private Guid?       _customerId  = null;
    private OrderStatus _status      = OrderStatus.Pending;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private List<OrderLineConfig> _lines = [];

    private record OrderLineConfig(Guid ProductId, int Quantity, decimal UnitPrice);

    public OrderSeeder(AppDbContext db) : base(db) { }

    public OrderSeeder WithId(Guid id)                 { _id = id;               return this; }
    public OrderSeeder WithCustomer(Customer customer)  { _customerId = customer.Id; return this; }
    public OrderSeeder WithCustomerId(Guid customerId)  { _customerId = customerId;  return this; }
    public OrderSeeder WithStatus(OrderStatus status)   { _status = status;         return this; }
    public OrderSeeder CreatedAt(DateTimeOffset at)     { _createdAt = at;          return this; }

    public OrderSeeder WithLine(Guid productId, int quantity = 1, decimal unitPrice = 9.99m)
    {
        _lines.Add(new(productId, quantity, unitPrice));
        return this;
    }

    public OrderSeeder WithLine(Product product, int quantity = 1)
    {
        _lines.Add(new(product.Id, quantity, product.Price));
        return this;
    }

    protected override Order Build() => new()
    {
        Id         = _id,
        CustomerId = _customerId ?? Guid.NewGuid(),
        Status     = _status,
        CreatedAt  = _createdAt,
        Lines      = _lines.Select(l => new OrderLine
        {
            Id        = Guid.NewGuid(),
            ProductId = l.ProductId,
            Quantity  = l.Quantity,
            UnitPrice = l.UnitPrice,
        }).ToList(),
    };
}
```

---

## Composite seeder — seed a full scenario in one call

When a test needs a complex starting state, compose seeders into a scenario helper:

```csharp
// tests/MyApp.ApplicationTests/DataSeeders/Scenarios/CustomerWithOrdersScenario.cs
namespace MyApp.ApplicationTests.DataSeeders.Scenarios;

public sealed class CustomerWithOrdersScenario
{
    public Customer Customer { get; private set; } = null!;
    public Product  Product  { get; private set; } = null!;
    public Order    PendingOrder   { get; private set; } = null!;
    public Order    ConfirmedOrder { get; private set; } = null!;

    public static async Task<CustomerWithOrdersScenario> CreateAsync(
        AppDbContext db,
        CancellationToken ct = default)
    {
        var scenario = new CustomerWithOrdersScenario();

        scenario.Customer = await new CustomerSeeder(db).SeedAsync(ct);
        scenario.Product  = await new ProductSeeder(db).WithStock(50).SeedAsync(ct);

        scenario.PendingOrder = await new OrderSeeder(db)
            .WithCustomer(scenario.Customer)
            .WithStatus(OrderStatus.Pending)
            .WithLine(scenario.Product, quantity: 2)
            .SeedAsync(ct);

        scenario.ConfirmedOrder = await new OrderSeeder(db)
            .WithCustomer(scenario.Customer)
            .WithStatus(OrderStatus.Confirmed)
            .WithLine(scenario.Product, quantity: 1)
            .SeedAsync(ct);

        return scenario;
    }
}
```

Usage:

```csharp
[Fact]
public async Task GET_Orders_WhenCustomerHasOrders_ReturnsAllOrders()
{
    // Arrange
    var scenario = await CustomerWithOrdersScenario.CreateAsync(Db);
    AuthenticateAs(scenario.Customer.UserId);

    // Act
    var response = await Client.GetAsync($"/orders?customerId={scenario.Customer.Id}");

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var orders = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
    orders.Should().HaveCount(2);
}
```

---

## Usage examples across test levels

### Integration test

```csharp
[Fact]
public async Task GetByStatusAsync_WhenMixedStatuses_ReturnsOnlyRequested()
{
    // Arrange
    var customer = await new CustomerSeeder(Db).SeedAsync();

    await new OrderSeeder(Db)
        .WithCustomer(customer)
        .WithStatus(OrderStatus.Pending)
        .SeedAsync();

    await new OrderSeeder(Db)
        .WithCustomer(customer)
        .WithStatus(OrderStatus.Confirmed)
        .SeedAsync();

    await new OrderSeeder(Db)
        .WithCustomer(customer)
        .WithStatus(OrderStatus.Confirmed)
        .SeedAsync();

    // Act
    var results = await _sut.GetByStatusAsync(OrderStatus.Confirmed);

    // Assert
    results.Should().HaveCount(2);
    results.Should().AllSatisfy(o => o.Status.Should().Be(OrderStatus.Confirmed));
}
```

### Application test — specific scenario

```csharp
[Fact]
public async Task POST_CancelOrder_WhenOrderIsAlreadyCancelled_Returns409()
{
    // Arrange
    var customer = await new CustomerSeeder(Db).SeedAsync();
    var order    = await new OrderSeeder(Db)
        .WithCustomer(customer)
        .WithStatus(OrderStatus.Cancelled)
        .SeedAsync();

    AuthenticateAs(customer.UserId);

    // Act
    var response = await Client.PostAsync($"/orders/{order.Id}/cancel", null);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Conflict);
}
```

### Seeding many entities

```csharp
[Fact]
public async Task GET_Orders_WhenPageSizeIsThree_ReturnsThreeOrders()
{
    // Arrange
    var customer = await new CustomerSeeder(Db).SeedAsync();

    // Seed 10 orders using SeedManyAsync
    await new OrderSeeder(Db)
        .WithCustomer(customer)
        .SeedManyAsync(count: 10);

    AuthenticateAs(customer.UserId);

    // Act
    var response = await Client.GetAsync($"/orders?page=1&pageSize=3&customerId={customer.Id}");

    // Assert
    var result = await response.Content.ReadFromJsonAsync<PagedResult<OrderDto>>();
    result!.Items.Should().HaveCount(3);
    result.TotalCount.Should().Be(10);
}
```

---

## Seeder design guidelines

- **Sensible defaults everywhere** — the caller only sets properties relevant to the test.
  A `CustomerSeeder.SeedAsync()` call with no overrides must produce a valid, saveable entity.
- **Return the persisted entity** — always return the saved entity (with any DB-generated values populated), not the input.
- **One seeder per aggregate root** — `OrderSeeder` includes lines; `CustomerSeeder` does not include orders.
- **Scenario seeders for complex multi-entity setups** — keep individual test Arrange sections short.
- **Never share seeded entities between tests** — each test seeds its own data after the clean-up in `InitializeAsync`.
- **No static state in seeders** — all configuration is per-instance via fluent methods.
