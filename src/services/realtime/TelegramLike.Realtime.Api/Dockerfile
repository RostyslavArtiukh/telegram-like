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
COPY ["src/services/realtime/TelegramLike.Realtime.Api/TelegramLike.Realtime.Api.csproj", "src/services/realtime/TelegramLike.Realtime.Api/"]
RUN dotnet restore "src/services/realtime/TelegramLike.Realtime.Api/TelegramLike.Realtime.Api.csproj"

COPY . .
WORKDIR /src/src/services/realtime/TelegramLike.Realtime.Api
RUN dotnet publish "TelegramLike.Realtime.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramLike.Realtime.Api.dll"]
