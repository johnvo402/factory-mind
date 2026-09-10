#!/usr/bin/env bash

# shellcheck source=common.sh
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)/common.sh"

backup_directory=""
force=0
requested_release=""
while (( $# > 0 )); do
  case "$1" in
    --force) force=1 ;;
    --release)
      shift
      (( $# > 0 )) || die "--release requires a full Git SHA."
      requested_release="$1"
      ;;
    -*) die "Unknown option: $1" ;;
    *)
      [[ -z "$backup_directory" ]] || die "Only one backup directory may be provided."
      backup_directory="$1"
      ;;
  esac
  shift
done

[[ -n "$backup_directory" ]] || die "Usage: $0 <backup-directory> --force [--release <sha>]"
(( force == 1 )) || die "DESTRUCTIVE OPERATION refused. Re-run with --force to overwrite production data."

require_command docker
require_command curl
require_command sha256sum
require_command flock
validate_deploy_environment
acquire_deployment_lock
backup_directory="$(cd -- "$backup_directory" 2>/dev/null && pwd -P)" \
  || die "Backup directory not found: $backup_directory"
validate_backup_directory "$backup_directory"

manifest_release="$(manifest_value "$backup_directory/manifest.json" releaseSha)"
target_sha="${requested_release:-$manifest_release}"
target_sha="$(normalize_sha "$target_sha")"
current_sha="$(read_current_release)"
export IMAGE_TAG="$target_sha"

info "DESTRUCTIVE OPERATION: restoring PostgreSQL and MinIO"
info "  backup: $backup_directory"
info "  release after restore: $target_sha"
compose config --quiet

info "Pulling matching application images before stopping writers..."
compose pull migrate api frontend
compose up -d --no-build postgres minio
wait_for_service_health postgres
wait_for_service_health minio

restore_failed() {
  local exit_code="$1"
  trap - ERR
  info "RESTORE FAILED. Application writers may remain stopped."
  info "Backup remains available at: $backup_directory"
  info "Inspect: docker compose --env-file '$ENV_FILE' -f '$COMPOSE_FILE' logs postgres minio migrate api"
  exit "$exit_code"
}
trap 'restore_failed $?' ERR

info "Stopping API and frontend writers..."
compose stop api frontend >/dev/null 2>&1 || true

info "Replacing PostgreSQL database contents from custom-format dump..."
restore_postgres "$backup_directory/database.dump"

info "Replacing MinIO bucket contents through the S3 API..."
restore_minio "$backup_directory/minio" >&2

info "Running idempotent migration/bootstrap for the selected release..."
run_migration
start_application
"$SCRIPT_DIR/smoke.sh" "$target_sha"
record_release "$target_sha" "$current_sha" "$backup_directory"
trap - ERR

info "Restore successful. Active release: $target_sha"
info "Restored backup: $backup_directory"
