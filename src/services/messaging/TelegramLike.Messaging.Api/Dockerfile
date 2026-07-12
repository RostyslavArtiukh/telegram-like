FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/services/messaging/TelegramLike.Messaging.Api/TelegramLike.Messaging.Api.csproj", "src/services/messaging/TelegramLike.Messaging.Api/"]
COPY ["src/services/messaging/TelegramLike.Messaging.Infrastructure/TelegramLike.Messaging.Infrastructure.csproj", "src/services/messaging/TelegramLike.Messaging.Infrastructure/"]
COPY ["src/services/messaging/TelegramLike.Messaging.Application/TelegramLike.Messaging.Application.csproj", "src/services/messaging/TelegramLike.Messaging.Application/"]
COPY ["src/services/messaging/TelegramLike.Messaging.Domain/TelegramLike.Messaging.Domain.csproj", "src/services/messaging/TelegramLike.Messaging.Domain/"]
COPY ["src/shared/TelegramLike.Api.ServiceDefaults/TelegramLike.Api.ServiceDefaults.csproj", "src/shared/TelegramLike.Api.ServiceDefaults/"]
COPY ["src/shared/TelegramLike.Infrastructure.ServiceDefaults/TelegramLike.Infrastructure.ServiceDefaults.csproj", "src/shared/TelegramLike.Infrastructure.ServiceDefaults/"]
COPY ["src/shared/TelegramLike.Application.ServiceDefaults/TelegramLike.Application.ServiceDefaults.csproj", "src/shared/TelegramLike.Application.ServiceDefaults/"]
COPY ["src/shared/TelegramLike.Domain.ServiceDefaults/TelegramLike.Domain.ServiceDefaults.csproj", "src/shared/TelegramLike.Domain.ServiceDefaults/"]
COPY ["src/TelegramLike.Contracts/TelegramLike.Contracts.csproj", "src/TelegramLike.Contracts/"]
RUN dotnet restore "src/services/messaging/TelegramLike.Messaging.Api/TelegramLike.Messaging.Api.csproj"

COPY . .
WORKDIR /src/src/services/messaging/TelegramLike.Messaging.Api
RUN dotnet publish "TelegramLike.Messaging.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramLike.Messaging.Api.dll"]
