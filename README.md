# Locksmith

Locksmith is a self-contained ASP.NET Core REST API for secure API key lifecycle management. It issues, lists, validates, retrieves, rotates, and deletes API keys, drives them through a status lifecycle (activate/deactivate/revoke), and manages per-key action permissions — hashing secrets with SHA-256 for lookup and encrypting them at rest with AES-256-GCM under a per-key Argon2id-derived DEK. Every key-targeting endpoint is rate limited through a Redis-backed sliding window.

## Documentation

| I want to understand... | Go to |
|---|---|
| What Locksmith is and why it exists | [Docs/what-is-locksmith.md](Docs/what-is-locksmith.md) |
| How the system is structured | [Docs/Architecture/overview.md](Docs/Architecture/overview.md) |
| What endpoints exist and what they return | [Docs/Architecture/api-surface.md](Docs/Architecture/api-surface.md) |
| How the database is laid out | [Docs/Architecture/database.md](Docs/Architecture/database.md) |
| Why a specific technical decision was made | [Docs/Decisions/](Docs/Decisions/) |
| What I learned implementing each concept | [Docs/Concepts/](Docs/Concepts/) |
| What threats the design defends against | [Docs/Security/threat-model.md](Docs/Security/threat-model.md) |
| How to run it with Docker | [Docs/Docker/Setup.md](Docs/Docker/Setup.md) |
| What's done vs. still outstanding | [Docs/TODO.md](Docs/TODO.md) |
| Conventions for contributors and AI assistants | [.claude/CLAUDE.md](.claude/CLAUDE.md) |

## Tooling at a glance

- **Git hooks** — Conventional Commit validation via `commitlint` (installed by `setup.sh` / `setup.bat`)
- **Code style** — StyleCop.Analyzers with `.editorconfig`, enforced at build time
- **Package management** — Centralised NuGet versions via `Directory.Packages.props`
- **Releases** — release-please generates `CHANGELOG.md` and version bumps from commit history
- **Docker** — Multi-stage `Docker/Dockerfile` (builds both Api and Agent) and a root `docker-compose.yml`
- **OpenAPI** — Generated OpenAPI document plus a Scalar UI in Development

### Tests

| Project | What it covers | Needs Docker |
|---|---|---|
| `Tests/Unit/` | Isolated logic with xUnit, Moq, and FluentAssertions | No |
| `Tests/Integration/` | `RedisSlidingWindowRateLimiter` against a real Redis via Testcontainers | Yes |
| `Tests/Application/` | Full HTTP pipeline via `WebApplicationFactory` against a Testcontainers PostgreSQL, using `ApplicationFixture` and `ApplicationTestBase` | Yes |

## Getting started

### Prerequisites

- **.NET 10 SDK** — [Download](https://dot.net/)
- **Node.js 18+** — for the commit-message git hook
- **Docker** — for PostgreSQL/Redis locally and for the Integration/Application tests

### Setup

```bash
git clone <repo-url>
cd locksmith
./setup.sh        # or setup.bat on Windows — installs the commitlint hook and restores packages
```

Then open `Solution.slnx` in your IDE.

## Running locally

### Without Docker (API and Agent on your machine)

You need PostgreSQL and Redis reachable — for example `docker compose up postgres redis`. Connection strings and the bearer token come from `appsettings.Development.json` or environment variables (see [.claude/CLAUDE.md](.claude/CLAUDE.md#configuration--environment-variables)).

```bash
dotnet build
dotnet run --project Src/Api/Api.csproj      # API
dotnet run --project Src/Agent/Agent.csproj  # background worker
```

- API: `http://localhost:5000`
- OpenAPI document: `/openapi/v1.json` (Development only)
- Scalar UI: `/scalar/v1` (Development only)

### With Docker Compose

```bash
cp .env.example .env   # edit values if needed
docker compose up --build
```

The API listens on `http://localhost:8080` (`API_HOST_PORT`). See [Docs/Docker/Setup.md](Docs/Docker/Setup.md) for details.

### Running tests

```bash
dotnet test                                   # everything (Docker must be running)
dotnet test Tests/Unit/Unit.csproj            # unit tests only, no Docker needed
```

## Contributing

All commits must follow [Conventional Commits](https://www.conventionalcommits.org/):

```bash
git commit -m "feat: add user authentication"
git commit -m "fix: correct validation logic"
git commit -m "docs: update setup instructions"
```

The hook installed by the setup script enforces this. Build and tests must pass before every commit.

### Documentation expectations

Any change that introduces or alters a design decision must be accompanied by an ADR in `Docs/Decisions/`. If you are unsure whether your change qualifies, ask yourself: "Would a reviewer reasonably wonder *why* I did it this way instead of another?" If yes, write the ADR.

For smaller changes — a bug fix, a refactor that doesn't change behaviour, a dependency bump — no ADR is needed, but the relevant architecture doc should be updated if the change affects something it describes.

A `docs:` commit type exists for exactly this purpose. A pull request that introduces a significant design decision without a corresponding ADR will not be merged.

## License

GPL — see [LICENSE](LICENSE).
