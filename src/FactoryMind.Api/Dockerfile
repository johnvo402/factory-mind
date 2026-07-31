FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY FactoryMind.sln ./
COPY src/FactoryMind.Api/FactoryMind.Api.csproj src/FactoryMind.Api/
COPY src/FactoryMind.Application/FactoryMind.Application.csproj src/FactoryMind.Application/
COPY src/FactoryMind.Domain/FactoryMind.Domain.csproj src/FactoryMind.Domain/
COPY src/FactoryMind.Infrastructure/FactoryMind.Infrastructure.csproj src/FactoryMind.Infrastructure/
COPY src/FactoryMind.Shared/FactoryMind.Shared.csproj src/FactoryMind.Shared/
RUN dotnet restore src/FactoryMind.Api/FactoryMind.Api.csproj

COPY src src
RUN dotnet publish src/FactoryMind.Api/FactoryMind.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS final
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FactoryMind.Api.dll"]
