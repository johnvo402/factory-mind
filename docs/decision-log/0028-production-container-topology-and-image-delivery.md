# 0028 - Production container topology and image delivery

- **Decision:** Package the API and Angular frontend as separate multi-stage images. Nginx serves the SPA and reverse-proxies same-origin `/api` traffic to the API; PostgreSQL and MinIO stay on the internal Compose network.
- **Secrets:** Production Compose requires database, MinIO, JWT, and Gemini secrets from the deployment environment. Bootstrap Admin values are required only while initializing an empty database. No persistent production secret has a committed fallback.
- **Bootstrap:** An empty production database creates only the configured company/Admin. Once both exist, bootstrap values can be removed and are not read again. Demo machines, materials, inventory, and demo credentials are Development-only.
- **Redis:** Do not deploy Redis yet. Hangfire already uses PostgreSQL and there is no measured cache/session use case; adding an unused stateful service violates the MVP infrastructure rule.
- **Delivery:** CI builds/tests source and publishes immutable API/frontend images to GHCR on `dev`. Deploying those images to a VPS remains a manual environment action until host credentials, TLS/domain, backups, and rollback ownership are supplied.
- **Date:** 2026-08-01
