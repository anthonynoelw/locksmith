# Docker Setup

This directory contains the Docker configuration for building and running Locksmith locally.

## Structure

- **Dockerfile** — multi-stage build parameterized by `PROJECT_NAME`/`PROJECT_PATH`, used to build both `Api` and `Agent` from the same file
- **docker-compose.yml** — local development stack: `api`, `worker` (the Agent), `postgres`, `redis`

## Quick start

```bash
cp .env.example .env   # then edit values if needed
docker-compose -f Docker/docker-compose.yml up --build
```

This builds and starts four services:

| Service | Container name | Built from |
|---|---|---|
| `api` | `locksmith-api` | `Src/Api/Api.csproj` |
| `worker` | `locksmith-worker` | `Src/Agent/Agent.csproj` |
| `postgres` | `locksmith-db` | `postgres:${POSTGRES_VERSION}-alpine` |
| `redis` | `locksmith-redis` | `redis:${REDIS_VERSION}-alpine` |

`api` and `worker` both wait on `postgres` and `redis` passing their healthchecks before starting.

## Configuration (`.env`)

`docker-compose.yml` reads its variables from a `.env` file at the repo root (see `.env.example`). This file is **only** used by Compose — it is not read by `dotnet run` for local development (see the root `CLAUDE.md` for that).

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

## Building a single project manually

```bash
docker build \
  --build-arg PROJECT_NAME=Api \
  --build-arg PROJECT_PATH=Src/Api \
  -t locksmith-api:latest \
  -f Docker/Dockerfile .
```

Swap `PROJECT_NAME=Agent` / `PROJECT_PATH=Src/Agent` to build the worker instead.

## Development tips

### Hot reload

Uncomment the `command` line under the `api` (or `worker`) service in `docker-compose.yml`:

```yaml
services:
  api:
    command: dotnet watch run --project Src/Api/Api.csproj
```

### Running a single service

```bash
docker-compose -f Docker/docker-compose.yml up api
```

## Security notes

- Images run as the non-root user `dotnetapp`.
- `.dockerignore` excludes dev files and secrets from the build context.
- The Dockerfile's `HEALTHCHECK` hits `/health` (the unauthenticated liveness probe).
- No app host — keeps the runtime image portable.
- `Api__BearerToken` and the Postgres/Redis credentials in `.env` are for local development only; never commit a populated `.env` file (it's gitignored) and never reuse the sample values in staging/production — see the root `CLAUDE.md` for how those are set outside Docker Compose.

## Troubleshooting

**"Could not find project"**
- Check `PROJECT_PATH`/`PROJECT_NAME` build args match an actual `Src/<Project>/<Project>.csproj`.
- Ensure the solution file exists at the repo root.

**`api`/`worker` won't start — waiting on `postgres`/`redis`**
- Both services declare `depends_on` with `condition: service_healthy`. Check `docker-compose -f Docker/docker-compose.yml ps` and the healthcheck logs for `postgres`/`redis` first.

**"Port already in use"**
- Change the relevant `*_HOST_PORT` value in `.env`, or override at the CLI:
  ```bash
  docker run -p 8081:8080 locksmith-api:latest
  ```
