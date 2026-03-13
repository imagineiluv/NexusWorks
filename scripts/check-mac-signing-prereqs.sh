#!/usr/bin/env bash
set -euo pipefail

APP_SIGN_IDENTITY="${APP_SIGN_IDENTITY:-}"
INSTALLER_SIGN_IDENTITY="${INSTALLER_SIGN_IDENTITY:-}"
NOTARY_PROFILE="${NOTARY_PROFILE:-}"
CODESIGN_KEYCHAIN="${CODESIGN_KEYCHAIN:-}"
NOTARY_KEYCHAIN="${NOTARY_KEYCHAIN:-$CODESIGN_KEYCHAIN}"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS." >&2
  exit 1
fi

if ! command -v security >/dev/null 2>&1; then
  echo "security is required." >&2
  exit 1
fi

if ! command -v xcrun >/dev/null 2>&1; then
  echo "xcrun is required." >&2
  exit 1
fi

codesign_cmd=(security find-identity -v -p codesigning)

if [[ -n "$CODESIGN_KEYCHAIN" ]]; then
  codesign_cmd+=("$CODESIGN_KEYCHAIN")
fi

codesign_output="$("${codesign_cmd[@]}" 2>&1 || true)"

echo "Available code-signing identities:"
echo "$codesign_output"
echo

if [[ -n "$APP_SIGN_IDENTITY" ]]; then
  if grep -Fq "$APP_SIGN_IDENTITY" <<<"$codesign_output"; then
    echo "app_identity: ready"
  else
    echo "app_identity: missing"
    exit 1
  fi
else
  echo "app_identity: not-set"
fi

if [[ -n "$INSTALLER_SIGN_IDENTITY" ]]; then
  installer_cmd=(security find-certificate -a -c "$INSTALLER_SIGN_IDENTITY")

  if [[ -n "$CODESIGN_KEYCHAIN" ]]; then
    installer_cmd+=("$CODESIGN_KEYCHAIN")
  fi

  installer_output="$("${installer_cmd[@]}" 2>&1 || true)"

  if [[ "$installer_output" == *"alis"* || "$installer_output" == *"labl"* ]]; then
    echo "installer_identity: ready"
  else
    echo "installer_identity: missing"
    exit 1
  fi
else
  echo "installer_identity: not-set"
fi

if [[ -n "$NOTARY_PROFILE" ]]; then
  notary_args=(history --keychain-profile "$NOTARY_PROFILE")

  if [[ -n "$NOTARY_KEYCHAIN" ]]; then
    notary_args+=(--keychain "$NOTARY_KEYCHAIN")
  fi

  if xcrun notarytool "${notary_args[@]}" >/tmp/nexusworks-guardian-notary-history.log 2>&1; then
    echo "notary_profile: ready"
  else
    echo "notary_profile: missing-or-invalid"
    sed -n '1,40p' /tmp/nexusworks-guardian-notary-history.log
    exit 1
  fi
else
  echo "notary_profile: not-set"
fi

echo
echo "status: ready-for-signing-check"
