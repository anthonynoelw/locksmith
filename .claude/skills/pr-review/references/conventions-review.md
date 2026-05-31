# Conventions Review Checklist

Detailed checklist for Pass 1 of the PR review. Check every item against every
changed `.cs` file. The full conventions definition lives in
`../code-writing/references/conventions.md` — read it if a rule needs clarification.

Severity levels:
- **Must Fix** — violates a rule that causes bugs, breaks builds, or will be rejected at review
- **Should Fix** — clear convention violation; should not be merged as-is
- **Suggestion** — style preference; non-blocking but worth noting

---

## 1. Naming

### Types

- [ ] Classes, records, structs, enums use **PascalCase**
- [ ] Interfaces use **PascalCase with `I` prefix** (`IOrderRepository`, not `OrderRepository` or `orderRepository`)
- [ ] Exception classes end with **`Exception`** suffix
- [ ] EventArgs classes end with **`EventArgs`** suffix
- [ ] Extension method static classes end with **`Extensions`** suffix
- [ ] Enum values use **PascalCase** (not `ALL_CAPS` or `camelCase`)
- [ ] Generic type parameters use **`T` prefix + PascalCase** (`TEntity`, `TResult`)

### Members

- [ ] Public methods use **PascalCase**
- [ ] Private and protected fields use **`_camelCase`** (underscore + camelCase, no exceptions)
- [ ] Local variables and parameters use **camelCase**
- [ ] Constants (`const`) use **PascalCase** (not `SCREAMING_SNAKE`)
- [ ] Async methods end with **`Async` suffix** — including interface declarations
- [ ] Boolean members read as **true/false statements** (`isEnabled`, `hasPermission`, not `enabled`, `permission`)
- [ ] Collection members are **plural** (`orders`, `customerIds`, not `orderList`, `customerIdList`)
- [ ] **No abbreviations** — `customerId` not `cId`, `maximumRetryCount` not `maxRC`
- [ ] **No Hungarian notation** — `name` not `strName`, `count` not `intCount`
- [ ] **No type suffixes** in variable names — `orders` not `orderList`, `config` not `configDictionary`

### Files and namespaces

- [ ] **One public type per file** — filename matches the type name exactly
- [ ] **File-scoped namespaces** used (`namespace MyApp.Orders;` — no braces)
- [ ] Namespace **mirrors the folder path** relative to the project root
- [ ] `using` directives: System namespaces first, then third-party, then project — each group separated by a blank line

---

## 2. Types

- [ ] Concrete service/repository/handler classes are **`sealed`** (unless designed as base classes)
- [ ] DTOs and value objects use **`record`** or **`sealed record`**
- [ ] `record` properties use **`init`** accessors unless mutation is explicitly needed
- [ ] `required` keyword used on properties that must be set at construction
- [ ] **`static class`** used for extension methods — never instance class with static members only
- [ ] **No `abstract` classes** where an interface suffices

---

## 3. Access modifiers

- [ ] **Explicit access modifier on every type** — no implicit `internal`
- [ ] **Explicit access modifier on every member** — no implicit `private`
- [ ] Members are as **restrictive as possible** — `private` → `internal` → `public`
- [ ] **No `public` fields** — use properties

---

## 4. Members and methods

- [ ] **Guard clauses at the top** of every public method — before any logic

```csharp
// CORRECT
public async Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(request);
    ArgumentException.ThrowIfNullOrWhiteSpace(request.Reference);
    // ... logic
}
```

- [ ] Guard clause helpers used correctly:
  - `ArgumentNullException.ThrowIfNull(x)` — not manual `if (x is null) throw`
  - `ArgumentException.ThrowIfNullOrWhiteSpace(s)` — not `string.IsNullOrWhiteSpace`
  - `ArgumentOutOfRangeException.ThrowIfNegative(n)` — not manual range check
