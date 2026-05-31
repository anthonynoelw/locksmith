# Build args
ARG PROJECT_NAME=YourProject
ARG PROJECT_PATH=Src/${PROJECT_NAME}
ARG DOTNET_VERSION=10.0

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /Src

# Copy solution file and project files
COPY ["*.slnx", "."]
COPY ["Src/", "Src/"]
COPY ["Tests/", "Tests/"]

# Restore dependencies
ARG PROJECT_NAME
ARG PROJECT_PATH
RUN dotnet restore "${PROJECT_PATH}/${PROJECT_NAME}.csproj"

# Build the application
RUN dotnet build "${PROJECT_PATH}/${PROJECT_NAME}.csproj" -c Release -o /app/build --no-restore

# Publish stage
FROM build AS publish
ARG PROJECT_NAME
ARG PROJECT_PATH
RUN dotnet publish "${PROJECT_PATH}/${PROJECT_NAME}.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-build

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

# Create non-root user
RUN groupadd -r dotnetapp && useradd -r -g dotnetapp dotnetapp

# Copy published application from publish stage
COPY --from=publish /app/publish .

# Set ownership of app files to non-root user
RUN chown -R dotnetapp:dotnetapp /app

# Switch to non-root user
USER dotnetapp

# Expose port (change if your API uses a different port)
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Run the application
ARG PROJECT_NAME
ENTRYPOINT ["dotnet", "${PROJECT_NAME}.dll"]
