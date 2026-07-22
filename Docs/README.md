# Locksmith — Documentation Index

Locksmith is a self-contained ASP.NET Core REST API for secure API key lifecycle management. It issues, lists, validates, retrieves, rotates, and deletes API keys, drives them through a status lifecycle (activate/deactivate/revoke), and manages per-key action permissions — hashing secrets with SHA-256 for lookup and encrypting them at rest with AES-256-GCM under a per-key Argon2id-derived DEK. Per-key rate limiting and an automated expiry job are designed but not yet implemented — see [TODO.md](TODO.md).

### Where to look for what

| I want to understand... | Go to |
|---|---|
| What the system does and how it is structured | `Architecture/overview.md` |
| What endpoints exist and what they return | `Architecture/api-surface.md` |
| Why a specific technical decision was made | `Decisions/` |
| What I learned implementing each concept | `Concepts/` |
| What threats the design defends against | `Security/threat-model.md` |
| What's done vs. still outstanding | `TODO.md` |

### Development tooling

- **Git hooks** — Conventional Commit validation via `commitlint`
- **Code style** — StyleCop.Analyzers with EditorConfig (enforced automatically)
- **Package management** — Centralised NuGet versions via `Directory.Packages.props`
- **Setup automation** — One-command initialization script (`setup.sh` / `setup.bat`)

### DevOps

- **Docker** — Multi-stage Dockerfile with health checks
- **Docker Compose** — Local development environment configuration
- **OpenAPI** — Automatic API documentation with a Scalar UI in development

### Testing

- **Unit** (`Tests/Unit/`) — Isolated logic tests with xUnit, Moq, and FluentAssertions
- **Integration** (`Tests/Integration/`) — HTTP-level API tests with `WebApplicationFactory` (scaffolded, no tests yet)
- **Application** (`Tests/Application/`) — Full in-memory app tests with real middleware via `ApplicationFixture` and `ApplicationTestBase`

## Getting started

### Prerequisites

- **.NET 10 SDK** — [Download](https://dot.net/)
- **Node.js 18+** — For git hooks
- **Docker**

### Setup

1. Clone and initialize:
   ```bash
   git clone <repo-url>
   cd locksmith
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
Scalar UI: `/scalar/v1` (Development only)

### With Docker

```bash
docker-compose -f Docker/docker-compose.yml up
```

## Contributing

All commits must follow [Conventional Commits](https://www.conventionalcommits.org/):

```bash
git commit -m "feat: add user authentication"
git commit -m "fix: correct validation logic"
git commit -m "docs: update setup instructions"
```

The setup script installs hooks that enforce this automatically.

### Documentation expectations

Any change that introduces or alters a design decision must be accompanied by
an ADR in `Docs/Decisions/`. If you are unsure whether your change qualifies,
ask yourself: "Would a reviewer reasonably wonder *why* I did it this way instead
of another?" If yes, write the ADR.

For smaller changes — a bug fix, a refactor that doesn't change behaviour, a
dependency bump — no ADR is needed, but the relevant architecture doc should be
updated if the change affects something it describes.

A `docs:` commit type exists for exactly this purpose. A pull request that
introduces a significant design decision without a corresponding ADR will not
be merged.

## License

GPL — See [LICENSE](../LICENSE)
