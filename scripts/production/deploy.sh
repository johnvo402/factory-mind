#!/usr/bin/env bash

# shellcheck source=common.sh
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)/common.sh"

if (( $# != 1 )); then
  die "Usage: $0 <40-character-git-sha>"
fi

require_command docker
require_command curl
require_command flock
require_command mktemp
target_sha="$(normalize_sha "$1")"
validate_deploy_environment
acquire_deployment_lock

current_sha="$(detect_running_release)"
backup_path=""
backup_result_file=""
migration_status="not started"
application_status="not started"
rollback_status="not required"
application_started=0

report_failure() {
  local exit_code="$1"
  trap - ERR
  set +e
  if [[ -n "$backup_result_file" && -f "$backup_result_file" ]]; then
    rm -f -- "$backup_result_file"
  fi

  if (( application_started == 1 )) && [[ "$current_sha" =~ ^[0-9a-f]{40}$ ]]; then
    info "New application failed its health/smoke gate; attempting image-only rollback to $current_sha."
    if switch_application_without_migration "$current_sha" \
      && "$SCRIPT_DIR/smoke.sh" "$current_sha"; then
      rollback_status="successful; previous application is healthy"
      application_status="failed; previous release restored"
    else
      rollback_status="failed or schema-incompatible"
      application_status="failed; operator recovery required"
    fi
  elif (( application_started == 1 )); then
    rollback_status="not possible; no previous immutable SHA is recorded"
  fi

  active_sha="$(inspect_running_release)"
  if [[ -z "$active_sha" ]]; then
    active_sha="$(read_current_release)"
  fi
  info ""
  info "DEPLOYMENT FAILED"
  info "  target SHA:       $target_sha"
  info "  migration:        $migration_status"
  info "  application:      $application_status"
  info "  rollback:         $rollback_status"
  info "  backup:           ${backup_path:-not created}"
  info "  active SHA:       ${active_sha:-unknown}"
  if [[ -n "$backup_path" ]]; then
    info "  recovery command: $SCRIPT_DIR/restore.sh '$backup_path' --force"
  else
    info "  recovery command: inspect Compose logs; no schema change was attempted without a backup"
  fi
  exit "$exit_code"
}
trap 'report_failure $?' ERR

export IMAGE_TAG="$target_sha"
info "Validating immutable release $target_sha..."
compose config --quiet

info "Pulling target images before changing the running release..."
compose pull migrate api frontend

info "Ensuring stateful services are healthy..."
compose up -d --no-build postgres minio
wait_for_service_health postgres
wait_for_service_health minio

backup_release="$current_sha"
if [[ -z "$backup_release" ]]; then
  backup_release="$target_sha"
fi
info "Creating mandatory pre-deploy backup..."
backup_result_file="$(mktemp "$FACTORYMIND_STATE_DIR/.backup-result.XXXXXX")"
if ! FACTORYMIND_DEPLOY_LOCK_HELD=1 \
  "$SCRIPT_DIR/backup.sh" "$backup_release" > "$backup_result_file"; then
  report_failure 1
fi
backup_path="$(tail -n 1 "$backup_result_file")"
rm -f -- "$backup_result_file"
backup_result_file=""
if [[ ! -d "$backup_path" ]]; then
  info "Backup script did not return a valid backup directory."
  false
fi

migration_status="running"
info "Running the one-shot EF Core migration/bootstrap service..."
run_migration
migration_status="successful"

application_status="starting"
application_started=1
info "Starting API and frontend for $target_sha..."
start_application
"$SCRIPT_DIR/smoke.sh" "$target_sha"
application_status="healthy"

record_release "$target_sha" "$current_sha" "$backup_path"
trap - ERR

info ""
info "DEPLOYMENT SUCCESSFUL"
info "  active SHA:  $target_sha"
info "  previous SHA: ${current_sha:-none}"
info "  migration:   $migration_status"
info "  backup:      $backup_path"
