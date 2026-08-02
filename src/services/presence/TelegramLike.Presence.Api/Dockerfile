FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# curl is needed for the docker-compose healthcheck to probe /health/ready.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# The shared layer arrives as versioned packages from the local feed, not as project
# references. Run build/pack-shared.ps1 before building images or restore will not find them.
COPY ["NuGet.config", "Directory.Build.props", "./"]
COPY ["artifacts/packages/", "artifacts/packages/"]
COPY ["src/services/presence/TelegramLike.Presence.Api/TelegramLike.Presence.Api.csproj", "src/services/presence/TelegramLike.Presence.Api/"]
COPY ["src/services/presence/TelegramLike.Presence.Infrastructure/TelegramLike.Presence.Infrastructure.csproj", "src/services/presence/TelegramLike.Presence.Infrastructure/"]
COPY ["src/services/presence/TelegramLike.Presence.Application/TelegramLike.Presence.Application.csproj", "src/services/presence/TelegramLike.Presence.Application/"]
COPY ["src/services/presence/TelegramLike.Presence.Domain/TelegramLike.Presence.Domain.csproj", "src/services/presence/TelegramLike.Presence.Domain/"]
RUN dotnet restore "src/services/presence/TelegramLike.Presence.Api/TelegramLike.Presence.Api.csproj"

COPY . .
WORKDIR /src/src/services/presence/TelegramLike.Presence.Api
RUN dotnet publish "TelegramLike.Presence.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramLike.Presence.Api.dll"]
