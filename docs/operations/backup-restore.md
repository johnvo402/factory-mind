# Backup and restore runbook

FactoryMind backups protect against application and operator errors on one VPS. They do **not** protect
against VPS, disk, account, or site loss. Copy completed backups to encrypted offsite storage using a
host-specific process after `backup.sh` succeeds. Limit access because the database dump and objects
contain business data.

## Create a backup

Standalone backup uses the current SHA recorded by a successful deploy:

```bash
./scripts/production/backup.sh
```

During deployment, the target/current SHA is passed explicitly. The script:

1. Obtains the same exclusive operations lock used by deploy/rollback/restore.
2. Waits for PostgreSQL and MinIO health.
3. Creates a PostgreSQL custom-format `pg_dump` through the production database container.
4. Mirrors the configured MinIO bucket using the supported `mc` S3 API client, never raw volume files.
5. Writes metadata and deterministic checksums.
6. Atomically renames the `.in-progress` directory only after every step succeeds.
7. Keeps the newest `BACKUP_RETENTION_COUNT` successful local backups (default seven).

Layout:

```text
20260910T130000Z/
  database.dump
  minio/
  manifest.json
  MINIO_OBJECTS.sha256
  SHA256SUMS
```

`manifest.json` records creation time, release SHA, database, bucket, artifact paths, and the EF release
responsible for schema state. It never contains credentials. `SHA256SUMS` protects the database dump,
manifest, and object checksum manifest; `MINIO_OBJECTS.sha256` verifies every mirrored object.

Manual validation:

```bash
cd /opt/factorymind/backups/20260910T130000Z
sha256sum --check SHA256SUMS
sha256sum --check MINIO_OBJECTS.sha256  # skip only when the file is empty
pg_restore --list database.dump >/dev/null
```

The deployment backup is never removed by retention during that backup operation. A failed/partial
directory retains `.backup-in-progress` or is removed and is never accepted by `restore.sh`.

## Restore decision

> **DESTRUCTIVE OPERATION:** restore replaces the entire configured PostgreSQL database and makes the
> configured MinIO bucket exactly match the backup. Existing rows and extra objects are deleted.

Use restore only for confirmed data corruption/operator error, or when a post-migration release cannot
be recovered by a schema-compatible application rollback. Prefer `rollback.sh` when only application
images need to change.

Before restore:

- Confirm the backup was copied off the affected disk when possible.
- Confirm its manifest release is available in GHCR and compatible with the intended schema.
- Stop unrelated writers/integrations.
- Announce downtime and identify the operator responsible for validation.
- Keep a copy of the current broken state if forensic investigation is required.

## Restore

Without `--force`, the command always refuses:

```bash
./scripts/production/restore.sh /opt/factorymind/backups/20260910T130000Z
```

Authorized destructive restore:

```bash
./scripts/production/restore.sh \
  /opt/factorymind/backups/20260910T130000Z \
  --force
```

By default the application release comes from the verified manifest. An operator may explicitly choose
a compatible immutable release:

```bash
./scripts/production/restore.sh \
  /opt/factorymind/backups/20260910T130000Z \
  --force \
  --release 0123456789abcdef0123456789abcdef01234567
```

Before stopping writers, the script validates the directory, completion marker, expected artifacts,
all checksums, manifest SHA, Compose configuration, service availability, and target image pulls. It
then stops API/frontend, drops and recreates the non-system application database, runs `pg_restore`,
mirrors MinIO with removal enabled, runs the idempotent migration/bootstrap mode, starts the selected
release, and applies the health/smoke gate.

Restore failure does not silently claim recovery and does not attempt a database downgrade. It reports
that writers may remain stopped, preserves the backup, and provides log commands.

## Isolated restore drill

Run regularly and after changing backup tooling:

```bash
./scripts/production/verify-backup-restore.sh
```

The drill creates an isolated Compose project and disposable volumes, seeds a PostgreSQL row and MinIO
object, invokes the real production backup path, deletes both values, restores using the shared restore
functions, asserts both values returned, then destroys only the isolated volumes. It never reads
`.env.production` and never targets the production Compose project.

For a manual staging drill, additionally log in, upload a disposable PDF, create a backup, replace the
staging volumes, restore, verify the API data and downloaded object, and run `smoke.sh` with the manifest
SHA. Never perform a destructive drill against production.

## Offsite extension point

After a local backup succeeds and checksum validation passes, a host-specific scheduled process may
copy the completed directory to encrypted object storage or another secured host. Preserve
`manifest.json`, both checksum files, and directory structure. Monitor copy failures and periodically
restore from the offsite copy. No cloud vendor is embedded in repository scripts because credentials,
retention, legal requirements, and destination are deployment-specific.
