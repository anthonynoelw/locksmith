# Build args
ARG PROJECT_NAME=YourProject
ARG PROJECT_PATH=Src/${PROJECT_NAME}
ARG DOTNET_VERSION=10.0

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /Src

# Copy solution file and project files
COPY ["*.slnx", "./"]
COPY ["Directory.Build.props", "Directory.Packages.props", "./"]
COPY ["Src/", "Src/"]
COPY ["Tests/", "Tests/"]

# Restore dependencies
ARG PROJECT_NAME
ARG PROJECT_PATH
RUN dotnet restore "${PROJECT_PATH}/${PROJECT_NAME}.csproj"

# Build the application
RUN dotnet build "${PROJECT_PATH}/${PROJECT_NAME}.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
ARG PROJECT_NAME
ARG PROJECT_PATH
RUN dotnet publish "${PROJECT_PATH}/${PROJECT_NAME}.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS final
WORKDIR /app

# curl is used by the HTTP-based healthcheck (see docker-compose.yml); not present in the base image
RUN apk add --no-cache curl

# Create non-root user with minimal privileges
RUN addgroup -S -g 1000 dotnetapp && adduser -S -u 1000 -G dotnetapp dotnetapp

# Copy published application from publish stage
COPY --from=publish /app/publish .

# Set ownership of app files to non-root user with restricted permissions
RUN chown -R dotnetapp:dotnetapp /app && chmod -R u+rX,go-rwx /app

# Switch to non-root user
USER dotnetapp

# Expose port (change if your API uses a different port)
EXPOSE 8080

ARG PROJECT_NAME
ENV PROJECT_NAME=${PROJECT_NAME}

# Health check: process liveness (works for any service, including non-HTTP workers).
# Services that expose an HTTP endpoint (e.g. Api) override this in docker-compose with a real endpoint probe.
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD pgrep -f "${PROJECT_NAME}.dll" > /dev/null || exit 1

# Run the application
ENTRYPOINT ["sh", "-c", "exec dotnet \"${PROJECT_NAME}.dll\""]
