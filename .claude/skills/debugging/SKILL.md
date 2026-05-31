---
name: dotnet-debugging
description: >
  Systematically debug C# and .NET 10 applications. Trigger this skill whenever
  the user reports an exception, stack trace, runtime error, NullReferenceException,
  ObjectDisposedException, deadlock, thread starvation, memory leak, high CPU,
  or unexpected behavior. Also use for EF Core query issues, async/await problems,
  gRPC/Minimal API errors, frozen apps, hangs, and any time something "isn't working"
  in a .NET context. Always prefer this skill over ad-hoc guessing.
---

# .NET 10 Debugging

Guide Claude through a structured, root-cause-first debugging process for C# and .NET 10 projects.

## Process

1. **Collect signal** — ask for (or read) the full stack trace, exception message, and relevant code. Never diagnose from a partial error.
2. **Find the origin frame** — locate the top frame inside project code (not framework internals). That is where to start.
3. **Map the dependency chain** — trace upward through the call chain: controller → service → repository → infrastructure.
4. **Inspect state** — what values were null, missing, disposed, or wrong at the failing frame?
5. **Propose a targeted fix** — explain *why* it resolves the root cause, not just what to change.
6. **Add a prevention pattern** — guard clause, retry policy, null-coalescing, lifetime fix, etc.

> Read `references/common-errors.md` before diagnosing — it catalogs the most frequent .NET 10 error patterns with fixes.

---

## Output format

ALWAYS respond with this exact structure:

### Problem
One-sentence plain-English summary of what went wrong.

### Root cause
What actually failed and why — trace it to the specific variable, service, or lifetime issue.

### Fix
The minimal code change needed.

```csharp
// before
// after
```

### Prevention
A pattern, guard, or architectural change that prevents recurrence.

---

## Async / deadlock diagnosis

Deadlocks and thread starvation are the most common hard-to-reproduce bugs in .NET. Follow this checklist:

### Symptoms that suggest a deadlock or async issue
- App freezes or hangs indefinitely (no exception, no response)
- `await` call never returns
- ThreadPool threads exhausted (visible in dotnet-counters or EventPipe)
- `SemaphoreSlim`, `Monitor`, or `lock` held across an `await`

### Step-by-step deadlock diagnosis

1. **Capture a thread dump** — use one of:
   - `dotnet-dump collect --process-id <pid>` then `dotnet-dump analyze`
   - `dotnet-stack report --process-id <pid>` (new in .NET 9+, works in .NET 10)
   - ProcDump + WinDbg on Windows

2. **Look for the blocking call** — search the dump for:
   - `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` on a hot path
   - `lock` or `Monitor.Enter` that is held while an async method awaits
   - `SemaphoreSlim.Wait()` (sync) mixed with `SemaphoreSlim.WaitAsync()`

3. **Check ConfigureAwait usage** — in library code, missing `ConfigureAwait(false)` can cause sync-context deadlocks (legacy ASP.NET, WPF, WinForms). In ASP.NET Core this is less common but still possible with `IHostedService`.

4. **Check for thread pool starvation** — if many tasks are waiting, the thread pool may have no threads to run continuations. Signs: high `ThreadPool.PendingWorkItemCount`, slow responses that improve after warming up.

### Deadlock patterns and fixes

| Pattern | Why it deadlocks | Fix |
|---|---|---|
| `task.Result` inside `async` method | Blocks the thread; continuation can't resume | `await task` instead |
| `lock (obj) { await SomeAsync(); }` | Holds lock across await; another thread can't enter | Use `SemaphoreSlim` with `WaitAsync()` |
| `.Wait()` in ASP.NET Core middleware | Blocks request thread; starves thread pool | Make middleware fully async |
| `Task.Run(() => SomeAsync().Result)` | Wraps the problem, doesn't fix it | Remove the wrapping and `await` correctly |
| Nested `SemaphoreSlim.Wait()` on same thread | Re-entrant wait on a non-re-entrant primitive | Use `AsyncLock` from Nito.AsyncEx or restructure |

### Safe async patterns (use these instead)

```csharp
// BAD — blocks thread, risks deadlock
var result = GetDataAsync().Result;
var result = GetDataAsync().GetAwaiter().GetResult();

// GOOD — fully async
var result = await GetDataAsync();

// BAD — lock held across await
lock (_syncRoot)
{
    var data = await FetchAsync(); // deadlock waiting to happen
}

// GOOD — async-compatible mutual exclusion
private readonly SemaphoreSlim _lock = new(1, 1);

await _lock.WaitAsync();
try
{
    var data = await FetchAsync();
}
finally
{
    _lock.Release();
}

// BAD — fire-and-forget swallows exceptions
_ = DoWorkAsync();

// GOOD — use IHostedService or background queue
await _channel.Writer.WriteAsync(workItem);
```

---

## EF Core diagnosis

- **N+1 queries**: missing `.Include()` or `.ThenInclude()`. Enable `LogTo(Console.WriteLine)` to see generated SQL.
- **DbContext lifetime**: injecting `DbContext` (scoped) into a singleton → `ObjectDisposedException`. Register as scoped; use `IDbContextFactory<T>` in singletons.
- **Tracking vs no-tracking**: read-only queries should use `.AsNoTracking()` for performance.
- **Migration failure**: always check that `Up()` and `Down()` are symmetric. Run `dotnet ef migrations script` to preview SQL before applying.

---

## .NET 10 specific notes

- **NativeAOT**: stack traces may be trimmed. Use `[DynamicallyAccessedMembers]` attributes and check trimming warnings at publish time.
- **Minimal APIs**: exceptions in route handlers surface differently than in controllers — check `IExceptionHandler` registration.
- **Blazor**: lifecycle method exceptions (e.g., in `OnInitializedAsync`) can silently swallow unless `ErrorBoundary` is in place.
- **System.Threading.Channels**: prefer over `ConcurrentQueue` for producer/consumer patterns; deadlocks here usually come from awaiting on a full bounded channel from the same thread that's supposed to drain it.

---

## Reference files

| File | When to read |
|---|---|
| `references/common-errors.md` | Before diagnosing any exception — full catalogue of known patterns |