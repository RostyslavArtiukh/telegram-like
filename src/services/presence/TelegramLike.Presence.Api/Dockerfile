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

COPY ["src/services/presence/TelegramLike.Presence.Api/TelegramLike.Presence.Api.csproj", "src/services/presence/TelegramLike.Presence.Api/"]
COPY ["src/services/presence/TelegramLike.Presence.Infrastructure/TelegramLike.Presence.Infrastructure.csproj", "src/services/presence/TelegramLike.Presence.Infrastructure/"]
COPY ["src/services/presence/TelegramLike.Presence.Application/TelegramLike.Presence.Application.csproj", "src/services/presence/TelegramLike.Presence.Application/"]
COPY ["src/services/presence/TelegramLike.Presence.Domain/TelegramLike.Presence.Domain.csproj", "src/services/presence/TelegramLike.Presence.Domain/"]
COPY ["src/shared/TelegramLike.Api.ServiceDefaults/TelegramLike.Api.ServiceDefaults.csproj", "src/shared/TelegramLike.Api.ServiceDefaults/"]
COPY ["src/shared/TelegramLike.Infrastructure.ServiceDefaults/TelegramLike.Infrastructure.ServiceDefaults.csproj", "src/shared/TelegramLike.Infrastructure.ServiceDefaults/"]
COPY ["src/shared/TelegramLike.Application.ServiceDefaults/TelegramLike.Application.ServiceDefaults.csproj", "src/shared/TelegramLike.Application.ServiceDefaults/"]
COPY ["src/shared/TelegramLike.Domain.ServiceDefaults/TelegramLike.Domain.ServiceDefaults.csproj", "src/shared/TelegramLike.Domain.ServiceDefaults/"]
COPY ["src/TelegramLike.Contracts/TelegramLike.Contracts.csproj", "src/TelegramLike.Contracts/"]
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
