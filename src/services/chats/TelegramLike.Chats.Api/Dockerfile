FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/services/chats/TelegramLike.Chats.Api/TelegramLike.Chats.Api.csproj", "src/services/chats/TelegramLike.Chats.Api/"]
COPY ["src/services/chats/TelegramLike.Chats.Infrastructure/TelegramLike.Chats.Infrastructure.csproj", "src/services/chats/TelegramLike.Chats.Infrastructure/"]
COPY ["src/services/chats/TelegramLike.Chats.Application/TelegramLike.Chats.Application.csproj", "src/services/chats/TelegramLike.Chats.Application/"]
COPY ["src/services/chats/TelegramLike.Chats.Domain/TelegramLike.Chats.Domain.csproj", "src/services/chats/TelegramLike.Chats.Domain/"]
COPY ["src/TelegramLike.Contracts/TelegramLike.Contracts.csproj", "src/TelegramLike.Contracts/"]
RUN dotnet restore "src/services/chats/TelegramLike.Chats.Api/TelegramLike.Chats.Api.csproj"

COPY . .
WORKDIR /src/src/services/chats/TelegramLike.Chats.Api
RUN dotnet publish "TelegramLike.Chats.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramLike.Chats.Api.dll"]
