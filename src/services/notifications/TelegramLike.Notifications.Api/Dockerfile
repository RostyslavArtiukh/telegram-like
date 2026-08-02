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
COPY ["src/services/notifications/TelegramLike.Notifications.Api/TelegramLike.Notifications.Api.csproj", "src/services/notifications/TelegramLike.Notifications.Api/"]
COPY ["src/services/notifications/TelegramLike.Notifications.Infrastructure/TelegramLike.Notifications.Infrastructure.csproj", "src/services/notifications/TelegramLike.Notifications.Infrastructure/"]
COPY ["src/services/notifications/TelegramLike.Notifications.Application/TelegramLike.Notifications.Application.csproj", "src/services/notifications/TelegramLike.Notifications.Application/"]
COPY ["src/services/notifications/TelegramLike.Notifications.Domain/TelegramLike.Notifications.Domain.csproj", "src/services/notifications/TelegramLike.Notifications.Domain/"]
RUN dotnet restore "src/services/notifications/TelegramLike.Notifications.Api/TelegramLike.Notifications.Api.csproj"

COPY . .
WORKDIR /src/src/services/notifications/TelegramLike.Notifications.Api
RUN dotnet publish "TelegramLike.Notifications.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramLike.Notifications.Api.dll"]
