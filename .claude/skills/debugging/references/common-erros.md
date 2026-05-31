# Common .NET 10 Errors — Reference Catalogue

Quick-reference patterns for the most frequent exceptions and runtime issues in C# / .NET 10 projects.

---

## Table of contents

- [Common .NET 10 Errors — Reference Catalogue](#common-net-10-errors--reference-catalogue)
  - [Table of contents](#table-of-contents)
  - [1. NullReferenceException](#1-nullreferenceexception)
  - [2. ObjectDisposedException](#2-objectdisposedexception)
  - [3. InvalidOperationException](#3-invalidoperationexception)
  - [4. StackOverflowException](#4-stackoverflowexception)
  - [5. OutOfMemoryException](#5-outofmemoryexception)
  - [6. TaskCanceledException / OperationCanceledException](#6-taskcanceledexception--operationcanceledexception)
  - [7. HttpRequestException](#7-httprequestexception)
  - [8. DbUpdateConcurrencyException](#8-dbupdateconcurrencyexception)
  - [9. DbUpdateException](#9-dbupdateexception)
  - [10. ArgumentException / ArgumentNullException](#10-argumentexception--argumentnullexception)
  - [11. KeyNotFoundException](#11-keynotfoundexception)
  - [12. FormatException / OverflowException](#12-formatexception--overflowexception)
  - [13. JsonException](#13-jsonexception)
  - [14. NotSupportedException in LINQ-to-SQL](#14-notsupportedexception-in-linq-to-sql)
  - [15. CryptographicException](#15-cryptographicexception)
  - [16. Thread starvation (no exception thrown)](#16-thread-starvation-no-exception-thrown)
  - [17. Memory leak patterns](#17-memory-leak-patterns)

---

## 1. NullReferenceException

**Message**: `Object reference not set to an instance of an object.`

**Common causes**

| Cause | Example |
|---|---|
| Uninitialized property | `public List<Item> Items { get; set; }` — never set |
| Lazy-loaded nav property not included | `order.Customer.Name` without `.Include(o => o.Customer)` |
| Nullable return not checked | `var user = repo.Find(id); user.Name` — Find returns null |
| DI service not registered | Constructor arg is null because it wasn't registered in DI |

**Diagnosis**

Enable nullable reference types in your csproj (default in .NET 10). The compiler surfaces most of these at build time:

```xml
<Nullable>enable</Nullable>
```

**Fix patterns**

```csharp
// Guard clause at method entry
ArgumentNullException.ThrowIfNull(user);

// Null-conditional for chains
var name = order?.Customer?.Name ?? "Unknown";

// Pattern matching
if (result is { } value)
    Process(value);

// Required init property (compile-time enforcement)
public required string Name { get; init; }
```

---

## 2. ObjectDisposedException

**Message**: `Cannot access a disposed object. Object name: 'DbContext'.`

**Common causes**

| Cause | Fix |
|---|---|
| Scoped `DbContext` injected into singleton | Use `IDbContextFactory<T>` in singletons |
| `HttpClient` disposed too early | Use `IHttpClientFactory`, never `new HttpClient()` |
| `CancellationTokenSource` disposed while token still in use | Extend the CTS lifetime or clone the token |
| Stream closed before async read completes | Keep stream open until all reads are done |

**DI lifetime cheat sheet**

```
Singleton  → lives for app lifetime
Scoped     → lives for one HTTP request
Transient  → new instance every time injected
```

Never inject a shorter-lived service into a longer-lived one.

```csharp
// BAD — DbContext (scoped) injected into singleton cache service
public class CacheService(AppDbContext db) { } // registered as singleton

// GOOD
public class CacheService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<User?> GetUserAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.FindAsync(id);
    }
}
```

---

## 3. InvalidOperationException

Broad exception — context is everything. Most common causes in .NET projects:

| Context | Message fragment | Cause |
|---|---|---|
| DI | `No service for type '...' has been registered` | Missing `builder.Services.Add*<T>()` |
| EF Core | `A second operation was started on this context` | DbContext used concurrently — it is not thread-safe |
| LINQ | `Sequence contains no elements` | `.First()` on empty sequence — use `.FirstOrDefault()` |
| LINQ | `Sequence contains more than one element` | `.Single()` matched multiple rows |
| ASP.NET | `Cannot write to response after it has completed` | Writing to response after `return` or after middleware short-circuited |
| Channels | `The channel has been completed` | Writing to a completed `Channel<T>` |

**EF Core concurrency fix**

```csharp
// BAD — sharing DbContext across concurrent tasks
var t1 = db.Users.ToListAsync();
var t2 = db.Orders.ToListAsync();
await Task.WhenAll(t1, t2); // throws!

// GOOD — one DbContext per operation
await using var db1 = await factory.CreateDbContextAsync();
await using var db2 = await factory.CreateDbContextAsync();
var t1 = db1.Users.ToListAsync();
var t2 = db2.Orders.ToListAsync();
await Task.WhenAll(t1, t2);
```

---

## 4. StackOverflowException

**Message**: (process crashes; no catchable exception in most runtimes)

**Causes**

- Infinite recursion — method calls itself without a base case
- Circular object graph during JSON serialization (`System.Text.Json` will throw `JsonException` instead in recent versions, but deep graphs still crash)
- Implicit operator or property getter calling itself

**Diagnosis**

Enable a core dump and analyze the stack — you'll see the same frames repeating hundreds of times.

```bash
dotnet-dump collect --process-id <pid>
dotnet-dump analyze core_<pid>
> clrstack   # look for repeating frames
```

**Fix**

Add a base case, break the cycle, or switch to an iterative algorithm. For serialization, use `[JsonIgnore]` or `ReferenceHandler.Preserve`:

```csharp
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
```

---

## 5. OutOfMemoryException

**Common causes**

| Cause | Indicator |
|---|---|
| Large object heap fragmentation | LOH size grows in dumps; GC.GetGCMemoryInfo() |
| Unbounded in-memory collections | `List<T>` or `Dictionary` growing without eviction |
| Memory leak from event handlers | Subscribers hold references; publisher never collected |
| Disposing `IMemoryOwner<T>` early and re-using the memory | Heap corruption leading to OOM |

**Quick checks**

```csharp
// Check current memory pressure
var info = GC.GetGCMemoryInfo();
Console.WriteLine($"Heap size: {info.HeapSizeBytes / 1024 / 1024} MB");

// Use dotnet-counters to watch in real time
// dotnet-counters monitor --process-id <pid> System.Runtime
```

**Prevention**

- Use `IAsyncEnumerable<T>` to stream large datasets rather than loading into memory
- Use `ArrayPool<T>.Shared.Rent()` for short-lived byte buffers
- Unsubscribe from events: `publisher.Event -= handler;`
- Use `WeakReference<T>` for cache entries

---

## 6. TaskCanceledException / OperationCanceledException

**Message**: `A task was canceled.` or `The operation was canceled.`

**Causes**

| Cause | Fix |
|---|---|
| Request timeout (`HttpClient.Timeout`) | Increase timeout or handle the exception |
| `CancellationToken` from request context passed to background task | Don't pass request CT to fire-and-forget work; use a separate long-lived token |
| `CancellationTokenSource.CancelAfter()` hit | Expected — handle it at the call site |
| Client disconnected mid-request in ASP.NET Core | Check `HttpContext.RequestAborted` |

**Handling pattern**

```csharp
try
{
    var result = await client.GetAsync(url, cancellationToken);
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    // Expected cancellation — log and exit cleanly
}
catch (OperationCanceledException)
{
    // Timeout from HttpClient — treat as transient, retry or return error
}
```

**Background service anti-pattern**

```csharp
// BAD — request CT cancels when client disconnects, killing background work
_ = DoLongWorkAsync(httpContext.RequestAborted);

// GOOD — use application lifetime token
_ = DoLongWorkAsync(_appLifetime.ApplicationStopping);
// or enqueue to a channel / queue
```

---

## 7. HttpRequestException

**Message**: `Response status code does not indicate success: 404 (Not Found).`

**Causes**

- Wrong base URL or path
- Missing authentication header (401)
- Server-side error (500) — log the response body
- SSL/TLS certificate failure
- DNS resolution failure (connection refused)

**Resilient HttpClient setup (with Polly in .NET 10)**

```csharp
builder.Services.AddHttpClient<IMyApiClient, MyApiClient>(c =>
    c.BaseAddress = new Uri(config["MyApi:BaseUrl"]!))
    .AddStandardResilienceHandler(); // Polly retry + circuit breaker built-in
```

**Logging the response body on failure**

```csharp
var response = await client.GetAsync(url, ct);
if (!response.IsSuccessStatusCode)
{
    var body = await response.Content.ReadAsStringAsync(ct);
    logger.LogError("API call failed {Status}: {Body}", response.StatusCode, body);
    response.EnsureSuccessStatusCode(); // re-throws with context
}
```

---

## 8. DbUpdateConcurrencyException

**Message**: `The database operation was expected to affect 1 row(s), but actually affected 0 row(s).`

**Cause**: A concurrency token (row version or timestamp) detected that another process modified or deleted the row between the read and write.

**Fix — last-write-wins**

```csharp
catch (DbUpdateConcurrencyException ex)
{
    var entry = ex.Entries.Single();
    await entry.ReloadAsync(); // reload from DB
    // re-apply changes and save again
}
```

**Fix — optimistic concurrency with user notification**

```csharp
catch (DbUpdateConcurrencyException)
{
    return Conflict("The record was modified by another user. Please reload and try again.");
}
```

**Setting up a row version (prevents the problem from being silent)**

```csharp
public class Order
{
    public int Id { get; set; }
    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
```

---

## 9. DbUpdateException

**Message**: `An error occurred while saving the entity changes.` (inner exception has the real detail)

**Always read the inner exception** — it contains the actual DB error.

| Inner exception message | Cause | Fix |
|---|---|---|
| `UNIQUE constraint failed` | Duplicate key | Check uniqueness before insert or use upsert |
| `FOREIGN KEY constraint failed` | Missing related entity | Insert parent before child |
| `NOT NULL constraint failed` | Required column is null | Set the property or mark it optional |
| `String or binary data would be truncated` | Value exceeds column max length | Add `[MaxLength(n)]` or truncate input |

```csharp
catch (DbUpdateException ex)
{
    logger.LogError(ex.InnerException, "DB save failed");
    // inspect ex.InnerException.Message for the real cause
}
```

---

## 10. ArgumentException / ArgumentNullException

**Message**: `Value cannot be null. (Parameter 'userId')`

These should be thrown deliberately at the top of public methods as guard clauses.

```csharp
// .NET 10 preferred guard clauses
ArgumentNullException.ThrowIfNull(user);
ArgumentException.ThrowIfNullOrWhiteSpace(name);
ArgumentOutOfRangeException.ThrowIfNegative(count);
ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxCount);
```

If you're *catching* one of these unexpectedly, look for a method call passing null where the API doesn't accept it. Enable nullable reference types to catch these at compile time.

---

## 11. KeyNotFoundException

**Message**: `The given key '42' was not present in the dictionary.`

| Cause | Fix |
|---|---|
| `dict[key]` where key is absent | Use `dict.TryGetValue(key, out var val)` |
| EF Core `.Find()` used as if it guarantees a result | Check for null return |
| Configuration key missing from appsettings | Use `GetRequiredSection()` to fail fast at startup |

```csharp
// BAD
var value = dict[key];

// GOOD
if (!dict.TryGetValue(key, out var value))
    throw new NotFoundException($"Key {key} not found.");
```

---

## 12. FormatException / OverflowException

**Message**: `Input string was not in a correct format.` or `Value was either too large or too small for an Int32.`

**Cause**: Parsing user input or external data with `int.Parse()`, `DateTime.Parse()`, etc., without validation.

```csharp
// BAD
var id = int.Parse(Request.Query["id"]!);

// GOOD
if (!int.TryParse(Request.Query["id"], out var id))
    return BadRequest("Invalid id.");

// With nullable support
int? id = int.TryParse(input, out var parsed) ? parsed : null;
```

---

## 13. JsonException

**Message**: `'0x22' is an invalid start of a value.` or `The JSON value could not be converted to System.Int32.`

| Cause | Fix |
|---|---|
| Response body is not JSON (HTML error page) | Log raw response body before deserializing |
| Case mismatch (`userId` vs `UserId`) | Use `JsonSerializerOptions.PropertyNameCaseInsensitive = true` |
| Missing `[JsonPropertyName]` | Add attribute or configure naming policy |
| Unexpected null in required field | Use `JsonSerializerOptions.RespectNullableAnnotations` (.NET 9+) |

```csharp
// Defensive deserialization
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip
};

try
{
    var result = JsonSerializer.Deserialize<MyDto>(json, options);
}
catch (JsonException ex)
{
    logger.LogError(ex, "Failed to deserialize: {Json}", json);
    throw;
}
```

---

## 14. NotSupportedException in LINQ-to-SQL

**Message**: `The LINQ expression '...' could not be translated. Either rewrite the query in a form that can be translated...`

**Cause**: Using a C# method that EF Core can't convert to SQL (string manipulation, custom methods, client-side evaluation).

```csharp
// BAD — can't be translated to SQL
var results = db.Users.Where(u => MyHelper.IsVip(u)).ToList();

// GOOD option 1 — move logic to SQL via expression
var results = db.Users.Where(u => u.TotalSpend > 1000).ToList();

// GOOD option 2 — load first, filter in memory (only if dataset is small)
var results = db.Users.AsEnumerable().Where(u => MyHelper.IsVip(u)).ToList();
```

---

## 15. CryptographicException

**Message**: `Padding is invalid and cannot be removed.` or `The key is not valid for use in the specified state.`

| Cause | Fix |
|---|---|
| Wrong key or IV used for decryption | Ensure key/IV match the ones used at encryption |
| Key stored in code or config | Use Azure Key Vault, AWS Secrets Manager, or Data Protection API |
| ASP.NET Core Data Protection keys not persisted | Configure `.PersistKeysToFileSystem()` or Key Vault |

For ASP.NET Core cookie/session encryption issues, always configure Data Protection key persistence in production:

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(/* ... */)
    .ProtectKeysWithAzureKeyVault(/* ... */);
```

---

## 16. Thread starvation (no exception thrown)

**Symptoms**: App is alive but slow. Requests time out. No exception in logs.

**Diagnosis**

```bash
# Watch thread pool in real time
dotnet-counters monitor --process-id <pid> System.Runtime \
  --counters threadpool-queue-length,threadpool-thread-count

# Take a dump and look at blocked threads
dotnet-dump collect --process-id <pid>
dotnet-dump analyze
> threadpool   # shows pending work items
> clrthreads   # shows threads in wait state
```

**Common causes**

| Cause | Fix |
|---|---|
| `.Result` / `.Wait()` blocking thread pool threads | Make the full call chain async |
| Too many `Task.Run()` calls for CPU-bound work | Limit parallelism with `SemaphoreSlim` or `Parallel.ForEachAsync` |
| `HttpClient` connections exhausted | Use `IHttpClientFactory`; check `PooledConnectionLifetime` |
| Synchronous DB calls in async controllers | Use `async` EF Core methods throughout |

---

## 17. Memory leak patterns

These don't throw — they cause gradual degradation over hours or days.

| Pattern | Leak mechanism | Fix |
|---|---|---|
| Event handler not removed | Subscriber keeps publisher alive | `publisher.Event -= handler;` in `Dispose()` |
| Static `Dictionary` cache growing unbounded | No eviction policy | Use `IMemoryCache` with size limits and expiry |
| `IDisposable` not disposed | Unmanaged resources accumulate | Use `using` or register with DI as scoped |
| `CancellationTokenSource` not disposed | Timer resources leak | `await using var cts = new CancellationTokenSource()` |
| `HttpClient` created per-request | Socket exhaustion (TIME_WAIT) | Use `IHttpClientFactory` |
| Closures capturing large objects in LINQ | Objects can't be GC'd | Avoid capturing `DbContext`, services, or large buffers in lambdas stored long-term |

**Detection**

```bash
# Take two dumps 10 minutes apart, compare heap
dotnet-dump collect --process-id <pid> -o dump1.dmp
# wait...
dotnet-dump collect --process-id <pid> -o dump2.dmp
dotnet-dump analyze dump2.dmp
> dumpheap -stat   # compare type counts between dumps
```