# Unit Tests Reference

Unit tests verify a single class in complete isolation. Every dependency is replaced
with a Moq mock. No I/O, no database, no HTTP — pure logic.

---

## Test class anatomy

```csharp
namespace MyApp.UnitTests.Services;

public sealed class OrderServiceTests
{
    // --- mocks (one per dependency of the SUT) ---
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IInventoryService> _inventoryServiceMock = new();
    private readonly Mock<ILogger<OrderService>> _loggerMock = new();

    // --- subject under test ---
    // Constructed once per test; Moq mock objects are reset per test by default
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _sut = new OrderService(
            _orderRepositoryMock.Object,
            _inventoryServiceMock.Object,
            _loggerMock.Object);
    }
}
```

**Rules:**
- One `Mock<T>` field per injected dependency — never more.
- Create the SUT in the constructor (xUnit creates a new instance per test).
- Never share state between tests via static fields.

---

## Moq patterns

### Setup — return a value

```csharp
// Return a value from an async method
_orderRepositoryMock
    .Setup(r => r.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
    .ReturnsAsync(new Order { Id = orderId, Status = OrderStatus.Pending });

// Return null (use nullable return type)
_orderRepositoryMock
    .Setup(r => r.GetByIdAsync(orderId, It.IsAny<CancellationToken>()))
    .ReturnsAsync((Order?)null);

// Return a sequence on successive calls
_orderRepositoryMock
    .SetupSequence(r => r.GetNextAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(order1)
    .ReturnsAsync(order2)
    .ReturnsAsync((Order?)null);
```

### Setup — throw an exception

```csharp
_orderRepositoryMock
    .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
    .ThrowsAsync(new DbUpdateException("Constraint violation"));
```

### Setup — capture arguments (callbacks)

```csharp
Order? capturedOrder = null;

_orderRepositoryMock
    .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
    .Callback<Order, CancellationToken>((order, _) => capturedOrder = order)
    .Returns(Task.CompletedTask);

// ... act ...

capturedOrder!.CustomerId.Should().Be(expectedCustomerId);
```

### Verify — was a method called?

```csharp
// Called exactly once with specific args
_orderRepositoryMock.Verify(
    r => r.AddAsync(It.Is<Order>(o => o.CustomerId == customerId), It.IsAny<CancellationToken>()),
    Times.Once);

// Called exactly N times
_orderRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Order>(), default), Times.Exactly(2));

// Never called
_orderRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), default), Times.Never);

// Verify ALL setups were called (use sparingly — makes tests brittle)
_orderRepositoryMock.VerifyAll();
```

### Argument matchers

```csharp
It.IsAny<Guid>()                            // any value of that type
It.Is<Order>(o => o.Status == OrderStatus.Pending)  // custom predicate
It.IsIn(Guid.NewGuid(), Guid.NewGuid())     // one of a set
It.IsNotNull<string>()                      // non-null
It.IsRegex(@"^\d{4}$")                      // regex match on strings
```

### Moq strict mode (opt-in for stricter tests)

```csharp
// Strict mode throws if any un-setup method is called
private readonly Mock<IOrderRepository> _repositoryMock = new(MockBehavior.Strict);
```

Use strict mode for the SUT's most critical dependencies to catch unexpected calls.

---

## Common unit test patterns

### Testing a happy path

```csharp
[Fact]
public async Task CreateAsync_WhenStockIsAvailable_ReturnsCreatedOrderDto()
{
    // Arrange
    var customerId = Guid.NewGuid();
    var productId = Guid.NewGuid();
    var request = new CreateOrderRequest(customerId, [new OrderLineRequest(productId, 2)]);

    _inventoryServiceMock
        .Setup(i => i.IsAvailableAsync(productId, 2, It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    _orderRepositoryMock
        .Setup(r => r.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    // Act
    var result = await _sut.CreateAsync(request);

    // Assert
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeNull();
    result.Value!.CustomerId.Should().Be(customerId);
}
```

### Testing a guard clause / exception

```csharp
[Fact]
public async Task CreateAsync_WhenRequestIsNull_ThrowsArgumentNullException()
{
    // Arrange
    CreateOrderRequest? request = null;

    // Act
    var act = async () => await _sut.CreateAsync(request!);

    // Assert
    await act.Should().ThrowExactlyAsync<ArgumentNullException>()
        .WithParameterName("request");
}
```

### Testing a domain rule failure (Result pattern)

```csharp
[Fact]
public async Task CreateAsync_WhenStockIsInsufficient_ReturnsFailureResult()
{
    // Arrange
    var request = BuildValidRequest();

    _inventoryServiceMock
        .Setup(i => i.IsAvailableAsync(It.IsAny<Guid>(), It.IsAny<int>(), default))
        .ReturnsAsync(false);

    // Act
    var result = await _sut.CreateAsync(request);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain("insufficient stock");
}
```

### Parameterized tests with [Theory] + [InlineData]

```csharp
[Theory]
[InlineData(0)]
[InlineData(-1)]
[InlineData(-100)]
public async Task CreateAsync_WhenQuantityIsNotPositive_ReturnsValidationError(int quantity)
{
    // Arrange
    var request = new CreateOrderRequest(Guid.NewGuid(), [new OrderLineRequest(Guid.NewGuid(), quantity)]);

    // Act
    var result = await _sut.CreateAsync(request);

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain("quantity");
}
```

### Parameterized tests with [MemberData] (complex objects)

```csharp
public static TheoryData<CreateOrderRequest, string> InvalidRequests => new()
{
    { new CreateOrderRequest(Guid.Empty, []), "customerId" },
    { new CreateOrderRequest(Guid.NewGuid(), []), "lines" },
};

[Theory]
[MemberData(nameof(InvalidRequests))]
public async Task CreateAsync_WhenRequestIsInvalid_ReturnsError(
    CreateOrderRequest request, string expectedErrorField)
{
    var result = await _sut.CreateAsync(request);

    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain(expectedErrorField);
}
```

### Testing that a side-effect was triggered

```csharp
[Fact]
public async Task CreateAsync_WhenOrderCreated_PublishesOrderCreatedEvent()
{
    // Arrange
    var request = BuildValidRequest();
    SetupInventoryAvailable();

    // Act
    await _sut.CreateAsync(request);

    // Assert
    _eventBusMock.Verify(
        b => b.PublishAsync(It.Is<OrderCreatedEvent>(e => e.CustomerId == request.CustomerId),
            It.IsAny<CancellationToken>()),
        Times.Once);
}
```

---

## Helpers and builders

Keep test setup DRY with private builder methods on the test class:

```csharp
private static CreateOrderRequest BuildValidRequest(
    Guid? customerId = null,
    int quantity = 1) =>
    new(
        customerId ?? Guid.NewGuid(),
        [new OrderLineRequest(Guid.NewGuid(), quantity)]);

private void SetupInventoryAvailable(bool available = true) =>
    _inventoryServiceMock
        .Setup(i => i.IsAvailableAsync(It.IsAny<Guid>(), It.IsAny<int>(), default))
        .ReturnsAsync(available);
```
