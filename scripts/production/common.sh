#!/usr/bin/env bash

set -Eeuo pipefail
umask 077

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd -P)"
COMPOSE_FILE="${COMPOSE_FILE:-$REPO_ROOT/compose.prod.yaml}"
ENV_FILE="${ENV_FILE:-$REPO_ROOT/.env.production}"

info() {
  printf '%s\n' "$*" >&2
}

die() {
  info "ERROR: $*"
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || die "Required command not found: $1"
}

read_env_setting() {
  local key="$1"
  local line value=""
  [[ -f "$ENV_FILE" ]] || die "Environment file not found: $ENV_FILE"

  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    [[ "$line" =~ ^[[:space:]]*(#|$) ]] && continue
    if [[ "$line" =~ ^[[:space:]]*${key}[[:space:]]*=(.*)$ ]]; then
      value="${BASH_REMATCH[1]}"
    fi
  done < "$ENV_FILE"

  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  if [[ ${#value} -ge 2 ]]; then
    if [[ "${value:0:1}" == '"' && "${value: -1}" == '"' ]] \
      || [[ "${value:0:1}" == "'" && "${value: -1}" == "'" ]]; then
      value="${value:1:${#value}-2}"
    fi
  fi
  printf '%s' "$value"
}

load_setting() {
  local key="$1"
  local default_value="${2-}"
  local current_value="${!key-}"
  if [[ -z "$current_value" ]]; then
    current_value="$(read_env_setting "$key")"
  fi
  if [[ -z "$current_value" ]]; then
    current_value="$default_value"
  fi
  export "$key=$current_value"
}

load_production_environment() {
  load_setting POSTGRES_DB factorymind
  load_setting POSTGRES_USER factorymind
  load_setting POSTGRES_PASSWORD
  load_setting MINIO_ROOT_USER
  load_setting MINIO_ROOT_PASSWORD
  load_setting MINIO_BUCKET factorymind
  load_setting HTTP_PORT 80
  load_setting GITHUB_REPOSITORY_OWNER johnvo402
  load_setting APP_PULL_POLICY always
  load_setting FACTORYMIND_STATE_DIR "$REPO_ROOT/.var/releases"
  load_setting BACKUP_ROOT "$REPO_ROOT/.var/backups"
  load_setting BACKUP_RETENTION_COUNT 7
  load_setting MINIO_MC_IMAGE quay.io/minio/mc:RELEASE.2025-08-13T08-35-41Z
  load_setting DEPLOY_HEALTH_TIMEOUT_SECONDS 180
  load_setting SMOKE_CONNECT_TIMEOUT_SECONDS 5
  load_setting SMOKE_REQUEST_TIMEOUT_SECONDS 15
  load_setting SMOKE_RETRY_COUNT 10

  export COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:-factory-mind-prod}"
}

require_value() {
  local key="$1"
  [[ -n "${!key-}" ]] || die "$key is required (value is not printed)."
}

validate_identifier() {
  local value="$1"
  local label="$2"
  [[ "$value" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] \
    || die "$label must contain only letters, digits, and underscores and may not start with a digit."
}

validate_bucket_name() {
  [[ "$1" =~ ^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$ ]] \
    || die "MINIO_BUCKET must be a valid lowercase S3 bucket name."
}

validate_sha() {
  [[ "$1" =~ ^[0-9A-Fa-f]{40}$ ]] \
    || die "Release must be a full 40-character hexadecimal Git SHA."
}

normalize_sha() {
  validate_sha "$1"
  printf '%s' "${1,,}"
}

validate_positive_integer() {
  [[ "$2" =~ ^[1-9][0-9]*$ ]] || die "$1 must be a positive integer."
}

validate_base_environment() {
  load_production_environment
  require_value POSTGRES_PASSWORD
  require_value MINIO_ROOT_USER
  require_value MINIO_ROOT_PASSWORD
  validate_identifier "$POSTGRES_DB" POSTGRES_DB
  validate_identifier "$POSTGRES_USER" POSTGRES_USER
  case "$POSTGRES_DB" in
    postgres|template0|template1)
      die "POSTGRES_DB must not name a PostgreSQL system database."
      ;;
  esac
  validate_bucket_name "$MINIO_BUCKET"
  validate_positive_integer BACKUP_RETENTION_COUNT "$BACKUP_RETENTION_COUNT"
  validate_positive_integer DEPLOY_HEALTH_TIMEOUT_SECONDS "$DEPLOY_HEALTH_TIMEOUT_SECONDS"
  validate_positive_integer SMOKE_CONNECT_TIMEOUT_SECONDS "$SMOKE_CONNECT_TIMEOUT_SECONDS"
  validate_positive_integer SMOKE_REQUEST_TIMEOUT_SECONDS "$SMOKE_REQUEST_TIMEOUT_SECONDS"
  validate_positive_integer SMOKE_RETRY_COUNT "$SMOKE_RETRY_COUNT"
}

validate_deploy_environment() {
  validate_base_environment
  load_setting JWT_KEY
  load_setting GEMINI_API_KEY
  require_value JWT_KEY
  require_value GEMINI_API_KEY
  [[ ${#JWT_KEY} -ge 32 ]] || die "JWT_KEY must contain at least 32 characters."
}

compose() {
  docker compose \
    --project-name "$COMPOSE_PROJECT_NAME" \
    --env-file "$ENV_FILE" \
    -f "$COMPOSE_FILE" \
    "$@"
}

ensure_private_directory() {
  mkdir -p -- "$1"
  chmod 700 -- "$1"
}

acquire_deployment_lock() {
  if [[ "${FACTORYMIND_DEPLOY_LOCK_HELD:-0}" == "1" ]]; then
    return
  fi

  require_command flock
  ensure_private_directory "$FACTORYMIND_STATE_DIR"
  exec {FACTORYMIND_LOCK_FD}>"$FACTORYMIND_STATE_DIR/deployment.lock"
  flock -n "$FACTORYMIND_LOCK_FD" \
    || die "Another deploy, rollback, backup, or restore operation holds the deployment lock."
  export FACTORYMIND_DEPLOY_LOCK_HELD=1
}

read_current_release() {
  local path="$FACTORYMIND_STATE_DIR/current-release"
  local value=""
  if [[ -f "$path" ]]; then
    value="$(tr -d '[:space:]' < "$path")"
  fi
  if [[ "$value" =~ ^[0-9a-f]{40}$ ]]; then
    printf '%s' "$value"
  fi
}

detect_running_release() {
  local recorded running
  running="$(inspect_running_release)"
  if [[ -n "$running" ]]; then
    printf '%s' "$running"
    return
  fi
  recorded="$(read_current_release)"
  printf '%s' "$recorded"
}

inspect_running_release() {
  local container_id value image
  container_id="$(compose ps -q api 2>/dev/null || true)"
  if [[ -z "$container_id" ]]; then
    return
  fi
  value="$(docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$container_id" 2>/dev/null \
    | sed -n 's/^Release__Sha=//p' \
    | head -n 1)"
  if [[ "$value" =~ ^[0-9a-f]{40}$ ]]; then
    printf '%s' "$value"
    return
  fi
  image="$(docker inspect --format '{{.Config.Image}}' "$container_id" 2>/dev/null || true)"
  value="${image##*:}"
  if [[ "$value" =~ ^[0-9a-f]{40}$ ]]; then
    printf '%s' "$value"
  fi
}

read_previous_release() {
  local path="$FACTORYMIND_STATE_DIR/previous-release"
  local value=""
  if [[ -f "$path" ]]; then
    value="$(tr -d '[:space:]' < "$path")"
  fi
  if [[ "$value" =~ ^[0-9a-f]{40}$ ]]; then
    printf '%s' "$value"
  fi
}

json_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//$'\n'/\\n}"
  printf '%s' "$value"
}

record_release() {
  local current_sha="$1"
  local previous_sha="$2"
  local backup_path="$3"
  local deployed_at
  deployed_at="$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
  ensure_private_directory "$FACTORYMIND_STATE_DIR"

  printf '%s\n' "$current_sha" > "$FACTORYMIND_STATE_DIR/.current-release.tmp"
  mv -f -- "$FACTORYMIND_STATE_DIR/.current-release.tmp" "$FACTORYMIND_STATE_DIR/current-release"
  printf '%s\n' "$previous_sha" > "$FACTORYMIND_STATE_DIR/.previous-release.tmp"
  mv -f -- "$FACTORYMIND_STATE_DIR/.previous-release.tmp" "$FACTORYMIND_STATE_DIR/previous-release"
  printf '{\n  "currentReleaseSha": "%s",\n  "previousReleaseSha": "%s",\n  "deployedAt": "%s",\n  "backupPath": "%s"\n}\n' \
    "$current_sha" \
    "$previous_sha" \
    "$deployed_at" \
    "$(json_escape "$backup_path")" \
    > "$FACTORYMIND_STATE_DIR/.last-deployment.json.tmp"
  mv -f -- \
    "$FACTORYMIND_STATE_DIR/.last-deployment.json.tmp" \
    "$FACTORYMIND_STATE_DIR/last-deployment.json"
  chmod 600 -- "$FACTORYMIND_STATE_DIR/current-release" \
    "$FACTORYMIND_STATE_DIR/previous-release" \
    "$FACTORYMIND_STATE_DIR/last-deployment.json"
}

wait_for_service_health() {
  local service="$1"
  local timeout_seconds="${2:-$DEPLOY_HEALTH_TIMEOUT_SECONDS}"
  local deadline=$((SECONDS + timeout_seconds))
  local container_id status="missing"

  while (( SECONDS < deadline )); do
    container_id="$(compose ps -q "$service" 2>/dev/null || true)"
    if [[ -n "$container_id" ]]; then
      status="$(docker inspect --format \
        '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' \
        "$container_id" 2>/dev/null || true)"
      if [[ "$status" == "healthy" || "$status" == "running" ]]; then
        return 0
      fi
      if [[ "$status" == "unhealthy" || "$status" == "exited" || "$status" == "dead" ]]; then
        info "$service entered terminal state: $status"
        compose logs --tail 40 "$service" >&2 || true
        return 1
      fi
    fi
    sleep 3
  done

  info "Timed out waiting ${timeout_seconds}s for $service health (last state: $status)."
  compose logs --tail 40 "$service" >&2 || true
  return 1
}

run_migration() {
  compose rm -sf migrate >/dev/null 2>&1 || true
  compose up \
    --no-deps \
    --no-build \
    --pull never \
    --abort-on-container-exit \
    --exit-code-from migrate \
    migrate
}

start_application() {
  compose up -d --no-deps --no-build --pull never api
  wait_for_service_health api
  compose up -d --no-deps --no-build --pull never frontend
  wait_for_service_health frontend
}

switch_application_without_migration() {
  local release_sha="$1"
  export IMAGE_TAG="$release_sha"
  compose up -d --no-deps --no-build --pull never api
  wait_for_service_health api
  compose up -d --no-deps --no-build --pull never frontend
  wait_for_service_health frontend
}

run_minio_client() {
  local mount_spec="$1"
  shift
  local minio_container
  minio_container="$(compose ps -q minio)"
  [[ -n "$minio_container" ]] || die "MinIO container is not running."

  local docker_args=(
    run --rm
    --network "container:$minio_container"
    -e MINIO_ROOT_USER
    -e MINIO_ROOT_PASSWORD
  )
  if [[ -n "$mount_spec" ]]; then
    docker_args+=(--volume "$mount_spec")
  fi
  # shellcheck disable=SC2016
  docker_args+=(
    --entrypoint /bin/sh
    "$MINIO_MC_IMAGE"
    -c 'set -eu; mc alias set factorymind http://127.0.0.1:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null; exec mc "$@"'
    sh
  )
  docker "${docker_args[@]}" "$@"
}

backup_minio() {
  local destination="$1"
  ensure_private_directory "$destination"
  run_minio_client "" mb --ignore-existing "factorymind/$MINIO_BUCKET" >/dev/null
  run_minio_client "$destination:/backup" \
    mirror --overwrite "factorymind/$MINIO_BUCKET" /backup
}

restore_minio() {
  local source_directory="$1"
  run_minio_client "" mb --ignore-existing "factorymind/$MINIO_BUCKET" >/dev/null
  run_minio_client "$source_directory:/backup:ro" \
    mirror --overwrite --remove /backup "factorymind/$MINIO_BUCKET"
}

restore_postgres() {
  local dump_path="$1"
  case "$POSTGRES_DB" in
    postgres|template0|template1) die "Refusing to replace PostgreSQL system database: $POSTGRES_DB" ;;
  esac

  compose exec -T postgres psql \
    --username "$POSTGRES_USER" \
    --dbname postgres \
    --set ON_ERROR_STOP=1 \
    --command "DROP DATABASE IF EXISTS \"$POSTGRES_DB\" WITH (FORCE);"
  compose exec -T postgres createdb \
    --username "$POSTGRES_USER" \
    --owner "$POSTGRES_USER" \
    "$POSTGRES_DB"
  compose exec -T postgres pg_restore \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --exit-on-error \
    --no-owner \
    --no-privileges \
    < "$dump_path"
}

manifest_value() {
  local manifest="$1"
  local key="$2"
  sed -n \
    "s/^[[:space:]]*\"${key}\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" \
    "$manifest" | head -n 1
}

validate_backup_directory() {
  local backup_directory="$1"
  [[ -d "$backup_directory" ]] || die "Backup directory not found: $backup_directory"
  [[ ! -e "$backup_directory/.backup-in-progress" ]] \
    || die "Backup is incomplete: .backup-in-progress exists."
  [[ -s "$backup_directory/database.dump" ]] || die "database.dump is missing or empty."
  [[ -f "$backup_directory/manifest.json" ]] || die "manifest.json is missing."
  [[ -d "$backup_directory/minio" ]] || die "MinIO backup directory is missing."
  [[ -f "$backup_directory/MINIO_OBJECTS.sha256" ]] \
    || die "MINIO_OBJECTS.sha256 is missing."
  [[ -f "$backup_directory/SHA256SUMS" ]] || die "SHA256SUMS is missing."

  (
    cd -- "$backup_directory"
    sha256sum --check SHA256SUMS >/dev/null
    if [[ -s MINIO_OBJECTS.sha256 ]]; then
      sha256sum --check MINIO_OBJECTS.sha256 >/dev/null
    fi
  ) || die "Backup checksum validation failed."

  local release_sha
  release_sha="$(manifest_value "$backup_directory/manifest.json" releaseSha)"
  validate_sha "$release_sha"
}
