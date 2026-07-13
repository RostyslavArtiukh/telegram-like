FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# curl is used by the compose healthcheck (same as the service images).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/TelegramLike.Web/TelegramLike.Web.csproj", "src/TelegramLike.Web/"]
COPY ["src/client/TelegramLike.Client/TelegramLike.Client.csproj", "src/client/TelegramLike.Client/"]
COPY ["src/TelegramLike.Contracts/TelegramLike.Contracts.csproj", "src/TelegramLike.Contracts/"]
RUN dotnet restore "src/TelegramLike.Web/TelegramLike.Web.csproj"

COPY . .
WORKDIR /src/src/TelegramLike.Web
RUN dotnet publish "TelegramLike.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramLike.Web.dll"]
