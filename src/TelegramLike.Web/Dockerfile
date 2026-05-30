FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/TelegramLike.Web/TelegramLike.Web.csproj", "src/TelegramLike.Web/"]
COPY ["src/TelegramLike.Application/TelegramLike.Application.csproj", "src/TelegramLike.Application/"]
COPY ["src/TelegramLike.Contracts/TelegramLike.Contracts.csproj", "src/TelegramLike.Contracts/"]
COPY ["src/TelegramLike.Domain/TelegramLike.Domain.csproj", "src/TelegramLike.Domain/"]
COPY ["src/TelegramLike.Infrastructure/TelegramLike.Infrastructure.csproj", "src/TelegramLike.Infrastructure/"]
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
