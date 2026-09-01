# syntax=docker/dockerfile:1
#
# API container — ASP.NET Core backend only. The static site lives in its own
# nginx container (see ../frontend/Dockerfile), which proxies /api/* here.
#
# Build context is the "backend" folder:  docker build -t invitation-platform/api:local ./backend

# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project files first, so "dotnet restore" is cached and re-runs
# just when a .csproj changes — not on every source edit.
COPY Directory.Build.props ./
COPY src/InvitationPlatform.Domain/InvitationPlatform.Domain.csproj             src/InvitationPlatform.Domain/
COPY src/InvitationPlatform.Infrastructure/InvitationPlatform.Infrastructure.csproj src/InvitationPlatform.Infrastructure/
COPY src/InvitationPlatform.Api/InvitationPlatform.Api.csproj                   src/InvitationPlatform.Api/
RUN dotnet restore src/InvitationPlatform.Api/InvitationPlatform.Api.csproj

COPY src/ src/
RUN dotnet publish src/InvitationPlatform.Api/InvitationPlatform.Api.csproj \
    -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is only here so Docker's HEALTHCHECK can hit /health — that status is what
# Docker Desktop shows next to the container and what the web container waits on.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    # Absolute path so it never depends on the working directory; compose mounts a
    # named volume here, otherwise every uploaded file dies with the container.
    Storage__BasePath=/app/media_storage

COPY --from=build /app/publish ./

# APP_UID (1654) is the non-root user baked into the Microsoft base images.
RUN mkdir -p /app/media_storage && chown -R $APP_UID /app/media_storage
USER $APP_UID

EXPOSE 8080

# start-period covers first-boot work: EF migrations plus seeding run before the
# app starts listening, so early probes would otherwise report a false failure.
HEALTHCHECK --interval=15s --timeout=5s --start-period=90s --retries=5 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "InvitationPlatform.Api.dll"]
