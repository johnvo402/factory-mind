#!/usr/bin/env bash

# shellcheck source=common.sh
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)/common.sh"

if (( $# != 0 )); then
  die "Usage: $0"
fi

require_command docker
require_command sha256sum
require_command flock
require_command mktemp

drill_root="$(mktemp -d "${TMPDIR:-/tmp}/factorymind-restore-drill.XXXXXX")"
COMPOSE_PROJECT_NAME="factorymind-drill-$$"
COMPOSE_FILE="$REPO_ROOT/compose.prod.yaml"
ENV_FILE="$drill_root/.env.production"
BACKUP_ROOT="$drill_root/backups"
FACTORYMIND_STATE_DIR="$drill_root/releases"
IMAGE_TAG="0123456789abcdef0123456789abcdef01234567"
APP_PULL_POLICY=never
POSTGRES_DB=factorymind_drill
POSTGRES_USER=factorymind_drill
POSTGRES_PASSWORD=factorymind-drill-postgres-password
MINIO_ROOT_USER=factorymind-drill-user
MINIO_ROOT_PASSWORD=factorymind-drill-password
MINIO_BUCKET=factorymind-drill
JWT_KEY=factorymind-drill-jwt-key-longer-than-thirty-two-characters
GEMINI_API_KEY=ci-placeholder-key
BACKUP_RETENTION_COUNT=2
FACTORYMIND_DEPLOY_LOCK_HELD=1
export COMPOSE_PROJECT_NAME COMPOSE_FILE ENV_FILE BACKUP_ROOT FACTORYMIND_STATE_DIR
export IMAGE_TAG APP_PULL_POLICY POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD
export MINIO_ROOT_USER MINIO_ROOT_PASSWORD MINIO_BUCKET JWT_KEY GEMINI_API_KEY
export BACKUP_RETENTION_COUNT FACTORYMIND_DEPLOY_LOCK_HELD

printf '%s\n' \
  "POSTGRES_DB=$POSTGRES_DB" \
  "POSTGRES_USER=$POSTGRES_USER" \
  "POSTGRES_PASSWORD=$POSTGRES_PASSWORD" \
  "MINIO_ROOT_USER=$MINIO_ROOT_USER" \
  "MINIO_ROOT_PASSWORD=$MINIO_ROOT_PASSWORD" \
  "MINIO_BUCKET=$MINIO_BUCKET" \
  "JWT_KEY=$JWT_KEY" \
  "GEMINI_API_KEY=$GEMINI_API_KEY" \
  "IMAGE_TAG=$IMAGE_TAG" \
  "APP_PULL_POLICY=$APP_PULL_POLICY" \
  "HTTP_PORT=18081" \
  > "$ENV_FILE"
chmod 600 "$ENV_FILE"
validate_base_environment

cleanup_drill() {
  local exit_code="$?"
  trap - EXIT
  set +e
  compose down -v --remove-orphans >/dev/null 2>&1
  if [[ -n "${drill_root:-}" \
    && -d "$drill_root" \
    && "$(basename -- "$drill_root")" == factorymind-restore-drill.* ]]; then
    rm -rf -- "$drill_root"
  fi
  exit "$exit_code"
}
trap cleanup_drill EXIT

info "Restore drill: starting isolated PostgreSQL and MinIO..."
compose up -d --no-build postgres minio
wait_for_service_health postgres
wait_for_service_health minio

compose exec -T postgres psql \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set ON_ERROR_STOP=1 \
  --command "CREATE TABLE recovery_drill (id integer PRIMARY KEY, payload text NOT NULL); INSERT INTO recovery_drill VALUES (1, 'database-restored');"

printf 'object-restored\n' > "$drill_root/probe.txt"
run_minio_client "$drill_root/probe.txt:/probe.txt:ro" \
  mb --ignore-existing "factorymind/$MINIO_BUCKET" >/dev/null
run_minio_client "$drill_root/probe.txt:/probe.txt:ro" \
  cp /probe.txt "factorymind/$MINIO_BUCKET/probe.txt" >/dev/null

info "Restore drill: creating production-format backup..."
backup_directory="$("$SCRIPT_DIR/backup.sh" "$IMAGE_TAG")"
validate_backup_directory "$backup_directory"

compose exec -T postgres psql \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set ON_ERROR_STOP=1 \
  --command "DELETE FROM recovery_drill;"
run_minio_client "" rm --force "factorymind/$MINIO_BUCKET/probe.txt" >/dev/null

info "Restore drill: replacing modified data from backup..."
restore_postgres "$backup_directory/database.dump"
restore_minio "$backup_directory/minio" >/dev/null

database_value="$(compose exec -T postgres psql \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --tuples-only \
  --no-align \
  --command "SELECT payload FROM recovery_drill WHERE id = 1;")"
database_value="${database_value//$'\r'/}"
[[ "$database_value" == "database-restored" ]] \
  || die "Restore drill did not recover the PostgreSQL row."

object_value="$(run_minio_client "" cat "factorymind/$MINIO_BUCKET/probe.txt")"
object_value="${object_value//$'\r'/}"
[[ "$object_value" == "object-restored" ]] \
  || die "Restore drill did not recover the MinIO object."

info "Restore drill successful: PostgreSQL row and MinIO object recovered."
