# FactoryMind

[![CI](https://github.com/johnvo402/factory-mind/actions/workflows/ci.yml/badge.svg?branch=dev)](https://github.com/johnvo402/factory-mind/actions/workflows/ci.yml)

GitHub Actions validates formatting, builds and tests the .NET backend and Angular frontend, audits production npm dependencies, and publishes deployable API/frontend artifacts for each push and pull request.

## Backend quick start

Start the local PostgreSQL and MinIO containers:

```powershell
docker compose up -d
docker compose ps
```

Run the API from the host:

```powershell
dotnet run --project src/FactoryMind.Api
```

The API applies pending EF Core migrations during startup. Default local database settings are compatible with `compose.yaml`.

To customize credentials or ports, copy `.env.example` to `.env` and keep `ConnectionStrings__FactoryMind` aligned with the Compose values. Never commit `.env` or a real OpenAI API key.

Stop the container without deleting data:

```powershell
docker compose down
```

Delete the local database volume only when a full reset is intentionally required:

```powershell
docker compose down --volumes
```
