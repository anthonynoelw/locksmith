# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build
dotnet build --configuration Release

# Run all tests (Integration and Application tests need a running Docker daemon — Testcontainers)
dotnet test

# Run a specific test project
dotnet test Tests/Unit/Unit.csproj
dotnet test Tests/Integration/Integration.csproj
dotnet test Tests/Application/Tests.Application.csproj

# Run a single test by name
dotnet test Tests/Unit/Unit.csproj --filter "FullyQualifiedName~GlobalExceptionHandlerTests"

# Run the API (needs Postgres + Redis reachable and Api__BearerToken set — see below)
dotnet run --project Src/Api/Api.csproj

# Run the Agent (background worker)
dotnet run --project Src/Agent/Agent.csproj

# Full local stack in containers
cp .env.example .env
docker compose up --build

# First-time setup (installs commitlint hook, restores packages)
./setup.sh
```

## Configuration & Environment Variables

All application settings use the `IOptions<T>` pattern with configuration validation at startup.

| Section | Settings class | Project | Notes |
|---|---|---|---|
| `Api` | `ApiSettings` | Api | `Name` and `BearerToken` both required |
| `Agent` | `AgentSettings` | Agent | `Name` required |
| `Cryptography` | `CryptoSettings` | Application | Argon2id `DegreeOfParallelism`, `MemorySize`, `Iterations` |
| `Cache` | `CacheSettings` | Api | `ApiKeyReadSeconds` — max-age for `[Cacheable]` reads (default 60) |
| `RateLimiting` | `RateLimitSettings` | Application | `Enabled`, `PermitLimit`, `WindowSeconds`, `FailOpen` |
| `ConnectionStrings` | — | Infrastructure | `DefaultConnection` (Postgres), `Redis` — both required at startup |

### Local Development Setup

`appsettings.Development.json` (committed) supplies local connection strings for Postgres and Redis on `localhost`. It deliberately does **not** contain a bearer token, so set one before running:

```bash
docker compose up postgres redis          # backing services only
export Api__BearerToken="dev-token"
dotnet run --project Src/Api/Api.csproj
```

Any setting can be overridden with environment variables (`Section__Key`):
```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=locksmith;Username=postgres;Password=..."
export ConnectionStrings__Redis="localhost:6379"
```

### Docker Compose Setup

**`.env` file** (for Docker Compose only):
- Automatically read by the root `docker-compose.yml`
- Configures database versions, ports, and app environment variables (including `Api__BearerToken`)
- Copy `.env.example` to `.env` and customize:
  ```bash
  cp .env.example .env
  docker compose up --build
  ```

### Production / Staging Deployment

Set environment variables directly (no `.env` files):
```bash
export ConnectionStrings__DefaultConnection="Host=prod-db.example.com;Port=5432;Database=locksmith_prod;Username=postgres;Password=..."
export ConnectionStrings__Redis="redis.example.com:6379"
export Api__BearerToken="your-bearer-token"
export Cryptography__Iterations=8
dotnet run --project Src/Api/Api.csproj
```

Or create `appsettings.Production.json` (keep out of version control):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=prod-db.example.com;...",
    "Redis": "redis.example.com:6379"
  }
}
```

**Summary:**
| Environment | Config source | Notes |
|---|---|---|
| Local dev | `appsettings.Development.json` + `Api__BearerToken` env var | Dev file is committed; contains no secrets |
| Docker | `.env` | Auto-read by `docker-compose.yml`; gitignored |
| Production | Environment variables OR `appsettings.Production.json` | Never commit secrets |

## Architecture

This is a **Clean Architecture** solution targeting **.NET 10** with five projects and two executables.

### Layer dependency order (innermost → outermost)

```
Domain ← Application ← Infrastructure ← Api
                                       ← Agent (separate executable)
```

