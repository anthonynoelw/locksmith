# Docker Setup

How to build and run Locksmith in containers for local development.

## Files

- **`Docker/Dockerfile`** — multi-stage build parameterized by `PROJECT_NAME`/`PROJECT_PATH`, used to build both `Api` and `Agent` from the same file
- **`docker-compose.yml`** (repo root) — local development stack: `api`, `worker` (the Agent), `postgres`, `redis`
- **`.env.example`** (repo root) — template for the `.env` file Compose reads

## Quick start

Run from the repository root:

```bash
cp .env.example .env   # then edit values if needed
docker compose up --build
```

This builds and starts four services:

| Service | Container name | Built from / image |
|---|---|---|
| `api` | `locksmith-api` | `Src/Api/Api.csproj` |
| `worker` | `locksmith-worker` | `Src/Agent/Agent.csproj` |
| `postgres` | `locksmith-db` | `postgres:${POSTGRES_VERSION}-alpine` |
| `redis` | `locksmith-redis` | `redis:${REDIS_VERSION}-alpine` |

`api` and `worker` both wait for `postgres` and `redis` to pass their healthchecks before starting. Both run with `ASPNETCORE_ENVIRONMENT=Development`, so the OpenAPI document and Scalar UI are available at `http://localhost:8080/openapi/v1.json` and `http://localhost:8080/scalar/v1`.

Postgres and Redis data persist in the named volumes `postgres_data` and `redis_data`. Remove them with `docker compose down -v`.

### Only the backing services

To run the API/Agent with `dotnet run` on your machine but keep Postgres and Redis in containers:

```bash
docker compose up postgres redis
```

The default `appsettings.Development.json` connection strings already point at `localhost:5432` / `localhost:6379`.

## Configuration (`.env`)

`docker-compose.yml` reads its variables from a `.env` file at the repo root (see `.env.example`). This file is **only** used by Compose — it is not read by `dotnet run` (see [.claude/CLAUDE.md](../../.claude/CLAUDE.md) for local configuration).

| Variable | Purpose |
|---|---|
| `POSTGRES_VERSION`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` | Postgres image version and credentials |
| `POSTGRES_HOST_PORT` / `POSTGRES_CONTAINER_PORT` | Port mapping for Postgres (default `5432:5432`) |
| `REDIS_VERSION` | Redis image version |
| `REDIS_HOST_PORT` / `REDIS_CONTAINER_PORT` | Port mapping for Redis (default `6379:6379`) |
| `API_HOST_PORT` / `API_CONTAINER_PORT` | Port mapping for the API (default `8080:8080`) |
| `ConnectionStrings__DefaultConnection` | EF Core connection string, using `postgres` as the hostname (the Compose service name, not `localhost`) |
| `ConnectionStrings__Redis` | Redis connection string, using `redis` as the hostname |
| `Api__BearerToken` | Static bearer token the API validates on every management request — change this for anything beyond local dev |

Any other setting (`Cryptography__*`, `RateLimiting__*`, `Cache__*`) can be added to `.env` the same way; otherwise the values from `appsettings.json` apply.

## Building a single image manually

```bash
docker build \
  --build-arg PROJECT_NAME=Api \
  --build-arg PROJECT_PATH=Src/Api \
  -t locksmith-api:latest \
  -f Docker/Dockerfile .
```

Swap `PROJECT_NAME=Agent` / `PROJECT_PATH=Src/Agent` to build the worker instead.

## Health checks

- The Dockerfile's `HEALTHCHECK` is a **process check** (`pgrep -f "${PROJECT_NAME}.dll"`), so it works for the non-HTTP worker too.
- In `docker-compose.yml`, the `api` service overrides it with an HTTP probe: `curl -f http://localhost:${API_CONTAINER_PORT}/health` (the unauthenticated liveness endpoint). `curl` is installed in the runtime image for this purpose.

## Development tips

### Hot reload

The compiled image does not support `dotnet watch`. For hot reload, run the API with `dotnet watch` on the host against the containerized backing services:

```bash
docker compose up -d postgres redis
dotnet watch run --project Src/Api/Api.csproj
```

### Running a single service

```bash
docker compose up api
```

## Security notes

- Images run as the non-root user `dotnetapp` (UID 1000), with app files owned by that user and unreadable to others.
- The build context is the repo root. There is **no `.dockerignore`** yet, so everything not excluded by Docker defaults (including a local `.env`) is sent to the build context. It is not copied into the image (the Dockerfile only copies `Src/`, `Tests/`, and the solution/props files), but adding a `.dockerignore` is recommended.
- The runtime image is built with `UseAppHost=false`, so the container runs `dotnet <Project>.dll` instead of a native host executable.
- `Api__BearerToken` and the Postgres/Redis credentials in `.env` are for local development only. Never commit a populated `.env` file (it's gitignored), and never reuse the sample values in staging/production — see [.claude/CLAUDE.md](../../.claude/CLAUDE.md) for how those are set outside Docker Compose.
- Compose publishes the Postgres and Redis ports to the host for convenience. Don't do that outside local development.

## Troubleshooting

**"Could not find project"**
- Check that the `PROJECT_PATH`/`PROJECT_NAME` build args match an actual `Src/<Project>/<Project>.csproj`.

**`api`/`worker` won't start — waiting on `postgres`/`redis`**
- Both services declare `depends_on` with `condition: service_healthy`. Check `docker compose ps` and the healthcheck logs for `postgres`/`redis` first.

**API exits immediately with an options validation error**
- A required setting is missing — usually `Api__BearerToken` in `.env`.

**"Port already in use"**
- Change the relevant `*_HOST_PORT` value in `.env`.
