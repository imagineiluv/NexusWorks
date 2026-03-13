#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
RUNTIME_IDENTIFIER="${RUNTIME_IDENTIFIER:-maccatalyst-arm64}"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-$(find "$ROOT_DIR/artifacts/macos" -mindepth 1 -maxdepth 1 -type d | LC_ALL=C sort | tail -n 1)}"
APP_BUNDLE_PATH="${APP_BUNDLE_PATH:-$ROOT_DIR/src/NexusWorks.Guardian.UI/bin/$CONFIGURATION/net8.0-maccatalyst/$RUNTIME_IDENTIFIER/NexusWorks.Guardian.UI.app}"
NOTARY_PROFILE="${NOTARY_PROFILE:-}"
NOTARY_LOG_DIR="${NOTARY_LOG_DIR:-$ARTIFACTS_DIR/notary-logs}"
NOTARY_KEYCHAIN="${NOTARY_KEYCHAIN:-}"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS." >&2
  exit 1
fi

if [[ -z "$ARTIFACTS_DIR" || ! -d "$ARTIFACTS_DIR" ]]; then
  echo "ARTIFACTS_DIR is required and must exist." >&2
  exit 1
fi

if [[ ! -d "$APP_BUNDLE_PATH" ]]; then
  echo "APP_BUNDLE_PATH does not exist: $APP_BUNDLE_PATH" >&2
  exit 1
fi

if [[ -z "$NOTARY_PROFILE" ]]; then
  echo "NOTARY_PROFILE is required. Create one with xcrun notarytool store-credentials." >&2
  exit 1
fi

if ! command -v xcrun >/dev/null 2>&1; then
  echo "xcrun is required." >&2
  exit 1
fi

SIGNED_ZIP_PATH="${SIGNED_ZIP_PATH:-$(find "$ARTIFACTS_DIR" -maxdepth 1 -type f -name '*-signed.zip' | LC_ALL=C sort | head -n 1)}"
SIGNED_PKG_PATH="${SIGNED_PKG_PATH:-$(find "$ARTIFACTS_DIR" -maxdepth 1 -type f -name '*-signed.pkg' | LC_ALL=C sort | head -n 1)}"
NOTARIZED_ZIP_PATH="${NOTARIZED_ZIP_PATH:-${SIGNED_ZIP_PATH%-signed.zip}-notarized.zip}"

mkdir -p "$NOTARY_LOG_DIR"

if [[ -n "$SIGNED_ZIP_PATH" && -f "$SIGNED_ZIP_PATH" ]]; then
  submit_zip_args=("$SIGNED_ZIP_PATH" --keychain-profile "$NOTARY_PROFILE" --wait)

  if [[ -n "$NOTARY_KEYCHAIN" ]]; then
    submit_zip_args+=(--keychain "$NOTARY_KEYCHAIN")
  fi

  echo "Submitting signed app zip: $SIGNED_ZIP_PATH"
  xcrun notarytool submit "${submit_zip_args[@]}" | tee "$NOTARY_LOG_DIR/app-zip-submit.log"

  echo "Stapling app bundle: $APP_BUNDLE_PATH"
  xcrun stapler staple "$APP_BUNDLE_PATH"

  echo "Creating notarized zip: $NOTARIZED_ZIP_PATH"
  rm -f "$NOTARIZED_ZIP_PATH"
  ditto -c -k --sequesterRsrc --keepParent "$APP_BUNDLE_PATH" "$NOTARIZED_ZIP_PATH"
fi

if [[ -n "$SIGNED_PKG_PATH" && -f "$SIGNED_PKG_PATH" ]]; then
  submit_pkg_args=("$SIGNED_PKG_PATH" --keychain-profile "$NOTARY_PROFILE" --wait)

  if [[ -n "$NOTARY_KEYCHAIN" ]]; then
    submit_pkg_args+=(--keychain "$NOTARY_KEYCHAIN")
  fi

  echo "Submitting signed installer: $SIGNED_PKG_PATH"
  xcrun notarytool submit "${submit_pkg_args[@]}" | tee "$NOTARY_LOG_DIR/installer-submit.log"

  echo "Stapling installer: $SIGNED_PKG_PATH"
  xcrun stapler staple "$SIGNED_PKG_PATH"
fi

echo "macOS notarization complete."
if [[ -f "$NOTARIZED_ZIP_PATH" ]]; then
  echo "Notarized zip: $NOTARIZED_ZIP_PATH"
fi
if [[ -n "$SIGNED_PKG_PATH" && -f "$SIGNED_PKG_PATH" ]]; then
  echo "Stapled pkg: $SIGNED_PKG_PATH"
fi
