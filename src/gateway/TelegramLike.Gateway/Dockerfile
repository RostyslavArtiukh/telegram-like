FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/gateway/TelegramLike.Gateway/TelegramLike.Gateway.csproj", "src/gateway/TelegramLike.Gateway/"]
RUN dotnet restore "src/gateway/TelegramLike.Gateway/TelegramLike.Gateway.csproj"

COPY . .
WORKDIR /src/src/gateway/TelegramLike.Gateway
RUN dotnet publish "TelegramLike.Gateway.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramLike.Gateway.dll"]
