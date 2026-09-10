# Production deployment runbook

FactoryMind production targets one Linux VPS running Docker Compose. The release unit is always one
full Git commit SHA shared by the API and frontend images.

```text
pull -> verified backup -> explicit migration -> API -> frontend -> health -> smoke
```

The moving `:prod` image tag is only a convenience pointer. None of the production scripts accepts it
as a release identifier.

## Host prerequisites

- A supported 64-bit Linux distribution with current security updates.
- Docker Engine 27 or newer and Docker Compose v2.24 or newer.
- Bash 5, `curl`, `flock` from util-linux, and GNU coreutils (`sha256sum`).
- Enough free space for PostgreSQL, MinIO, one new image pair, the previous image pair, and at least
  seven local backups.
- Outbound access to `ghcr.io` and the configured Gemini endpoint.
- HTTPS ingress through a reverse proxy, load balancer, or trusted tunnel. Port 80 alone is not a
  secure public internet deployment.

Recommended layout:

```text
/opt/factorymind/app       deployment checkout/files
/opt/factorymind/releases host-local release metadata and deployment lock
/opt/factorymind/backups  sensitive local backups
```

PostgreSQL and MinIO have no published host ports in `compose.prod.yaml`. Only the Nginx frontend is
published; `/api` is proxied to the private API service.

## Initial host setup

```bash
sudo install -d -m 700 /opt/factorymind/{app,releases,backups}
cd /opt/factorymind/app
git clone https://github.com/johnvo402/factory-mind.git .
cp .env.production.example .env.production
chmod 600 .env.production
```

Configure `.env.production` with strong unique values. Required secrets are `POSTGRES_PASSWORD`,
`MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `JWT_KEY`, and `GEMINI_API_KEY`. On an empty database also
set all four `BOOTSTRAP_*` values; the bootstrap password must be at least 12 characters. Set:

```dotenv
FACTORYMIND_STATE_DIR=/opt/factorymind/releases
BACKUP_ROOT=/opt/factorymind/backups
BACKUP_RETENTION_COUNT=7
```

Do not commit this file. Quote values containing shell-significant whitespace because the operations
scripts and Docker Compose both read the file. The scripts never enable `set -x` or print secret
values.

If GHCR packages are private, authenticate with a token limited to `read:packages`:

```bash
printf '%s' "$CR_PAT" | docker login ghcr.io --username YOUR_GITHUB_USER --password-stdin
```

## First deployment

Choose a full 40-character SHA whose CI run and production-image job are green:

```bash
./scripts/production/deploy.sh 0123456789abcdef0123456789abcdef01234567
```

The script validates the environment/SHA, obtains an exclusive `flock`, pulls both immutable images,
starts the private data services, creates a verified pre-migration backup, runs
`dotnet FactoryMind.Api.dll --migrate`, and starts the API only after migration succeeds. Migration
mode creates the schema and the first Company/Admin, then exits. Re-running it is safe.

After the first successful deployment, remove the `BOOTSTRAP_*` values from the host environment.
They are not needed when a Company and User already exist.

## Normal deployment

```bash
cd /opt/factorymind/app
git pull --ff-only
./scripts/production/deploy.sh 89abcdef0123456789abcdef0123456789abcdef
```

The pull occurs before any running container is changed. A failed image pull, invalid environment, or
failed backup leaves the active application untouched. No verified backup means no migration.

Release metadata is written atomically with mode 0600 beneath `FACTORYMIND_STATE_DIR`:

- `current-release`: active immutable SHA.
- `previous-release`: last active immutable SHA.
- `last-deployment.json`: deployment timestamp and pre-deploy backup path.
- `deployment.lock`: host-local exclusive operations lock.

## Health and release verification

The deployment gate waits a bounded time for PostgreSQL, MinIO, API, and frontend health checks.
`smoke.sh` checks the public frontend health endpoint and the API readiness response from inside the
private network. The readiness JSON includes a sanitized `releaseSha`; smoke fails if it differs from
the requested image SHA.

```bash
./scripts/production/smoke.sh 89abcdef0123456789abcdef0123456789abcdef
docker compose --env-file .env.production -f compose.prod.yaml ps
```

## Application rollback

```bash
./scripts/production/rollback.sh
# Or choose an explicit known-good SHA:
./scripts/production/rollback.sh 0123456789abcdef0123456789abcdef01234567
```

Rollback pulls the target API/frontend images before switching containers, leaves the database
untouched, waits for health, and verifies the running SHA. If the target fails, the script attempts to
return to the original application images.

**Application rollback is not database rollback.** EF migrations are never automatically reversed.
Production migrations must follow expand -> deploy -> contract-later when old and new application
versions need overlapping schema compatibility. If a previous image cannot run against the migrated
schema, restore the pre-deploy backup only after an operator explicitly chooses that destructive path.

## Failure handling

| Failure point | Result |
| --- | --- |
| Environment/SHA/image pull | Running release is unchanged |
| Backup | Deployment stops; migration does not run |
| Migration | New API does not start; backup path is reported |
| New health/smoke gate | Image-only rollback is attempted when a previous SHA is known |
| Rollback health | Reported as failed/schema-incompatible; operator chooses restore |

The deploy script reports migration status, application status, rollback result, backup path, active
SHA, and an exact restore command. It never automatically overwrites the database to recover from a
post-migration failure.

## Logs and routine checks

```bash
docker compose --env-file .env.production -f compose.prod.yaml ps
docker compose --env-file .env.production -f compose.prod.yaml logs --tail 200 api
docker compose --env-file .env.production -f compose.prod.yaml logs --tail 200 migrate
docker compose --env-file .env.production -f compose.prod.yaml logs --tail 200 postgres minio

docker system df
docker volume inspect factory-mind-prod_factorymind-postgres-data
docker volume inspect factory-mind-prod_factorymind-minio-data
du -sh /opt/factorymind/backups /opt/factorymind/releases
```

Do not run `docker system prune -a` automatically. The previous SHA images are part of the rollback
path. Monitor volume and backup growth, configure disk alerts, and test recovery regularly.

See [backup-restore.md](backup-restore.md) for the destructive restore procedure and isolated drill.
