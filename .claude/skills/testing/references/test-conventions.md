# Test Conventions Reference

Naming, structure, and project layout conventions for xUnit test projects in .NET 10.

---

## Test method naming

### Pattern

```
MethodName_StateUnderTest_ExpectedBehavior
```

For HTTP endpoint tests (application level):

```
HTTP_VERB_Endpoint_StateUnderTest_ExpectedBehavior
```

### Examples

```csharp
// Service / domain tests
CreateOrder_WhenStockIsAvailable_ReturnsCreatedOrder()
CreateOrder_WhenStockIsInsufficient_ReturnsFailureResult()
CreateOrder_WhenRequestIsNull_ThrowsArgumentNullException()
CreateOrder_WhenCustomerIsInactive_ThrowsInvalidOperationException()
GetById_WhenOrderExists_ReturnsOrderWithLines()
GetById_WhenOrderDoesNotExist_ReturnsNull()
CancelOrder_WhenAlreadyCancelled_ThrowsDomainException()
Login_WhenPasswordIsIncorrect_LocksAccountAfterFiveAttempts()
CalculateTotal_WhenDiscountApplied_ReturnsReducedAmount()

// HTTP endpoint tests
POST_Orders_WhenRequestIsValid_Returns201WithOrderDto()
POST_Orders_WhenUnauthenticated_Returns401()
POST_Orders_WhenStockInsufficient_Returns422()
GET_Orders_WhenCustomerHasOrders_ReturnsPagedResults()
GET_Orders_WhenCustomerDoesNotExist_Returns404()
DELETE_Orders_WhenCallerIsNotOwner_Returns403()
PUT_Orders_Id_WhenOrderIsShipped_Returns409()
```

### Naming rules

- **State describes the precondition** — not the input. `WhenStockIsAvailable` not `WhenQuantityIs2`.
- **Expected behavior describes the observable outcome** — not internal implementation. `ReturnsCreatedOrder` not `CallsRepository`.
- **No abbreviations** — `WhenRequestIsNull` not `WhenReqNull`.
- **Past or present tense for state** — `WhenOrderIsShipped`, `WhenCustomerHasNoOrders`.
- **Returns / Throws / Publishes / Saves for expected behavior verbs**.

---

## Test class structure

```csharp
namespace MyApp.UnitTests.Services; // mirrors the source namespace under the test project

public sealed class OrderServiceTests         // matches source class name + "Tests"
{
    // 1. Constants (test data IDs, magic values used across tests)
    private static readonly Guid ValidCustomerId = Guid.NewGuid();
    private static readonly Guid ValidProductId  = Guid.NewGuid();

    // 2. Mocks
    private readonly Mock<IOrderRepository>  _orderRepositoryMock  = new();
    private readonly Mock<IInventoryService> _inventoryServiceMock = new();

    // 3. Subject under test
    private readonly OrderService _sut;

    // 4. Constructor — wire up SUT
    public OrderServiceTests()
    {
        _sut = new OrderService(
            _orderRepositoryMock.Object,
            _inventoryServiceMock.Object);
    }

    // 5. Test methods — grouped by the method under test, separated by blank lines
    // All tests for CreateAsync first, then all tests for GetByIdAsync, etc.

    [Fact]
    public async Task CreateAsync_WhenStockIsAvailable_ReturnsCreatedOrder() { }

    [Fact]
    public async Task CreateAsync_WhenRequestIsNull_ThrowsArgumentNullException() { }

    // 6. Private helpers at the bottom
    private void SetupInventoryAvailable(bool available = true) => /* ... */;
    private static CreateOrderRequest BuildRequest() => /* ... */;
}
```

---

## Test attribute usage

### [Fact] — single test case

Use for any test with a fixed scenario.

```csharp
[Fact]
public async Task GetById_WhenOrderDoesNotExist_ReturnsNull() { }
```

### [Theory] + [InlineData] — multiple inputs, same logic

Use when testing boundary values, invalid inputs, or multiple equivalent states.

