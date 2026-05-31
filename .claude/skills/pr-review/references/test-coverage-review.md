# Test Coverage Review Checklist

Detailed checklist for Pass 3 of the PR review. The full testing patterns and
conventions live in `../testing/SKILL.md` and its reference files — open them
for DataSeeder patterns, fixture setup, and FluentAssertions examples.

---

## Step 1 — Map changed production code to required tests

For every file changed in the PR, identify what type of test is required.
Use the table below. If a file changed but no test file changed alongside it,
that is a coverage gap — record it as a finding.

| Changed file type | Required test file | Test level |
|---|---|---|
| `Services/OrderService.cs` | `UnitTests/Services/OrderServiceTests.cs` | Unit |
| `Repositories/OrderRepository.cs` | `IntegrationTests/Repositories/OrderRepositoryTests.cs` | Integration |
| `Controllers/OrdersController.cs` or endpoint group | `ApplicationTests/Orders/CreateOrderTests.cs` (etc.) | Application |
| `Domain/Order.cs` (entity with rules) | `UnitTests/Domain/OrderTests.cs` | Unit |
| `Migrations/20240615_*.cs` | `IntegrationTests/Migrations/MigrationTests.cs` | Integration |
| `Middleware/*.cs` | `ApplicationTests/Middleware/*Tests.cs` | Application |
| Bug fix in any layer | Regression test that fails before the fix | Matching level |

---

## Step 2 — Coverage requirements per change type

### New public method on a service

**Required:** Unit tests in `UnitTests/Services/<ClassName>Tests.cs`

Minimum tests:
- [ ] Happy path — correct input → correct output
- [ ] At least one failure path — domain rule, validation failure, or exception
- [ ] Guard clause tests — null/empty inputs throw `ArgumentNullException` / `ArgumentException`

```csharp
// Example minimum for a new CreateAsync method
CreateAsync_WhenRequestIsValid_ReturnsCreatedDto()          // happy path
CreateAsync_WhenRequestIsNull_ThrowsArgumentNullException() // guard
CreateAsync_WhenStockInsufficient_ReturnsFailureResult()    // domain rule
```

### New repository method

**Required:** Integration tests in `IntegrationTests/Repositories/<ClassName>Tests.cs`

Minimum tests:
- [ ] Happy path — entity exists → correct result returned
- [ ] Not-found path — entity does not exist → returns null / empty list
- [ ] Data is persisted correctly when written (verify via a fresh `DbContext`, not the same instance)

### New or modified API endpoint

**Required:** Application tests in `ApplicationTests/<Feature>/<Action>Tests.cs`

Minimum tests:
- [ ] `2xx` happy path — valid authenticated request → correct response body and status code
- [ ] `401 Unauthorized` — unauthenticated request is rejected
- [ ] `403 Forbidden` — authenticated but wrong role/ownership → rejected
- [ ] `400 Bad Request` or `422 Unprocessable Entity` — invalid/missing fields
- [ ] `404 Not Found` — resource does not exist

```csharp
// Example minimum for POST /orders
POST_Orders_WhenRequestIsValid_Returns201WithOrderDto()
POST_Orders_WhenUnauthenticated_Returns401()
POST_Orders_WhenUserDoesNotOwnCustomer_Returns403()
POST_Orders_WhenRequestBodyIsMissing_Returns400()
POST_Orders_WhenStockInsufficient_Returns422()
```

### Domain entity with business rules

**Required:** Unit tests in `UnitTests/Domain/<EntityName>Tests.cs`

- [ ] Every branch of every domain rule has a test
- [ ] `[Theory]` with `[InlineData]` used for boundary values
- [ ] Domain events are asserted if the entity raises them

### Bug fix

**Required:** Regression test that:
- [ ] Fails on the code **before** the fix (author must confirm this)
- [ ] Passes after the fix
- [ ] Is named to describe the bug: `GetTotal_WhenDiscountExceedsOrderValue_ReturnsZeroNotNegative()`

### EF Core migration

**Required:** Integration test verifying the migration result:

```csharp
// Minimum migration test
[Fact]
public async Task Migration_20240615_AddOrderShippedAt_AddsNullableColumn()
{
    // Arrange — db is at the pre-migration baseline (use a specific target migration)
    // Act — apply the migration under test
    await db.Database.MigrateAsync();

    // Assert — column exists with correct type and nullability
    var columnInfo = await db.Database
        .SqlQuery<ColumnInfo>($"""
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_name = 'Orders' AND column_name = 'ShippedAt'
            """)
        .FirstOrDefaultAsync();

    columnInfo.Should().NotBeNull();
    columnInfo!.IsNullable.Should().Be("YES");
}
```

