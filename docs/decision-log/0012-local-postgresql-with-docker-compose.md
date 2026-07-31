# 0012 - Local PostgreSQL with Docker Compose

- **Decision:** Local development runs PostgreSQL 17 through the root `compose.yaml` with a persistent named volume and healthcheck.
- **Decision:** Use the `pgvector/pgvector:0.8.2-pg17-bookworm` image so the required extension binaries are available for the Knowledge sprint without adding a second database later.
- **Reason:** One reproducible database command removes workstation-specific PostgreSQL setup while preserving the PostgreSQL-first architecture.
- **Boundary:** Compose starts only PostgreSQL today. Redis, MinIO, and other services are added when a running feature actually needs them.
- **Configuration:** Development defaults match `appsettings.json`; real or shared environment credentials must be supplied through an ignored `.env` or environment variables.
- **Date:** 2026-07-31
