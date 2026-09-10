#!/usr/bin/env bash

# shellcheck source=common.sh
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)/common.sh"

if (( $# > 1 )); then
  die "Usage: $0 [target-release-sha]"
fi

require_command docker
require_command curl
require_command flock
validate_deploy_environment
acquire_deployment_lock

current_sha="$(read_current_release)"
[[ "$current_sha" =~ ^[0-9a-f]{40}$ ]] \
  || die "Current immutable release is not recorded; refusing an ambiguous rollback."
target_sha="${1:-$(read_previous_release)}"
target_sha="$(normalize_sha "$target_sha")"
[[ "$target_sha" != "$current_sha" ]] || die "Rollback target is already active."

info "APPLICATION-ONLY ROLLBACK"
info "  current: $current_sha"
info "  target:  $target_sha"
info "  database migrations will NOT be reversed"

export IMAGE_TAG="$target_sha"
compose config --quiet
info "Pulling rollback images before changing the running release..."
compose pull api frontend

restore_current_on_failure() {
  local exit_code="$1"
  trap - ERR
  set +e
  info "Rollback target failed health/smoke. Attempting to return application images to $current_sha."
  if switch_application_without_migration "$current_sha" \
    && "$SCRIPT_DIR/smoke.sh" "$current_sha"; then
    info "Original application release is healthy again."
  else
    info "Original release could not be recovered. The schema may be incompatible."
  fi
  info "Database restore was not attempted automatically."
  exit "$exit_code"
}
trap 'restore_current_on_failure $?' ERR

switch_application_without_migration "$target_sha"
"$SCRIPT_DIR/smoke.sh" "$target_sha"
record_release "$target_sha" "$current_sha" ""
trap - ERR

info "Rollback successful: $current_sha -> $target_sha"
info "Database schema was left unchanged."