---

## Step 3 — Test quality checks

For every test file changed or added in the PR:

### Structure

- [ ] **AAA sections are labelled** — `// Arrange`, `// Act`, `// Assert` comments present
- [ ] **One behavior per test** — test method asserts exactly one thing or one object's shape
- [ ] **No multi-assert tests** that mix unrelated behaviors (e.g. verifying a return value AND a side effect in the same test)
- [ ] **Async tests use `async Task`** — not `async void`

### Naming

- [ ] Test class named `<SubjectClass>Tests`
- [ ] Test method follows `MethodName_StateUnderTest_ExpectedBehavior`
- [ ] State description is a precondition (`WhenStockIsInsufficient`) — not an input (`WhenQuantityIs0`)
- [ ] Expected behavior uses result verbs (`Returns`, `Throws`, `Publishes`, `Saves`) — not implementation verbs (`Calls`, `Invokes`)

### Assertions

- [ ] **FluentAssertions used exclusively** — no bare `Assert.Equal`, `Assert.True`, `Assert.NotNull`
- [ ] `.Should().Be()` not `.Should().Equals()` (common mistake)
- [ ] Exception assertions use `act.Should().ThrowExactlyAsync<T>()` — not `Assert.Throws`
- [ ] Collection assertions use `.Should().HaveCount(N)`, `.Should().ContainSingle()`, `.Should().BeEmpty()`
- [ ] String assertions use `.Should().Contain()` or `.Should().StartWith()` — not manual `.Contains()` in a bool assertion

### Mocking (unit tests)

- [ ] **One `Mock<T>` per dependency** — mocks are not shared across test classes
- [ ] `It.IsAny<CancellationToken>()` used in setups that include `CancellationToken` — so the setup does not break when a real token is passed
- [ ] `.Callback<T>()` used to capture arguments when the test needs to assert on what was passed to a mock
- [ ] **Verify is called** for methods where the call itself is the behavior (e.g. `PublishAsync`, `SendAsync`, `DeleteAsync`)
- [ ] **No `VerifyAll()`** unless all setups are expected to be called — it makes tests fragile
- [ ] Mocks test the SUT — they do not mock the SUT itself

### Test data

- [ ] **No magic strings or numbers** in test data — use named constants, factory methods, or `DataSeeder`s
- [ ] Test-specific IDs (`Guid.NewGuid()`) are assigned once to a variable and reused — not inlined multiple times
- [ ] `DataSeeder` pattern used for integration and application tests (see `../testing/references/data-seeders.md`)
- [ ] Seeders use `WithXxx()` fluent methods to set only the properties relevant to the test

### Timing and I/O

- [ ] **No `Thread.Sleep` or `Task.Delay` in tests** — use `FakeTimeProvider` or mock the time-dependent dependency
- [ ] **No network calls in unit tests** — all I/O is mocked
- [ ] Integration tests clean the database in `InitializeAsync` (table truncate) — not in `DisposeAsync`

---

## Step 4 — Test run verification

If test results are provided (`.trx` file, console output, CI log):

- [ ] **All tests pass** — zero failures
- [ ] **No skipped tests** related to the changed code — skipped tests hide coverage gaps
- [ ] **No new `[Skip]` attributes** added without a documented reason

If test results are NOT provided, record:

**[T-N] Test results not supplied**
**Type:** Missing verification
**Gap:** Cannot confirm all tests pass. Reviewer must run `dotnet test` before approving.
**Required action:** Run `dotnet test` and paste results, or link CI run.

---

## Coverage gap quick-reference

| Signal in diff | Coverage gap |
|---|---|
| New `public` method in a service, no new test file | Missing unit tests |
| New repository method, no integration test | Missing integration test |
| New `MapGet` / `MapPost` / `[HttpGet]` etc., no app test | Missing application test |
| Bug fix commit, no regression test | Missing regression test |
| New migration file, no migration test | Missing migration verification |
| Existing test file modified but no new tests added | Possible coverage erosion |
| `[Skip(` added to an existing test | Test disabled — needs justification |
| Test uses `Assert.` instead of `.Should().` | Quality: wrong assertion library |
| `// Arrange` / `// Act` / `// Assert` missing | Quality: AAA not followed |
| `async void` on a test method | Quality: exceptions will not be caught |