- **Domain** (`Src/Domain/`) — Entities (`ApiKey`, `ApiKeyStatus`, `ApiKeyAction`, `IdempotencyKey`, all `IAppendOnlyTable`), enums, domain exceptions, and `WellKnown` constants. Pure .NET, no external dependencies.
- **Application** (`Src/Application/`) — Use-case services (key management, `Services/Status/`, `Services/Actions/`), `CryptoService`, commands/DTOs, repository and service interfaces (`IUnitOfWork`, `IRateLimiter`, …), and `CryptoSettings`/`RateLimitSettings`.
- **Infrastructure** (`Src/Infrastructure/`) — EF Core `AppDbContext` (Npgsql) with the append-only guard, migrations, repositories, `UnitOfWork`, Redis cache + `IConnectionMultiplexer`, and `RedisSlidingWindowRateLimiter`. Registered via `AddInfrastructure()`.
- **Api** (`Src/Api/`) — ASP.NET Core 10 MVC controller-based API. Hosts REST endpoints, bearer-token authentication, action filters (key resolution, rate limiting, cache-control), OpenAPI + Scalar, versioning, Serilog, and global exception handling.
- **Agent** (`Src/Agent/`) — Separate Worker Service executable for background jobs. Calls `AddInfrastructure()` like the Api, but shares no other startup code. The `Worker` is currently a placeholder (the key-expiry job is not built yet).

Full detail lives in `Docs/Architecture/overview.md`; endpoint contracts in `Docs/Architecture/api-surface.md`.

### Exception handling pipeline

Domain exceptions in `Src/Domain/Exceptions/` map to RFC 9457 ProblemDetails via `GlobalExceptionHandler` (implements `IExceptionHandler`):

| Exception | HTTP status |
|---|---|
| `NotFoundException` | 404 |
| `ValidationException` | 422 (includes field-level errors) |
| `ConflictException` | 409 |
| `DecryptionFailedException` | 422 |
| `AppendOnlyViolationException` / unhandled | 500 (detail redacted outside Development) |

Model-binding failures also return 422 with the same shape (custom `InvalidModelStateResponseFactory`). A missing/duplicated `X-Api-Key` header returns 400 directly from `ResolveApiKeyFilter`.

### Health check endpoints

Two unversioned, unauthenticated infrastructure endpoints (not under `/api/v{version}/`):

| Endpoint | Probe type | Behavior |
|---|---|---|
| `GET /health` | Liveness | Always `Healthy` while the process is running — no dependency checks (`Predicate = _ => false`) |
| `GET /health/ready` | Readiness | Runs checks tagged `WellKnown.HealthCheckTags.READY` — currently the EF Core `AppDbContext` check and the Redis check |

Both return a JSON body via `UIResponseWriter.WriteHealthCheckUIResponse` (`AspNetCore.HealthChecks.UI.Client`). New readiness checks attach in `ServiceExtensions.AddApiServices()`:
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: new[] { WellKnown.HealthCheckTags.READY });
```

### API versioning

URL-segment versioning (`/api/v{version}/...`). The base controller in `Src/Api/Controllers/Controller.cs` carries the versioned route template and `[Authorize]`; all controllers inherit from it. The three V1 controllers (`Src/Api/Controllers/V1/`) override the route to the shared `api/v{version:apiVersion}/api-key` prefix. Each API version gets its own OpenAPI document via `ApiVersionDocumentTransformer` (one `AddOpenApi("vN", ...)` call per version).

JSON request bodies take enums as **integers** (no `JsonStringEnumConverter` is registered); responses return enum names as strings.

### Configuration Validation

All application settings are bound via `IOptions<T>` with `ValidateDataAnnotations()` and `ValidateOnStart()`:

```csharp
// In ServiceExtensions
builder.Services
    .AddOptions<ApiSettings>()
    .BindConfiguration(WellKnown.ConfigSections.API)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Settings classes live in `Src/<Project>/Settings/` and use `[Required]`, `[Range]`, and other `System.ComponentModel.DataAnnotations` attributes to declare validation rules. The `IOptions<T>` instance is available to any service or controller via constructor injection.