- [ ] **`CancellationToken` parameter present** on every `async` method
- [ ] **`CancellationToken` defaults to `default`** on public method signatures
- [ ] **`CancellationToken` is forwarded** to every awaited call (not ignored)
- [ ] **Expression-bodied members** used for single-expression methods/properties — not for multi-statement bodies
- [ ] **No logic in constructors** — constructors wire up dependencies only
- [ ] **No `static` mutable state** in service classes

---

## 5. Async and concurrency

- [ ] **No `.Result`** on a `Task` — always `await`
- [ ] **No `.Wait()`** on a `Task` — always `await`
- [ ] **No `.GetAwaiter().GetResult()`** — always `await`
- [ ] **No `async void`** except on event handlers (and even then, prefer a wrapper)
- [ ] **No `Thread.Sleep`** — use `await Task.Delay(ms, ct)`
- [ ] **No `lock`** across an `await` — use `SemaphoreSlim.WaitAsync()` instead
- [ ] **No `new HttpClient()`** — use `IHttpClientFactory`
- [ ] **No fire-and-forget `_ = SomeAsync()`** for work that matters — use a channel or hosted service
- [ ] `await using` used for `IAsyncDisposable` — not plain `using`
- [ ] `await foreach` used for `IAsyncEnumerable<T>` — not `.ToListAsync()` when streaming is appropriate

---

## 6. Null handling

- [ ] `<Nullable>enable</Nullable>` is present in the project file (or `Directory.Build.props`)
- [ ] **No bare null suppression** (`someValue!`) without a comment explaining why it is safe
- [ ] Nullable return types (`T?`) declared where the method can legitimately return null
- [ ] Null-coalescing (`??`, `??=`) used instead of verbose null checks for simple defaults
- [ ] `?? throw new NotFoundException(...)` pattern used for mandatory lookups

---

## 7. Logging

- [ ] **Structured log message templates** — curly-brace placeholders, never string interpolation

```csharp
// MUST FIX — interpolation defeats structured logging
_logger.LogInformation($"Order {orderId} created for {customerId}");

// CORRECT
_logger.LogInformation("Order {OrderId} created for customer {CustomerId}", orderId, customerId);
```

- [ ] **No sensitive data in logs** — no passwords, tokens, connection strings, PII
- [ ] Log level is appropriate: `LogDebug` for verbose internals, `LogInformation` for business events, `LogWarning` for recoverable issues, `LogError`/`LogCritical` for faults
- [ ] Logger is typed: `ILogger<T>` — not `ILogger` untyped

---

## 8. Formatting and layout

- [ ] **4-space indentation** — no tabs
- [ ] **Allman brace style** — opening brace on its own line for types and methods
- [ ] **Lines under 120 characters** — long parameter lists and chains wrapped
- [ ] **One blank line between members** — not zero, not two or more
- [ ] **No trailing whitespace**
- [ ] **Member order within a class:**
  1. Constants
  2. Static readonly fields
  3. Private fields
  4. Constructors
  5. Public properties
  6. Public methods
  7. Private/internal methods
  8. Nested types

---

## 9. XML documentation

- [ ] **XML doc comment on every public type**
- [ ] **XML doc comment on every public method** — including interface declarations
- [ ] `<param>` tags for non-obvious parameters
- [ ] `<returns>` tag when the return value needs explanation
- [ ] `<exception>` tag for documented exception types

---

## 10. Anti-patterns — instant Must Fix

Any of these in the diff is an automatic **Must Fix**:

| Anti-pattern | Why |
|---|---|
| `dynamic` | Bypasses type safety; runtime errors instead of compile-time |
| `Thread.Sleep` | Blocks a thread; use `await Task.Delay` |
| `catch (Exception e) { }` | Swallows all errors silently |
| `catch (Exception e) { throw e; }` | Loses the stack trace; use `throw;` |
| `.Result` / `.Wait()` on Task | Deadlock risk; use `await` |
| `new HttpClient()` | Socket exhaustion |
| Hardcoded connection string / secret | Security violation |
| `async void` on non-event-handler | Exceptions cannot be caught by callers |
| Mutable `static` field in a service | Shared state across requests; concurrency bugs |
| `Html.Raw(userInput)` | XSS vulnerability |
