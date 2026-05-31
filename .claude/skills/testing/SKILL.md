---
name: dotnet-testing
description: >
  Write xUnit tests for .NET 10 projects across all test levels: unit tests with Moq,
  integration tests with WebApplicationFactory, and application/end-to-end tests that
  boot a real in-memory application with a real database. Also covers DataSeeders for
  fluent test data seeding, test naming conventions, and AAA structure. Trigger this
  skill whenever the user asks to write a test, add test coverage, mock a dependency,
  create a test fixture, seed test data, set up a test database, write an integration
  test, or says "test this", "cover this", "how do I test", or "add xUnit tests".
---

# .NET 10 Testing with xUnit

Produce clear, maintainable, layered tests across unit, integration, and application levels.
Every test documents behavior — a failing test tells you exactly what broke and why.

## Before writing any test

1. **Identify the test level** — unit, integration, or application (see layer guide below).
2. **Read the relevant reference file** for that level.
3. **Identify the subject under test (SUT)** — the single class or endpoint being exercised.
4. **Name the test** using the convention in `references/test-conventions.md` before writing a line of code.

---

## Test levels at a glance

| Level | What it tests | Isolation | Speed | Reference |
|---|---|---|---|---|
| **Unit** | Single class / method in complete isolation | All deps mocked with Moq | ~ms | `references/unit-tests.md` |
| **Integration** | A slice of the real application (e.g. service + real EF Core + real DB) | External services mocked; DB is real (Testcontainers) | ~s | `references/integration-tests.md` |
| **Application** | Full HTTP stack booted via `WebApplicationFactory`; real DB, real DI graph | 3rd-party HTTP calls stubbed via `WireMock` | ~10s | `references/application-tests.md` |

---

## Universal rules (apply at every level)

### AAA structure — mandatory in every test

Every test body is divided into exactly three labelled sections:

```csharp
[Fact]
public async Task CreateOrder_WhenStockIsAvailable_ReturnsCreatedOrder()
{
    // Arrange
    var customerId = Guid.NewGuid();
    var request = new CreateOrderRequest(customerId, [new OrderLineRequest(ProductId, 2)]);
    _inventoryMock.Setup(i => i.IsAvailableAsync(ProductId, 2, default)).ReturnsAsync(true);

    // Act
    var result = await _sut.CreateAsync(request);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value!.CustomerId.Should().Be(customerId);
}
```

- **Arrange** — set up state, mocks, and input.
- **Act** — call the SUT. One call only per test.
- **Assert** — verify the outcome. Use FluentAssertions exclusively.

### Naming convention

```
MethodName_StateUnderTest_ExpectedBehavior
```

Examples:

```csharp
CreateOrder_WhenStockIsAvailable_ReturnsCreatedOrder()
CreateOrder_WhenStockIsInsufficient_ThrowsOutOfStockException()
GetById_WhenOrderDoesNotExist_ReturnsNull()
Login_WhenPasswordIsIncorrect_LocksAccountAfterFiveAttempts()
```

### One behavior per test

Never assert multiple independent behaviors in a single test. Split them.

```csharp
// WRONG — two behaviors, two reasons to fail
[Fact]
public async Task CreateOrder_HappyPath()
{
    // ...
    result.IsSuccess.Should().BeTrue();
    await _notificationMock.Received(1).SendAsync(Arg.Any<OrderCreatedEvent>()); // unrelated
}

// CORRECT — split into two focused tests
[Fact] public async Task CreateOrder_WhenValid_ReturnsSuccessResult() { }
[Fact] public async Task CreateOrder_WhenValid_SendsOrderCreatedNotification() { }
```

### Use FluentAssertions everywhere

```csharp
// WRONG — xUnit bare assertions
Assert.Equal(expected, actual);
Assert.NotNull(result);
Assert.True(result.IsSuccess);

// CORRECT — FluentAssertions
actual.Should().Be(expected);
result.Should().NotBeNull();
result.IsSuccess.Should().BeTrue();

// Collections
orders.Should().HaveCount(3);
orders.Should().ContainSingle(o => o.Status == OrderStatus.Pending);
orders.Should().BeInAscendingOrder(o => o.CreatedAt);

// Exceptions
var act = () => sut.Process(null!);
act.Should().ThrowExactly<ArgumentNullException>()
   .WithParameterName("request");

// Async exceptions
var act = async () => await sut.CreateAsync(request, ct);
await act.Should().ThrowExactlyAsync<OutOfStockException>();
```

---

## Project structure

```
tests/
├── MyApp.UnitTests/
│   ├── Services/
│   │   └── OrderServiceTests.cs
│   ├── Domain/
│   │   └── OrderTests.cs
│   └── MyApp.UnitTests.csproj
├── MyApp.IntegrationTests/
│   ├── Repositories/
│   │   └── OrderRepositoryTests.cs
│   ├── Infrastructure/
│   │   └── DbContextTests.cs
│   └── MyApp.IntegrationTests.csproj
└── MyApp.ApplicationTests/
    ├── Fixtures/
    │   ├── ApplicationFixture.cs          ← boots WebApplicationFactory + DB
    │   └── ApplicationFixtureCollection.cs
    ├── DataSeeders/
    │   ├── OrderSeeder.cs
    │   └── CustomerSeeder.cs
    ├── Orders/
    │   └── CreateOrderTests.cs
    └── MyApp.ApplicationTests.csproj
```

---

## Output format

Always produce output in this order:

1. **Test class** with constructor, fixtures, and shared setup
2. **Test methods** grouped by the method or endpoint under test
3. **DataSeeder** if the test requires pre-existing data (application tests)
4. **NuGet packages needed** (list once per level, don't repeat if already stated)

---

## Reference files

| File | When to read |
|---|---|
| `references/unit-tests.md` | Writing unit tests with Moq |
| `references/integration-tests.md` | Writing integration tests against a real DB |
| `references/application-tests.md` | Booting the full application in tests |
| `references/data-seeders.md` | Fluent data seeding for integration and application tests |
| `references/test-conventions.md` | Naming, structure, and project layout conventions |