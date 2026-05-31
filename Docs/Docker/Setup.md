# Docker Setup

This directory contains Docker configuration for building and running your .NET projects.

## Structure

- **Dockerfile** - Multi-stage build supporting multiple projects via build args
- **.dockerignore** - Excludes unnecessary files from the build context
- **docker-compose.yml** - Compose configuration for local development

## Building a Single Project

### From the repository root:

```bash
# Build with default project name
docker build \
  --build-arg PROJECT_NAME=YourProject \
  --build-arg PROJECT_PATH=Src/YourProject \
  -t yourproject-api:latest \
  -f Docker/Dockerfile .
```

### Or with docker-compose:

```bash
# Uses args from docker-compose.yml
docker-compose -f Docker/docker-compose.yml up --build
```

## Building Multiple Projects

If your repository contains multiple projects, define them in `docker-compose.yml`:

```yaml
services:
  api:
    build:
      context: ..
      dockerfile: Docker/Dockerfile
      args:
        PROJECT_NAME: YourProject.Api
        PROJECT_PATH: Src/YourProject.Api
        DOTNET_VERSION: "10.0"

  worker:
    build:
      context: ..
      dockerfile: Docker/Dockerfile
      args:
        PROJECT_NAME: YourProject.Worker
        PROJECT_PATH: Src/YourProject.Worker
        DOTNET_VERSION: "10.0"
```

Then:

```bash
# Build all services
docker-compose -f Docker/docker-compose.yml build

# Run all services
docker-compose -f Docker/docker-compose.yml up

# Run specific service
docker-compose -f Docker/docker-compose.yml up api
```

## Build Arguments

| Arg | Default | Description |
|-----|---------|-------------|
| `PROJECT_NAME` | `YourProject` | The project name (used for .csproj and .dll) |
| `PROJECT_PATH` | `Src/YourProject` | Path to the project directory relative to repo root |
| `DOTNET_VERSION` | `10.0` | .NET SDK/runtime version |

## Expected Project Structure

The Dockerfile assumes the following layout:

```
repository-root/
├── .sln                          # Solution file
├── Src/
│   ├── YourProject/
│   │   ├── YourProject.csproj
│   │   ├── Program.cs
│   │   └── ...
│   └── AnotherProject/
│       ├── AnotherProject.csproj
│       └── ...
├── Tests/                        # (Optional) Test projects
│   └── YourProject.Tests/
└── Docker/
    ├── Dockerfile
    ├── .dockerignore
    └── docker-compose.yml
```

If your structure differs, adjust `PROJECT_PATH` in the build args.

## Development Tips

### Hot Reload
Uncomment the `command` line in `docker-compose.yml` to enable `dotnet watch`:

```yaml
services:
  api:
    command: dotnet watch run --project Src/YourProject/YourProject.csproj
```

### Port Mapping
The Dockerfile exposes port `8080`. Override in compose if needed:

```yaml
services:
  api:
    ports:
      - "5000:8080"  # Host:Container
```

### Environment Variables
Add to the `environment` section in compose or pass at runtime:

```bash
docker run -e ASPNETCORE_ENVIRONMENT=Production yourproject-api:latest
```

## Security Notes

- Images run as non-root user `dotnetapp`
- `.dockerignore` excludes dev files and secrets
- Health checks are built-in
- No app host—ensures portability

## Troubleshooting

**"Could not find project"**
- Check `PROJECT_PATH` matches your actual directory
- Ensure `.sln` file exists at repo root

**"dotnet restore" fails**
- Verify all `.csproj` files are valid XML
- Check NuGet sources in your local nuget.config

**"Port already in use"**
- Change the host port in compose or use `--publish` flag:
  ```bash
  docker run -p 8081:8080 yourproject-api:latest
  ```
