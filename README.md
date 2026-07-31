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

To customize credentials or ports, copy `.env.example` to `.env` and keep `ConnectionStrings__FactoryMind` aligned with the Compose values. Set a newly generated `GEMINI_API_KEY` locally and never commit `.env` or a real API key.

Stop the container without deleting data:

```powershell
docker compose down
```

Delete the local database volume only when a full reset is intentionally required:

```powershell
docker compose down --volumes
```

## Production container build

Create a deployment `.env` from `.env.example`, set the database, MinIO, JWT, Gemini, and initial bootstrap Admin values, then validate and start the production topology:

```powershell
docker compose -f compose.prod.yaml config
docker compose -f compose.prod.yaml up -d --build
docker compose -f compose.prod.yaml ps
```

Only the Nginx frontend port is published. It serves Angular and proxies `/api` to the private API container. PostgreSQL and MinIO data live in named volumes; back up those volumes before upgrades. TLS/domain setup and VPS rollout are environment-specific and intentionally not automated without deployment credentials and a rollback owner.

On an empty production database, `BOOTSTRAP_*` creates the first Admin and company. Production rejects the development JWT key, bootstrap passwords shorter than 12 characters, and missing provider/infrastructure secrets. After the first successful startup, remove the `BOOTSTRAP_*` values from the deployment environment; subsequent starts do not require them once a company and user exist. Never commit the deployment `.env`.
