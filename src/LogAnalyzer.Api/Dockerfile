# syntax=docker/dockerfile:1
# Build context: the repository root (see docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so this layer stays cached until a project file or package version changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/LogAnalyzer.Core/LogAnalyzer.Core.csproj src/LogAnalyzer.Core/
COPY src/LogAnalyzer.Infrastructure/LogAnalyzer.Infrastructure.csproj src/LogAnalyzer.Infrastructure/
COPY src/LogAnalyzer.Api/LogAnalyzer.Api.csproj src/LogAnalyzer.Api/
RUN dotnet restore src/LogAnalyzer.Api/LogAnalyzer.Api.csproj

COPY src/LogAnalyzer.Core/ src/LogAnalyzer.Core/
COPY src/LogAnalyzer.Infrastructure/ src/LogAnalyzer.Infrastructure/
COPY src/LogAnalyzer.Api/ src/LogAnalyzer.Api/
RUN dotnet publish src/LogAnalyzer.Api/LogAnalyzer.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# curl backs the Docker Compose health check; the .NET runtime images do not include it.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# Run as the non-root user that the .NET images provide.
USER $APP_UID

# ASP.NET Core images listen on port 8080 by default.
EXPOSE 8080
ENTRYPOINT ["dotnet", "LogAnalyzer.Api.dll"]
