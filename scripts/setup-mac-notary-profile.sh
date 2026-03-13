#!/usr/bin/env bash
set -euo pipefail

PROFILE_NAME="${NOTARY_PROFILE:-guardian-notary}"
APPLE_ID="${APPLE_ID:-}"
TEAM_ID="${TEAM_ID:-}"
APP_SPECIFIC_PASSWORD="${APP_SPECIFIC_PASSWORD:-}"
NOTARY_KEYCHAIN="${NOTARY_KEYCHAIN:-}"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS." >&2
  exit 1
fi

if ! command -v xcrun >/dev/null 2>&1; then
  echo "xcrun is required." >&2
  exit 1
fi

if [[ -z "$APPLE_ID" ]]; then
  echo "APPLE_ID is required." >&2
  exit 1
fi

if [[ -z "$TEAM_ID" ]]; then
  echo "TEAM_ID is required." >&2
  exit 1
fi

if [[ -z "$APP_SPECIFIC_PASSWORD" ]]; then
  echo "APP_SPECIFIC_PASSWORD is required." >&2
  exit 1
fi

store_args=(
  "$PROFILE_NAME"
  --apple-id "$APPLE_ID"
  --team-id "$TEAM_ID"
  --password "$APP_SPECIFIC_PASSWORD"
)

if [[ -n "$NOTARY_KEYCHAIN" ]]; then
  store_args+=(--keychain "$NOTARY_KEYCHAIN")
fi

echo "Creating notarytool keychain profile: $PROFILE_NAME"
xcrun notarytool store-credentials "${store_args[@]}"

echo
echo "Profile created. Validate it with:"
echo "  NOTARY_PROFILE=\"$PROFILE_NAME\" ./scripts/check-mac-signing-prereqs.sh"
