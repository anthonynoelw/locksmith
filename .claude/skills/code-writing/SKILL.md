---
name: dotnet-code-writing
description: >
  Write idiomatic, production-quality C# code for .NET 10 projects. Trigger this
  skill whenever the user asks to create a class, record, interface, service,
  controller, repository, middleware, DTO, extension method, background service,
  or any C# construct. Also use when refactoring existing code, implementing
  design patterns (CQRS, mediator, repository, decorator), adding features to
  existing services, or when the user says "write", "create", "implement",
  "add", or "refactor" in a .NET context. Always read conventions.md before
  producing code.
---

# .NET 10 Code Writing

Produce clean, idiomatic, production-grade C# that follows the team's conventions and .NET 10 best practices.

## Before writing any code

1. **Read `references/conventions.md`** — it defines the naming, formatting, and architecture standards for this project. Never deviate from them.
2. **Check existing patterns** — don't introduce a new abstraction if one already exists in the codebase. Ask to see relevant existing files when uncertain.
3. **Clarify scope** — if the request is ambiguous or touches multiple layers (API + service + repo), confirm the expected surface area before producing large amounts of code.

---

## Process

1. Identify which layer(s) the code lives in: API / Application / Domain / Infrastructure.
2. Identify the relevant interfaces, base classes, or abstractions already in play.
3. Write the code following the conventions in `references/conventions.md`.
4. Apply guard clauses, null checks, and cancellation token plumbing upfront — not as an afterthought.
5. Add XML doc comments on every public type and member.
6. State what tests should cover this code (don't write them unless asked).

---

## Layer responsibilities

| Layer | Lives here | Does NOT do |
|---|---|---|
| **API** (Controllers / Minimal API endpoints) | Routing, model binding, HTTP status mapping, auth attributes | Business logic, direct DB access |
| **Application** (Services, Commands, Queries) | Orchestration, validation, use-case logic | Direct DB queries, HTTP concerns |
| **Domain** (Entities, Value Objects, Domain Events) | Business rules, invariants | Infrastructure, serialization |
| **Infrastructure** (Repositories, DbContext, external clients) | Data access, external service calls | Business rules |

---

## Key code patterns (.NET 10)

### Dependency injection via primary constructors (C# 12+)

```csharp
public sealed class OrderService(
    IOrderRepository orderRepository,
    ILogger<OrderService> logger,
    TimeProvider timeProvider) : IOrderService
{
    public async Task<OrderDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var order = await orderRepository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Order {id} not found.");

        logger.LogInformation("Retrieved order {OrderId}", id);

        return order.ToDto();
    }
}
```

### Records for DTOs and value objects

```csharp
// Request DTO
public sealed record CreateOrderRequest(
    Guid CustomerId,
    IReadOnlyList<OrderLineRequest> Lines);

// Response DTO
public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineDto> Lines);

// Value object with validation
public sealed record Money(decimal Amount, string Currency)
{
    public static Money From(decimal amount, string currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        return new(amount, currency);
    }
}
```

### Minimal API endpoint group (preferred over controllers for new code)

```csharp
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        group.MapGet("{id:guid}", GetById);
        group.MapPost(string.Empty, Create);

        return app;
    }

    private static async Task<Results<Ok<OrderDto>, NotFound>> GetById(
        Guid id,
        IOrderService orderService,
        CancellationToken ct)
    {
        var order = await orderService.GetByIdAsync(id, ct);
        return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
    }

    private static async Task<Results<Created<OrderDto>, ValidationProblem>> Create(
        CreateOrderRequest request,
        IOrderService orderService,
        CancellationToken ct)
    {
        var result = await orderService.CreateAsync(request, ct);
        return TypedResults.Created($"/orders/{result.Id}", result);
    }
}
```

### Repository pattern

```csharp
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
}

public sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Orders
            .Include(o => o.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
    }
}
```

### Guard clauses (use built-in throw helpers)

```csharp
public void Process(string name, int count, User? user)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(name);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxBatchSize);
    ArgumentNullException.ThrowIfNull(user);
}
```

### Async-safe locking (never lock across await)

```csharp
private readonly SemaphoreSlim _lock = new(1, 1);

public async Task<Result> ProcessAsync(CancellationToken ct)
{
    await _lock.WaitAsync(ct);
    try
    {
        return await DoWorkAsync(ct);
    }
    finally
    {
        _lock.Release();
    }
}
```

### Result pattern (avoid exception-driven control flow)

```csharp
public sealed record Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string error) { IsSuccess = false; Error = error; }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error) => new(error);
}
```

---

## Output format

Always produce output in this order:

1. **Interface** (if introducing a new abstraction)
2. **Implementation** (concrete class)
3. **DI registration snippet** (`builder.Services.AddScoped<IFoo, Foo>()`)
4. **Design note** — one short paragraph explaining the key decision made
5. **Test surface** — bullet list of what should be tested (don't write tests unless asked)

---

## Always do

- Use `sealed` on all concrete classes not designed for inheritance.
- Use file-scoped namespaces (`namespace My.Project;` — no braces).
- Pass `CancellationToken` through every async call chain; default it to `default` on public methods.
- Use `IReadOnlyList<T>` / `IReadOnlyDictionary<TKey, TValue>` for output collections.
- Use injected `TimeProvider` instead of `DateTime.Now` or `DateTimeOffset.UtcNow` directly.
- Use `ILogger<T>` with structured log message templates — never string interpolation in log calls.
- Mark `record` properties as `init`-only unless mutation is explicitly required.

## Never do

- Never use `dynamic`.
- Never use `Thread.Sleep` — use `await Task.Delay(ms, ct)`.
- Never catch `Exception` and swallow it silently.
- Never block on a `Task` with `.Result` or `.Wait()` — always `await`.
- Never instantiate `HttpClient` directly — always use `IHttpClientFactory`.
- Never hardcode connection strings, secrets, or environment-specific values.
- Never use mutable `static` state in services.

---

## Reference files

| File | When to read |
|---|---|
| `references/conventions.md` | Before writing any code — naming, formatting, and project structure |