#!/usr/bin/env bash

# shellcheck source=common.sh
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)/common.sh"

if (( $# > 1 )); then
  die "Usage: $0 [release-sha]"
fi

require_command docker
require_command sha256sum
require_command flock
validate_base_environment
acquire_deployment_lock
ensure_private_directory "$BACKUP_ROOT"
BACKUP_ROOT="$(cd -- "$BACKUP_ROOT" && pwd -P)"

release_sha="${1:-$(read_current_release)}"
if [[ -z "$release_sha" && "${IMAGE_TAG:-}" =~ ^[0-9A-Fa-f]{40}$ ]]; then
  release_sha="$IMAGE_TAG"
fi
release_sha="$(normalize_sha "$release_sha")"
export IMAGE_TAG="$release_sha"

timestamp="$(date -u +'%Y%m%dT%H%M%SZ')"
final_directory="$BACKUP_ROOT/$timestamp"
if [[ -e "$final_directory" ]]; then
  final_directory="$BACKUP_ROOT/${timestamp}-$$"
fi
in_progress_directory="${final_directory}.in-progress"

cleanup_incomplete() {
  if [[ -n "${in_progress_directory:-}" \
    && -d "$in_progress_directory" \
    && "$(dirname -- "$in_progress_directory")" == "$BACKUP_ROOT" \
    && "$(basename -- "$in_progress_directory")" == *.in-progress ]]; then
    rm -rf -- "$in_progress_directory"
  fi
}
trap cleanup_incomplete EXIT

ensure_private_directory "$in_progress_directory"
touch "$in_progress_directory/.backup-in-progress"

info "Ensuring PostgreSQL and MinIO are healthy before backup..."
compose up -d --no-build postgres minio >&2
wait_for_service_health postgres
wait_for_service_health minio

info "Creating PostgreSQL custom-format dump..."
compose exec -T postgres pg_dump \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --format custom \
  --no-owner \
  --no-privileges \
  > "$in_progress_directory/database.dump"
[[ -s "$in_progress_directory/database.dump" ]] || die "PostgreSQL dump is empty."

info "Mirroring MinIO bucket through the S3 API..."
backup_minio "$in_progress_directory/minio" >&2

schema_migration="uninitialized"
history_table="$(compose exec -T postgres psql \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --tuples-only \
  --no-align \
  --command "SELECT to_regclass('public.\"__EFMigrationsHistory\"');")"
history_table="${history_table//$'\r'/}"
if [[ -n "$history_table" ]]; then
  schema_migration="$(compose exec -T postgres psql \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --tuples-only \
    --no-align \
    --command 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')"
  schema_migration="${schema_migration//$'\r'/}"
fi

created_at="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
printf '{\n  "createdAt": "%s",\n  "releaseSha": "%s",\n  "postgresDatabase": "%s",\n  "minioBucket": "%s",\n  "databaseBackup": "database.dump",\n  "objectBackup": "minio/",\n  "schemaMigration": "%s"\n}\n' \
  "$created_at" \
  "$release_sha" \
  "$POSTGRES_DB" \
  "$MINIO_BUCKET" \
  "$(json_escape "$schema_migration")" \
  > "$in_progress_directory/manifest.json"

(
  cd -- "$in_progress_directory" || exit 1
  find minio -type f -print0 \
    | LC_ALL=C sort -z \
    | xargs -0 -r sha256sum \
    > MINIO_OBJECTS.sha256
  sha256sum database.dump manifest.json MINIO_OBJECTS.sha256 > SHA256SUMS
)

rm -f -- "$in_progress_directory/.backup-in-progress"
mv -- "$in_progress_directory" "$final_directory"
in_progress_directory=""
chmod 700 -- "$final_directory" "$final_directory/minio"
chmod 600 -- \
  "$final_directory/database.dump" \
  "$final_directory/manifest.json" \
  "$final_directory/MINIO_OBJECTS.sha256" \
  "$final_directory/SHA256SUMS"

mapfile -t completed_backups < <(
  find "$BACKUP_ROOT" -mindepth 1 -maxdepth 1 -type d \
    -name '20??????T??????Z*' ! -name '*.in-progress' -printf '%f\n' \
    | LC_ALL=C sort -r
)
for (( index=BACKUP_RETENTION_COUNT; index<${#completed_backups[@]}; index++ )); do
  expired="$BACKUP_ROOT/${completed_backups[$index]}"
  if [[ "$expired" != "$final_directory" && "$(dirname -- "$expired")" == "$BACKUP_ROOT" ]]; then
    info "Removing expired local backup: $expired"
    rm -rf -- "$expired"
  fi
done

trap - EXIT
info "Backup successful: $final_directory"
printf '%s\n' "$final_directory"