```csharp
[Theory]
[InlineData(0)]
[InlineData(-1)]
[InlineData(int.MinValue)]
public async Task CreateOrder_WhenQuantityIsNotPositive_ReturnsError(int quantity) { }
```

### [Theory] + [MemberData] — complex input objects

Use when inputs are too complex for `[InlineData]` (non-primitive types, collections).

```csharp
public static TheoryData<string?, string> InvalidNames => new()
{
    { null,    "Name is required." },
    { "",      "Name is required." },
    { "  ",    "Name is required." },
    { new string('a', 256), "Name must not exceed 255 characters." },
};

[Theory]
[MemberData(nameof(InvalidNames))]
public void Create_WhenNameIsInvalid_ReturnsError(string? name, string expectedError) { }
```

### [Theory] + [ClassData] — when data generation requires logic

```csharp
public sealed class InvalidOrderRequestData : TheoryData<CreateOrderRequest>
{
    public InvalidOrderRequestData()
    {
        Add(new CreateOrderRequest(Guid.Empty, []));
        Add(new CreateOrderRequest(Guid.NewGuid(), []));
        foreach (var qty in new[] { 0, -1, -100 })
            Add(new CreateOrderRequest(Guid.NewGuid(), [new(Guid.NewGuid(), qty)]));
    }
}

[Theory]
[ClassData(typeof(InvalidOrderRequestData))]
public async Task CreateOrder_WhenRequestIsInvalid_ReturnsValidationError(
    CreateOrderRequest request) { }
```

---

## xUnit execution model

Understanding this prevents subtle bugs:

| Behavior | Detail |
|---|---|
| **New instance per test** | xUnit creates a fresh instance of the test class for every `[Fact]` and `[Theory]` case. Mocks and fields re-initialize every test. |
| **Parallel by default** | Tests in different classes run in parallel. Tests in the same class run sequentially by default. |
| **IAsyncLifetime** | Implement on test class or fixture for async setup/teardown (`InitializeAsync` / `DisposeAsync`). |
| **IClassFixture<T>** | Shared fixture created once per test class. Use for expensive-but-safe-to-share state (e.g., a database connection). |
| **ICollectionFixture<T>** | Shared fixture across multiple test classes in a `[Collection]`. Use for app-wide fixtures like `ApplicationFixture`. |

---

## Parallel execution and isolation

```csharp
// Disable parallelism within a test class if tests have shared state side effects
[Collection("Sequential")]
public sealed class OrderRepositoryTests { }

// Or disable globally in xunit.runner.json
{
  "parallelizeAssembly": false,
  "parallelizeTestCollections": true
}
```

Application and integration tests that share a database container must either:
- **Truncate tables before each test** (preferred — see `InitializeAsync` in ApplicationTestBase).
- Or run sequentially with `[Collection]` to avoid conflicts.

---

## .editorconfig for test projects

Add to `.editorconfig` to suppress noise specific to test projects:

```ini
[tests/**/*.cs]
# Allow non-async test method names (e.g. [Theory] with sync overloads)
dotnet_diagnostic.CA1707.severity = none   # underscores in method names
dotnet_diagnostic.CA2007.severity = none   # ConfigureAwait not needed in test code
dotnet_diagnostic.CS1591.severity = none   # missing XML doc comments on test methods
```

---

## xunit.runner.json

Place in each test project root and set **Copy to Output Directory: Always**:

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 4,
  "diagnosticMessages": false,
  "longRunningTestSeconds": 30
}
```

---

## Test coverage targets (guidelines)

| Layer | Target | Rationale |
|---|---|---|
| Domain (entities, value objects) | 95%+ | Pure logic, no I/O — easy and critical |
| Application services | 85%+ | Business rules; unit test the happy + error paths |
| Repositories | Key queries via integration tests | Unit testing EF Core queries is low-value |
| API endpoints | Critical flows via application tests | Auth, validation, status codes |
| Infrastructure (email, SMS, etc.) | Faked in application tests | Test the interface contract, not the vendor SDK |