**Important:** All configuration section name constants (`API`, `AGENT`, `CRYPTOGRAPHY`, `CACHE`, `RATE_LIMITING`) are centralized in `Domain/WellKnown.ConfigSections` to avoid magic strings. Do not define `SectionName` constants inside settings classes.

### Logging

Both Api and Agent use **Serilog** configured from `appsettings.json` (Console + rolling File sinks), enriched with log context, machine name, and environment name via `AddSerilogLogging()`. The Api additionally enriches each request with `RequestHost`, `RequestScheme`, and `UserAgent` via `UseSerilogRequestLogging` in `UseApiPipeline()`. Inject `ILogger<T>` in classes — Serilog is the registered provider. Do not add other logging providers, and never log secrets, idempotency keys, or the bearer token.

### Service registration pattern

Extension methods on `IHostApplicationBuilder` keep `Program.cs` clean:
- `AddApiServices()` — controllers, global `ResponseCacheControlFilter`, bearer-token auth, versioning, OpenAPI, ProblemDetails, global exception handler, health checks, settings binding, application services, and the `ResolveApiKeyFilter` / `RateLimitFilter` / `CredentialRateLimitFilter` action filters
- `AddInfrastructure()` — `AppDbContext`, `IUnitOfWork`, repositories, Redis cache/multiplexer, `IRateLimiter` (called by both Api and Agent)
- `AddSerilogLogging()` — Serilog from config (one per executable)
- `AddAgentServices()` — registers `Worker` as a hosted service and binds `AgentSettings`

The middleware pipeline is configured via an extension method on `WebApplication`:
- `UseApiPipeline()` — Serilog request logging, exception handler, OpenAPI + Scalar (Development), HTTPS redirect, authentication, authorization, controllers, health check endpoints

## Tests

Three test projects with distinct responsibilities:

| Project | Purpose | Key infrastructure |
|---|---|---|
| `Tests/Unit/` | Isolated logic with Moq mocks | xUnit + Moq + FluentAssertions |
| `Tests/Integration/` | Infrastructure components against real backing services | Testcontainers Redis (`RedisSlidingWindowRateLimiterTests`) |
| `Tests/Application/` | Full HTTP pipeline, real middleware | `ApplicationFixture` (collection fixture: `WebApplicationFactory` + Testcontainers PostgreSQL), `ApplicationTestBase` base class |

`ApplicationFixture` boots the API once per xUnit collection. Tests that need `HttpClient` or `IServiceProvider` inherit from `ApplicationTestBase`. `RateLimitEndpointTests` uses its own factory with `FakeRateLimiter` / `FakeGetApiKeyBySecretService`.

## Code conventions

Enforced at build time via `.editorconfig` + `StyleCop.Analyzers` + `EnforceCodeStyleInBuild=true`:

- File-scoped namespaces
- Allman braces
- 4-space indentation, 120-char line limit
- No `this.` qualifier
- Private fields: `_camelCase`; constants: `SCREAMING_SNAKE_CASE`; interfaces: `IPascalCase`; type params: `TPascalCase`
- Suppress analyzer warnings via `.editorconfig` (`dotnet_diagnostic.<ID>.severity = none`), never with `#pragma warning disable`

## Commit conventions

**Before every commit, the build and all tests must succeed with no errors or critical warnings.**

Conventional Commits are enforced by a commitlint Git hook installed via `setup.sh`. Release-please uses commit history to auto-generate CHANGELOG and version bumps on merge to `main`.

Run the following before committing:
```bash
dotnet build
dotnet test
```

## Roadmap (Docs/TODO.md)

Key items not yet implemented that new features will likely need:
- Agent key-expiry background job (transition keys past `ExpiresAt` to `Expired`)
- Client-supplied `Idempotency-Key` request deduplication
- OpenTelemetry traces + metrics
- Serilog redaction of the `Authorization` header; per-endpoint request body size limits; CORS
- Database-level append-only privileges

## Important

Concept And Decision Folders are not to be touched!
