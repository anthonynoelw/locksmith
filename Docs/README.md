# .NET 10 Project Template

A production-ready project template for building scalable .NET 10 applications with clean architecture, Docker support, and modern development practices. Use this as a starting point for new APIs, background services, or microservices.

## What you get

### Architecture

A proven layered structure that scales with your project:

- **Domain** — Core business logic, entities, and domain rules (no infrastructure dependencies)
- **Application** — Use cases, business workflows, CQRS commands/queries (domain-focused)
- **Infrastructure** — Data access, external services, configuration (technical implementation)
- **Api** — ASP.NET Core web API with OpenAPI documentation
- **Agent** — Background worker service for scheduled or event-driven work

### Development tooling

- **Git hooks** — Conventional Commit validation via `commitlint`
- **Code style** — StyleCop.Analyzers with EditorConfig (enforced automatically)
- **Package management** — Centralised NuGet versions via `Directory.Packages.props`
- **Setup automation** — One-command initialization script (`setup.sh` / `setup.bat`)

### Infrastructure

- **Docker** — Multi-stage Dockerfile with health checks
- **Docker Compose** — Local development environment configuration
- **OpenAPI** — Automatic API documentation in development

### Testing

- **Unit** (`Tests/Unit/`) — Isolated logic tests with xUnit, Moq, and FluentAssertions
- **Integration** (`Tests/Integration/`) — HTTP-level API tests with `WebApplicationFactory` (scaffolded, no tests yet)
- **Application** (`Tests/Application/`) — Full in-memory app tests with real middleware via `ApplicationFixture` and `ApplicationTestBase`

## Getting started

### Prerequisites

- **.NET 10 SDK** — [Download](https://dot.net/)
- **Node.js 18+** — For git hooks
- **Docker** (optional)

### Setup

1. Clone and initialize:
   ```bash
   git clone <repo-url>
   cd dotnet-project-template
   ./setup.sh  # or setup.bat on Windows
   ```

2. Open in your IDE:
   ```bash
   dotnet sln open
   ```

That's it—you're ready to start building.

## Running locally

### Quick start (without Docker)

```bash
# Build
dotnet build

# Run API
dotnet run --project Src/Api/Api.csproj

# Run background worker
dotnet run --project Src/Agent/Agent.csproj
```

API: `http://localhost:5000`  
OpenAPI docs: `/openapi/v1.json` (Development only)

### With Docker

```bash
docker-compose -f Docker/docker-compose.yml up
```

## What's included

✅ **Already configured:**
- Layered architecture with clear separation of concerns
- StyleCop code style enforcement
- Git hooks for Conventional Commits
- Docker and Docker Compose setup
- ASP.NET Core API with OpenAPI support
- Background service template
- HTTPS redirection
- Serilog structured logging (enriched with machine name, environment, and HTTP request context)

❌ **Intentionally left blank** (add as you need):
- Domain models and entities
- API endpoints and business logic
- Worker implementation
- Database schema and migrations

## Contributing

All commits must follow [Conventional Commits](https://www.conventionalcommits.org/):

```bash
git commit -m "feat: add user authentication"
git commit -m "fix: correct validation logic"
git commit -m "docs: update setup instructions"
```

The setup script installs hooks that enforce this automatically.

## License

MIT — See [LICENSE](../LICENSE)
