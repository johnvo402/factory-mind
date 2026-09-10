#!/usr/bin/env bash

# shellcheck source=common.sh
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)/common.sh"

if (( $# > 1 )); then
  die "Usage: $0 [expected-release-sha]"
fi

require_command curl
require_command docker
validate_deploy_environment

expected_sha="${1:-${IMAGE_TAG:-}}"
expected_sha="$(normalize_sha "$expected_sha")"
export IMAGE_TAG="$expected_sha"
base_url="${SMOKE_BASE_URL:-http://127.0.0.1:$HTTP_PORT}"

curl_args=(
  --fail
  --silent
  --show-error
  --connect-timeout "$SMOKE_CONNECT_TIMEOUT_SECONDS"
  --max-time "$SMOKE_REQUEST_TIMEOUT_SECONDS"
  --retry "$SMOKE_RETRY_COUNT"
  --retry-delay 2
  --retry-all-errors
)

info "Smoke: checking frontend health at $base_url/health"
frontend_health="$(curl "${curl_args[@]}" "$base_url/health")"
[[ "$frontend_health" == *healthy* ]] || die "Frontend health response was unexpected."

info "Smoke: checking API readiness and immutable release identity"
api_health="$(compose exec -T api curl \
  --fail \
  --silent \
  --show-error \
  --connect-timeout "$SMOKE_CONNECT_TIMEOUT_SECONDS" \
  --max-time "$SMOKE_REQUEST_TIMEOUT_SECONDS" \
  http://127.0.0.1:8080/health/ready)"
[[ "$api_health" == *'"status":"Healthy"'* ]] || die "API readiness response was not healthy."
[[ "$api_health" == *"\"releaseSha\":\"$expected_sha\""* ]] \
  || die "API is healthy but is not running requested release $expected_sha."

info "Smoke successful for release $expected_sha."
