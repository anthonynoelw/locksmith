# Template TODO

Improvements prioritised by impact. Complete **Must Have** items before using this template in a real project.

---

## Must Have

- [x] **Global Exception Handling & Problem Details** — Add a middleware or `IExceptionHandler` that catches unhandled exceptions and returns RFC 9457 `ProblemDetails`. Without this, stack traces leak to API consumers.

- [x] **Health Checks** — Add `/health` and `/health/ready` endpoints via `AspNetCore.HealthChecks.*`. Required by every container orchestrator (Kubernetes, ECS, Azure Container Apps). Docker Compose cannot manage service lifecycle without them.

- [x] **CI/CD Pipeline (GitHub Actions)** — Workflow exists at `.github/workflows/ci.yml` with `dotnet build`, `dotnet test`, and `dotnet publish` steps triggered on pull requests and pushes to main/dev.

- [x] **Integration & Application Test Projects** — Only a unit test project exists. Add:
  - `Tests/Integration/` — WebApplicationFactory-based HTTP-level tests
  - `Tests/Application/` — real in-memory app + real database tests
  
  The test structure set by this template is what every adopting team will follow.

- [x] **Structured Logging (Serilog)** — Default `Microsoft.Extensions.Logging` is insufficient for production. Wire up Serilog with structured output, configurable log levels, and console/file sinks in both Api and Agent.

- [x] **Configuration Validation (Options Pattern)** — Bind all settings via `IOptions<T>` with `ValidateDataAnnotations()` and `ValidateOnStart()`. Misconfigured deployments must fail at startup, not at runtime.

- [x] **API Versioning** — Add `Asp.Versioning.Http` so routes start as `/api/v1/`. Retrofitting versioning into a live API is painful — it must be established from day one.

- [x] **Conventional Commits Changelog Generation** — `commitlint` is already wired. Added `release-please` to auto-generate `CHANGELOG.md` from commit history via GitHub Actions.

- [x] **`CODEOWNERS` File** — Add `.github/CODEOWNERS` as a placeholder. Expected in team repositories and commonly overlooked.
---

## Should Have

Patterns and infrastructure that any real feature will immediately need.

- [ ] **EF Core + DbContext + Migration Infrastructure** — The Infrastructure project is empty. Add EF Core, a base `AppDbContext`, an `IDbContext` interface, and migration scaffolding so adopters can immediately add entities.

- [ ] **CQRS via MediatR** — The Application layer is empty. Wire up MediatR with `IRequest`/`IRequestHandler` base types, a pipeline behavior, and one example command and one example query.

- [ ] **FluentValidation + MediatR Pipeline Behavior** — Add `AbstractValidator<T>` integrated as a MediaR pipeline behavior so validation runs automatically for every command and query.

- [ ] **Repository Pattern Interfaces** — Define `IRepository<T>` and `IUnitOfWork` in Domain; implement them in Infrastructure. Without these, Infrastructure has no contract to fulfil.

- [ ] **OpenTelemetry (Traces + Metrics)** — Add `OpenTelemetry.Extensions.Hosting` with OTLP exporter configured in both Api and Agent. Required for distributed tracing and production observability.

- [ ] **Scalar / Swagger UI** — `Microsoft.AspNetCore.OpenApi` is referenced but only `MapOpenApi()` is called. Add Scalar or Swashbuckle so developers can explore the API immediately on first run.

- [ ] **CORS Configuration** — Add a named CORS policy wired through configuration. Any frontend-connected service will need this immediately.

- [ ] **Rate Limiting** — Add a sliding window policy via `Microsoft.AspNetCore.RateLimiting` (ships in-box in .NET 10). Removing it later is trivial; adding it after launch is not.

- [ ] **`Directory.Build.props` for Shared MSBuild Settings** — Centralise `<Nullable>`, `<ImplicitUsings>`, `<TreatWarningsAsErrors>`, and StyleCop references here. Currently every `.csproj` repeats these properties.

---

## Can Have

- [ ] **JWT Bearer Authentication Scaffolding** — Wire up `AddAuthentication().AddJwtBearer()` with configuration stubs, even with no protected endpoints yet. Teams need this immediately after adopting the template.

- [ ] **Object Mapping (Mapster)** — Add Mapster for DTO ↔ domain mapping with one example mapping config. Preferred over AutoMapper for .NET 10 (no reflection cost at runtime).

- [ ] **Idempotency Middleware** — An `Idempotency-Key` header middleware is a common requirement for payment and order APIs. Easy to include as an opt-in middleware; hard to add after the API is in production.

- [ ] **Outbox Pattern Stub** — If the Agent/Worker project is for event processing, add an outbox table and a hosted service that polls it. Prevents message loss on process restart.

- [ ] **`docker-compose.override.yml`** — Add a local-development override with hot-reload volumes, debug ports, and a local database container. Keeps the base `docker-compose.yml` clean for production.
---
