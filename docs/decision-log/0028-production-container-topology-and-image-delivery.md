# 0028 - Production container topology and image delivery

- **Decision:** Package the API and Angular frontend as separate multi-stage images. Nginx serves the SPA and reverse-proxies same-origin `/api` traffic to the API; PostgreSQL and MinIO stay on the internal Compose network.
- **Secrets:** Production Compose requires database, MinIO, JWT, and Gemini secrets from the deployment environment. Bootstrap Admin values are required only while initializing an empty database. No persistent production secret has a committed fallback.
- **Bootstrap:** An empty production database creates only the configured company/Admin. Once both exist, bootstrap values can be removed and are not read again. Demo machines, materials, inventory, and demo credentials are Development-only.
- **Redis:** Do not deploy Redis yet. Hangfire already uses PostgreSQL and there is no measured cache/session use case; adding an unused stateful service violates the MVP infrastructure rule.
- **Delivery:** CI builds/tests source on `main` and publishes API/frontend images to GHCR with both the immutable commit SHA and the moving `prod` tag. Production Compose is registry-only: it has no local build context, requires `IMAGE_TAG`, pulls both application images from GHCR, and must be started with `--no-build`. Operators deploy and roll back with an exact commit SHA; the moving `prod` tag is not used as the normal deployment version.
- **Runtime guard:** Production Compose hard-codes `ASPNETCORE_ENVIRONMENT=Production`; a dotenv file cannot silently switch the API back to Development.
- **Date:** 2026-08-01
- **Amended:** 2026-09-07 to make GHCR the only Production application-image source.
