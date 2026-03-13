#!/usr/bin/env bash
set -euo pipefail

CERTIFICATE_P12_BASE64="${CERTIFICATE_P12_BASE64:-}"
CERTIFICATE_P12_PASSWORD="${CERTIFICATE_P12_PASSWORD:-}"
KEYCHAIN_PASSWORD="${KEYCHAIN_PASSWORD:-}"
CODESIGN_KEYCHAIN="${CODESIGN_KEYCHAIN:-${RUNNER_TEMP:-/tmp}/nexusworks-guardian-signing.keychain-db}"
CERTIFICATE_PATH="${CERTIFICATE_PATH:-${RUNNER_TEMP:-/tmp}/nexusworks-guardian-signing.p12}"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS." >&2
  exit 1
fi

if ! command -v security >/dev/null 2>&1; then
  echo "security is required." >&2
  exit 1
fi

if [[ -z "$CERTIFICATE_P12_BASE64" ]]; then
  echo "CERTIFICATE_P12_BASE64 is required." >&2
  exit 1
fi

if [[ -z "$CERTIFICATE_P12_PASSWORD" ]]; then
  echo "CERTIFICATE_P12_PASSWORD is required." >&2
  exit 1
fi

if [[ -z "$KEYCHAIN_PASSWORD" ]]; then
  echo "KEYCHAIN_PASSWORD is required." >&2
  exit 1
fi

echo "Creating temporary keychain: $CODESIGN_KEYCHAIN"
security create-keychain -p "$KEYCHAIN_PASSWORD" "$CODESIGN_KEYCHAIN"
security set-keychain-settings -lut 21600 "$CODESIGN_KEYCHAIN"
security unlock-keychain -p "$KEYCHAIN_PASSWORD" "$CODESIGN_KEYCHAIN"

if echo "$CERTIFICATE_P12_BASE64" | base64 --decode >"$CERTIFICATE_PATH" 2>/dev/null; then
  :
elif echo "$CERTIFICATE_P12_BASE64" | base64 -D >"$CERTIFICATE_PATH" 2>/dev/null; then
  :
else
  echo "Failed to decode CERTIFICATE_P12_BASE64." >&2
  exit 1
fi

echo "Importing signing certificate into temporary keychain"
security import "$CERTIFICATE_PATH" \
  -k "$CODESIGN_KEYCHAIN" \
  -P "$CERTIFICATE_P12_PASSWORD" \
  -f pkcs12 \
  -T /usr/bin/codesign \
  -T /usr/bin/productbuild \
  -T /usr/bin/security

security set-key-partition-list \
  -S apple-tool:,apple:,codesign: \
  -s \
  -k "$KEYCHAIN_PASSWORD" \
  "$CODESIGN_KEYCHAIN"

mapfile -t current_keychains < <(security list-keychains -d user | sed 's/^[[:space:]]*//; s/"//g')
security list-keychains -d user -s "$CODESIGN_KEYCHAIN" "${current_keychains[@]}"

echo
echo "Temporary keychain ready."
echo "CODESIGN_KEYCHAIN=$CODESIGN_KEYCHAIN"
