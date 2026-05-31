# C# Conventions Reference

Industry-standard C# naming, formatting, and structural conventions based on Microsoft's official guidelines, the .NET Runtime codebase style, and widely adopted community practices.

---

## Table of contents

1. [Naming conventions](#1-naming-conventions)
2. [File and namespace structure](#2-file-and-namespace-structure)
3. [Types: classes, records, structs, interfaces](#3-types-classes-records-structs-interfaces)
4. [Members: fields, properties, methods, events](#4-members-fields-properties-methods-events)
5. [Access modifiers](#5-access-modifiers)
6. [Formatting and layout](#6-formatting-and-layout)
7. [Language features and idioms](#7-language-features-and-idioms)
8. [Async and concurrency](#8-async-and-concurrency)
9. [Null handling](#9-null-handling)
10. [Comments and documentation](#10-comments-and-documentation)
11. [Project and solution structure](#11-project-and-solution-structure)
12. [Common anti-patterns to avoid](#12-common-anti-patterns-to-avoid)

---

## 1. Naming conventions

### Casing rules

| Construct | Casing | Example |
|---|---|---|
| Class, record, struct | PascalCase | `OrderService`, `CustomerDto` |
| Interface | PascalCase with `I` prefix | `IOrderRepository` |
| Enum | PascalCase | `OrderStatus` |
| Enum value | PascalCase | `OrderStatus.Pending` |
| Public method | PascalCase | `GetByIdAsync` |
| Public property | PascalCase | `FirstName` |
| Public field (rare) | PascalCase | `MaxRetryCount` |
| Constant (`const`) | PascalCase | `MaxPageSize` |
| Private / protected field | `_camelCase` with underscore prefix | `_orderRepository` |
| Local variable | camelCase | `orderTotal` |
| Parameter | camelCase | `customerId` |
| Type parameter (generic) | `T` prefix + PascalCase | `TEntity`, `TResult` |
| Async method | PascalCase + `Async` suffix | `CreateOrderAsync` |
| Event | PascalCase | `OrderCreated` |
| EventArgs subclass | PascalCase + `EventArgs` suffix | `OrderCreatedEventArgs` |
| Exception subclass | PascalCase + `Exception` suffix | `NotFoundException` |
| Extension method class | PascalCase + `Extensions` suffix | `StringExtensions` |
| Test class | Subject class name + `Tests` | `OrderServiceTests` |
| Test method | `MethodName_State_ExpectedResult` | `CreateOrder_WhenStockInsufficient_ThrowsException` |

### Naming guidelines

- **Be descriptive, not abbreviated.** `customerId` not `cid`. `maximumRetryCount` not `maxRC`.
- **Boolean names read as true/false statements.** `isEnabled`, `hasPermission`, `canProcess` — not `enabled`, `permission`, `process`.
- **Collection names are plural.** `orders`, `customerIds`, `lineItems`.
- **Avoid type suffixes in variable names.** `orders` not `orderList`. `mapping` not `mappingDictionary`.
- **Interfaces describe capability, not implementation.** `IOrderRepository` (not `IOrderRepo`), `INotifiable` (not `INotificationHandler`).
- **No Hungarian notation.** `name` not `strName`. `count` not `intCount`.

---

## 2. File and namespace structure

### One type per file

Each public type lives in its own file. The filename matches the type name exactly.

```
OrderService.cs       → public sealed class OrderService
IOrderRepository.cs   → public interface IOrderRepository
OrderDto.cs           → public sealed record OrderDto
```

Exception: small, tightly coupled private types (e.g. a private nested class used only by the outer class) may live in the same file.

### File-scoped namespaces (required, C# 10+)

```csharp
// CORRECT — file-scoped, no extra indentation
namespace MyApp.Orders.Services;

public sealed class OrderService { }

// WRONG — old block-scoped style, adds indentation
namespace MyApp.Orders.Services
{
    public sealed class OrderService { }
}
```

### Namespace mirrors folder structure

```
Src/
  MyApp.Orders/
    Services/
      OrderService.cs        → namespace MyApp.Orders.Services
    Repositories/
      OrderRepository.cs     → namespace MyApp.Orders.Repositories
    Models/
      OrderDto.cs            → namespace MyApp.Orders.Models
```

### Using directives

- Place `using` directives at the top of the file, before the namespace declaration.
- Order: System namespaces first, then third-party, then project namespaces. Separate each group with a blank line.
- Use `global using` in a dedicated `GlobalUsings.cs` file for universally needed namespaces.

```csharp
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using MyApp.Common.Exceptions;
using MyApp.Orders.Models;
```

---

## 3. Types: classes, records, structs, interfaces

### When to use each

| Type | Use for |
|---|---|
| `class` | Services, repositories, controllers, domain entities with identity and behavior |
| `sealed class` | All concrete service/repository implementations (prevents unintended inheritance) |
| `record` | DTOs, request/response models, value objects, immutable data bags |
| `readonly struct` | Small, allocation-free value types (coordinates, money amounts for hot paths) |
| `interface` | Abstractions for DI, testability, and cross-cutting concerns |
| `enum` | Fixed sets of named constants (status codes, categories) |
| `static class` | Extension methods, helper utilities with no state |

### Sealing concrete classes

Always `sealed` unless the class is explicitly designed as a base class.

```csharp
// CORRECT
public sealed class OrderService : IOrderService { }

// INCORRECT — leaves the door open for unintended inheritance
public class OrderService : IOrderService { }
```

### Interfaces

```csharp
/// <summary>Provides access to order data.</summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetPageAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

### Records for DTOs

```csharp
// Positional record — concise, immutable by default
public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineDto> Lines);

// Extended record — when you need custom validation or computed properties
public sealed record CreateOrderRequest
{
    public required Guid CustomerId { get; init; }
    public required IReadOnlyList<OrderLineRequest> Lines { get; init; }
}
```

### Enums

```csharp
// CORRECT — PascalCase values, explicit underlying type
public enum OrderStatus : byte
{
    Pending = 0,
    Confirmed = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}

// For flags
[Flags]
public enum UserPermission
{
    None    = 0,
    Read    = 1 << 0,
    Write   = 1 << 1,
    Delete  = 1 << 2,
    Admin   = Read | Write | Delete
}
```

---

## 4. Members: fields, properties, methods, events

### Fields

```csharp
public sealed class OrderService
{
    // Private fields — underscore + camelCase
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<OrderService> _logger;

    // Constants — PascalCase
    private const int MaxBatchSize = 100;

    // Static readonly — PascalCase
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
}
```

Prefer primary constructors (C# 12) for DI — the compiler generates the backing fields:

```csharp
public sealed class OrderService(
    IOrderRepository orderRepository,
    ILogger<OrderService> logger)
    : IOrderService
{
    // orderRepository and logger are available as implicit fields
}
```

### Properties

```csharp
public sealed class Order
{
    // Auto-property
    public Guid Id { get; private set; }

    // Init-only (for immutable entities set via constructor)
    public Guid CustomerId { get; init; }

    // Computed property
    public decimal TotalAmount => Lines.Sum(l => l.LineTotal);

    // Required property (C# 11+)
    public required string ReferenceNumber { get; init; }
}
```

### Methods

```csharp
// Public async method — PascalCase + Async suffix
public async Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
{
    // guard clauses first
    ArgumentNullException.ThrowIfNull(request);

    // logic
    var order = Order.Create(request.CustomerId, request.Lines);
    await _orderRepository.AddAsync(order, ct);

    _logger.LogInformation("Created order {OrderId} for customer {CustomerId}",
        order.Id, order.CustomerId);

    return order.ToDto();
}

// Private helper — PascalCase, no Async suffix needed if not truly async
private static OrderLine MapLine(OrderLineRequest request) =>
    new(request.ProductId, request.Quantity, request.UnitPrice);
```

Expression-bodied members are appropriate for single-expression methods and properties. Multi-statement logic uses block bodies.

### Events

```csharp
// Event declaration on a class
public event EventHandler<OrderCreatedEventArgs>? OrderCreated;

// Raising the event safely
protected virtual void OnOrderCreated(OrderCreatedEventArgs e) =>
    OrderCreated?.Invoke(this, e);

// EventArgs
public sealed class OrderCreatedEventArgs(Guid orderId) : EventArgs
{
    public Guid OrderId { get; } = orderId;
}
```

---

## 5. Access modifiers

- **Explicit on everything.** Never rely on the default (`internal` for types, `private` for members).
- **Prefer the most restrictive access that works.** `private` → `private protected` → `protected` → `internal` → `public`.
- **`internal` for types that don't cross assembly boundaries.** Only use `public` when the type is part of the public API.

```csharp
// CORRECT — explicit everywhere
public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;

    public async Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken ct = default) { }

    private static OrderDto MapToDto(Order order) { }
}

// WRONG — missing access modifiers
class OrderService : IOrderService
{
    IOrderRepository _repository;
    async Task<OrderDto?> GetByIdAsync(Guid id) { }
}
```

---

## 6. Formatting and layout

### Indentation and braces

- **4 spaces** per indent level. Never tabs.
- Allman style (opening brace on its own line) for type and method declarations.
- Single-line braces are acceptable for short auto-properties and expression-bodied members.

```csharp
// Type and method — Allman
public sealed class OrderService(IOrderRepository repository) : IOrderService
{
    public async Task<OrderDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Order {id} not found.");

        return order.ToDto();
    }
}

// Expression-bodied — acceptable when the body is a single expression
public string FullName => $"{FirstName} {LastName}";
public override string ToString() => $"Order({Id})";
```

### Line length and wrapping

- Soft limit of **120 characters** per line.
- When wrapping method parameters or arguments, each parameter goes on its own line, aligned with the opening parenthesis or indented by 4 spaces.

```csharp
// Wrapping long parameter lists
public async Task<PagedResult<OrderDto>> GetPagedAsync(
    Guid customerId,
    int page,
    int pageSize,
    CancellationToken ct = default)

// Wrapping long method chains
var orders = await db.Orders
    .Where(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending)
    .OrderByDescending(o => o.CreatedAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .AsNoTracking()
    .ToListAsync(ct);
```

### Blank lines

- One blank line between members.
- Two blank lines between top-level type declarations in the same file (rare — prefer one type per file).
- No trailing blank lines at the end of a block.

### Member ordering within a class

Follow this order consistently:

1. Constants (`const`)
2. Static readonly fields
3. Private fields
4. Constructors
5. Public properties
6. Public methods
7. Private / internal methods
8. Nested types (if any)

---

## 7. Language features and idioms

### var usage

Use `var` when the type is **obvious from the right-hand side** of the assignment. Use the explicit type when it aids clarity.

```csharp
// CORRECT — type obvious from constructor / literal
var order = new Order(customerId);
var orders = new List<Order>();
var id = Guid.NewGuid();

// CORRECT — explicit type adds clarity
IReadOnlyList<Order> orders = await repository.GetAllAsync(ct);
HttpResponseMessage response = await client.GetAsync(url, ct);

// WRONG — type is not obvious
var result = GetResult();        // what type is result?
var x = ProcessItems(input);     // same problem
```

### Pattern matching

```csharp
// Type pattern
if (shape is Circle { Radius: > 0 } circle)
    return Math.PI * circle.Radius * circle.Radius;

// Switch expression
var description = status switch
{
    OrderStatus.Pending   => "Awaiting confirmation",
    OrderStatus.Confirmed => "Confirmed",
    OrderStatus.Shipped   => "In transit",
    OrderStatus.Delivered => "Delivered",
    OrderStatus.Cancelled => "Cancelled",
    _                     => throw new ArgumentOutOfRangeException(nameof(status))
};

// Null check
if (user is null)
    throw new NotFoundException("User not found.");
```

### Collection expressions (C# 12)

```csharp
// Collection expression for arrays and lists
int[] numbers = [1, 2, 3, 4, 5];
List<string> names = ["Alice", "Bob", "Carol"];

// Spread operator
int[] combined = [..first, ..second];
```

### String interpolation and formatting

```csharp
// CORRECT — interpolation for readable strings
var message = $"Order {order.Id} created for customer {order.CustomerId}.";

// CORRECT — structured logging (NOT interpolation in log calls)
_logger.LogInformation("Order {OrderId} created for customer {CustomerId}",
    order.Id, order.CustomerId);

// WRONG — interpolation in log calls (defeats structured logging)
_logger.LogInformation($"Order {order.Id} created.");
```

### LINQ

```csharp
// Method syntax preferred for complex queries
var total = orders
    .Where(o => o.Status == OrderStatus.Confirmed)
    .SelectMany(o => o.Lines)
    .Sum(l => l.LineTotal);

// Query syntax acceptable for joins (often more readable)
var result =
    from order in orders
    join customer in customers on order.CustomerId equals customer.Id
    select new { order, customer };

// Avoid LINQ for simple loops where a foreach is clearer
foreach (var order in pendingOrders)
{
    await ProcessAsync(order, ct);
}
```

### switch expressions over if-else chains for exhaustive matching

```csharp
// CORRECT — switch expression, exhaustive, no fall-through risk
string label = priority switch
{
    Priority.Low    => "Low",
    Priority.Medium => "Medium",
    Priority.High   => "High",
    Priority.Critical => "CRITICAL",
    _ => throw new ArgumentOutOfRangeException(nameof(priority))
};
```

---

## 8. Async and concurrency

### Core rules

- Every method that performs I/O must be `async Task` or `async Task<T>`. Never use `async void` except for event handlers.
- Suffix all async methods with `Async`: `GetByIdAsync`, `CreateAsync`.
- Always pass and forward `CancellationToken`. Default it to `default` on public methods.
- Never block on a task: no `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- Use `await using` and `await foreach` for async disposables and async enumerables.

```csharp
// CORRECT
public async Task<IReadOnlyList<OrderDto>> GetByCustomerAsync(
    Guid customerId,
    CancellationToken ct = default)
{
    var orders = await _repository.GetByCustomerAsync(customerId, ct);
    return orders.Select(o => o.ToDto()).ToList();
}

// WRONG — blocking, risks deadlock
public IReadOnlyList<OrderDto> GetByCustomer(Guid customerId)
{
    var orders = _repository.GetByCustomerAsync(customerId).Result;
    return orders.Select(o => o.ToDto()).ToList();
}
```

### Cancellation token flow

```csharp
// Thread all the way down
public async Task ProcessOrdersAsync(CancellationToken ct = default)
{
    var orders = await _repository.GetPendingAsync(ct);

    foreach (var order in orders)
    {
        ct.ThrowIfCancellationRequested();
        await _processor.ProcessAsync(order, ct);
    }
}
```

### Fire-and-forget (background work)

Never use `_ = SomeAsync()` for background work that matters. Enqueue to a channel or use `IHostedService`.

```csharp
// WRONG — exceptions are swallowed, no cancellation
_ = SendEmailAsync(user);

// CORRECT — enqueue to a background channel
await _emailQueue.Writer.WriteAsync(new EmailJob(user), ct);
```

### Parallelism

```csharp
// CPU-bound parallelism
await Parallel.ForEachAsync(items, ct, async (item, innerCt) =>
{
    await ProcessAsync(item, innerCt);
});

// Controlled concurrency (max 5 at once)
var limiter = new SemaphoreSlim(5);
var tasks = items.Select(async item =>
{
    await limiter.WaitAsync(ct);
    try { await ProcessAsync(item, ct); }
    finally { limiter.Release(); }
});
await Task.WhenAll(tasks);
```

---

## 9. Null handling

### Nullable reference types — always enabled

```xml
<!-- In every .csproj -->
<Nullable>enable</Nullable>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

### Null patterns

```csharp
// Guard clause at method entry
ArgumentNullException.ThrowIfNull(request);
ArgumentException.ThrowIfNullOrWhiteSpace(name);

// Null-conditional and null-coalescing
var name = customer?.Profile?.DisplayName ?? "Anonymous";

// Null-coalescing assignment
_cache ??= new Dictionary<Guid, Order>();

// Throw on null return
var order = await _repository.GetByIdAsync(id, ct)
    ?? throw new NotFoundException($"Order {id} not found.");

// Nullable return type — caller must check
public async Task<Order?> FindAsync(Guid id, CancellationToken ct = default) =>
    await _db.Orders.FindAsync([id], ct);
```

### Suppress null warnings sparingly

Only suppress (`!`) when you have verified the value is non-null and the compiler cannot infer it:

```csharp
// Acceptable — we checked just above
if (value is not null)
    Process(value!); // still wrong — remove the !
    Process(value);  // correct — compiler knows it's non-null after the check

// Acceptable only when genuinely required
var config = builder.Configuration.GetSection("Feature")!;
```

---

## 10. Comments and documentation

### XML doc comments on all public members

```csharp
/// <summary>
/// Retrieves an order by its unique identifier.
/// </summary>
/// <param name="id">The unique identifier of the order.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// The <see cref="OrderDto"/> if found; otherwise <see langword="null"/>.
/// </returns>
/// <exception cref="NotFoundException">Thrown when the order does not exist.</exception>
public async Task<OrderDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
```

### Inline comments

Use inline comments to explain **why**, not **what**. Code should be self-explanatory; comments explain intent, tradeoffs, or non-obvious constraints.

```csharp
// CORRECT — explains a non-obvious constraint
// EF Core does not support parallel queries on the same DbContext.
// Use separate contexts for concurrent operations.

// WRONG — restates the code
// Get the order by id
var order = await _repository.GetByIdAsync(id, ct);
```

### TODO and FIXME

Always include a ticket or author reference:

```csharp
// TODO(#1234): Replace with IMemoryCache once rate limiting is in place.
// FIXME: This throws on empty collections — tracked in #5678.
```

---

## 11. Project and solution structure

### Recommended solution layout

```
YourSolution/
├── Src/
│   ├── YourApp.Api/                  # Minimal API / controllers, middleware, Program.cs
│   ├── YourApp.Application/          # Services, use cases, validators, DTOs
│   ├── YourApp.Domain/               # Entities, value objects, domain events, enums
│   └── YourApp.Infrastructure/       # EF Core, repositories, external HTTP clients
├── tests/
│   ├── YourApp.UnitTests/            # Pure unit tests, no I/O
│   ├── YourApp.IntegrationTests/     # Tests that hit real DB or external services
│   └── YourApp.ArchitectureTests/    # NetArchTest rules (enforce layer boundaries)
├── .claude/
│   └── skills/
├── YourSolution.sln
└── Directory.Build.props             # Shared MSBuild properties (Nullable, TreatWarningsAsErrors, etc.)
```

### Configuration / Options pattern

Settings classes live in `Src/<Project>/Settings/` and are bound via `IOptions<T>`:

```csharp
// Src/Api/Settings/ApiSettings.cs
namespace Api.Settings;

using System.ComponentModel.DataAnnotations;

public sealed class ApiSettings
{
    [Required]
    public required string Name { get; init; }
}
```

Wire in `ServiceExtensions`:

```csharp
// Src/Api/Extensions/ServiceExtensions.cs
builder.Services
    .AddOptions<ApiSettings>()
    .BindConfiguration(WellKnown.ConfigSections.Api)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**Important:** All configuration section name constants go in `Domain/WellKnown.ConfigSections`, not inside the settings class:

```csharp
// Src/Domain/WellKnown.cs
public static class ConfigSections
{
    public const string Api = "Api";
    public const string Agent = "Agent";
}
```

This centralizes all magic strings and ensures deployment failures happen at startup (when `ValidateOnStart()` is called) rather than at first use.

### Directory.Build.props (shared across all projects)

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisMode>All</AnalysisMode>
    <LangVersion>latest</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

### GlobalUsings.cs (per project)

```csharp
// YourApp.Application/GlobalUsings.cs
global using System.Threading;
global using Microsoft.Extensions.Logging;
global using YourApp.Domain.Entities;
global using YourApp.Domain.Enums;
global using YourApp.Application.Models;
```

---

## 12. Common anti-patterns to avoid

| Anti-pattern | Problem | Correct approach |
|---|---|---|
| `async void` method | Exceptions can't be awaited or caught | `async Task` everywhere except event handlers |
| `.Result` / `.Wait()` on a Task | Blocks thread, risks deadlock | `await` the task |
| `new HttpClient()` per request | Socket exhaustion | `IHttpClientFactory` |
| `DbContext` injected as singleton | ObjectDisposedException | Register as scoped; use `IDbContextFactory<T>` in singletons |
| `catch (Exception e) { }` | Swallows all errors silently | Log and rethrow, or handle specific exception types |
| Mutable public fields | Breaks encapsulation | Properties with appropriate accessors |
| Logic in constructors | Hard to test, hard to trace exceptions | Move to a factory method or Initialize pattern |
| God class / service doing everything | Violates single responsibility | Split into focused services, one use-case per class |
| Returning `null` from collections | Forces null checks at every call site | Return empty collection (`[]` or `Array.Empty<T>()`) |
| `string.Format` for log messages | Defeats structured logging | `_logger.LogInformation("Message {Param}", value)` |
| Magic strings and numbers | Hard to maintain, easy to mistype | Named constants or enums |
| `DateTime.Now` / `DateTime.UtcNow` directly | Hard to test time-dependent logic | Inject `TimeProvider` and use `timeProvider.GetUtcNow()` |
