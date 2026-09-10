# 0043 — Explicit production migration and recovery

- **Decision:** A production release is one full Git SHA used by both application images. Normal
  Production API startup does not initialize schema. The same API image supports a one-shot
  `--migrate` mode that applies EF Core migrations, bootstraps the first Admin idempotently, and exits.
- **Deployment:** Linux/Bash scripts serialize operations with `flock` and enforce pull -> verified
  PostgreSQL/MinIO backup -> migration -> health -> smoke. Host-local metadata records current and
  previous SHAs. Failed post-start gates attempt image-only rollback.
- **Recovery:** PostgreSQL uses custom-format `pg_dump`/`pg_restore`; MinIO is mirrored through `mc`.
  Manifests and SHA-256 checksums make partial/corrupt backups invalid. Restore requires `--force` and
  replaces both stores exactly.
- **Compatibility:** Application rollback never reverses database migrations. Schema changes requiring
  rollback overlap must use expand/deploy/contract-later; otherwise the verified pre-deploy backup is
  the explicit operator recovery path.
- **Scope:** This remains a private-network, single-VPS Docker Compose topology. No orchestrator,
  replication, provider-specific offsite storage, or automatic destructive recovery is introduced.
