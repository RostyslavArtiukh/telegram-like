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

COPY ["src/services/identity/TelegramLike.Identity.Api/TelegramLike.Identity.Api.csproj", "src/services/identity/TelegramLike.Identity.Api/"]
COPY ["src/services/identity/TelegramLike.Identity.Infrastructure/TelegramLike.Identity.Infrastructure.csproj", "src/services/identity/TelegramLike.Identity.Infrastructure/"]
COPY ["src/services/identity/TelegramLike.Identity.Application/TelegramLike.Identity.Application.csproj", "src/services/identity/TelegramLike.Identity.Application/"]
COPY ["src/services/identity/TelegramLike.Identity.Domain/TelegramLike.Identity.Domain.csproj", "src/services/identity/TelegramLike.Identity.Domain/"]
COPY ["src/shared/TelegramLike.Api.ServiceDefaults/TelegramLike.Api.ServiceDefaults.csproj", "src/shared/TelegramLike.Api.ServiceDefaults/"]
RUN dotnet restore "src/services/identity/TelegramLike.Identity.Api/TelegramLike.Identity.Api.csproj"

COPY . .
WORKDIR /src/src/services/identity/TelegramLike.Identity.Api
RUN dotnet publish "TelegramLike.Identity.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramLike.Identity.Api.dll"]
