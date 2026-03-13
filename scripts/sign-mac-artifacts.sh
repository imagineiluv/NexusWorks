#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Release}"
RUNTIME_IDENTIFIER="${RUNTIME_IDENTIFIER:-maccatalyst-arm64}"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-$(find "$ROOT_DIR/artifacts/macos" -mindepth 1 -maxdepth 1 -type d | LC_ALL=C sort | tail -n 1)}"
APP_BUNDLE_PATH="${APP_BUNDLE_PATH:-$ROOT_DIR/src/NexusWorks.Guardian.UI/bin/$CONFIGURATION/net8.0-maccatalyst/$RUNTIME_IDENTIFIER/NexusWorks.Guardian.UI.app}"
APP_SIGN_IDENTITY="${APP_SIGN_IDENTITY:-}"
INSTALLER_SIGN_IDENTITY="${INSTALLER_SIGN_IDENTITY:-}"
ENTITLEMENTS_PATH="${ENTITLEMENTS_PATH:-}"
CODESIGN_KEYCHAIN="${CODESIGN_KEYCHAIN:-}"

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

if [[ -z "$APP_SIGN_IDENTITY" ]]; then
  echo "APP_SIGN_IDENTITY is required." >&2
  exit 1
fi

if ! command -v codesign >/dev/null 2>&1; then
  echo "codesign is required." >&2
  exit 1
fi

if ! command -v productbuild >/dev/null 2>&1; then
  echo "productbuild is required." >&2
  exit 1
fi

UNSIGNED_ZIP_PATH="$(find "$ARTIFACTS_DIR" -maxdepth 1 -type f -name '*.zip' | LC_ALL=C sort | head -n 1)"
UNSIGNED_PKG_PATH="$(find "$ARTIFACTS_DIR" -maxdepth 1 -type f -name '*.pkg' | LC_ALL=C sort | head -n 1)"

if [[ -n "$UNSIGNED_ZIP_PATH" ]]; then
  SIGNED_ZIP_PATH="${SIGNED_ZIP_PATH:-${UNSIGNED_ZIP_PATH%.zip}-signed.zip}"
else
  SIGNED_ZIP_PATH="${SIGNED_ZIP_PATH:-$ARTIFACTS_DIR/NexusWorks.Guardian-macos-app-signed.zip}"
fi

if [[ -n "$UNSIGNED_PKG_PATH" ]]; then
  SIGNED_PKG_PATH="${SIGNED_PKG_PATH:-${UNSIGNED_PKG_PATH%.pkg}-signed.pkg}"
else
  SIGNED_PKG_PATH="${SIGNED_PKG_PATH:-$ARTIFACTS_DIR/NexusWorks.Guardian.UI-signed.pkg}"
fi

codesign_args=(--force --deep --strict --timestamp --options runtime --sign "$APP_SIGN_IDENTITY")

if [[ -n "$ENTITLEMENTS_PATH" ]]; then
  codesign_args+=(--entitlements "$ENTITLEMENTS_PATH")
fi

if [[ -n "$CODESIGN_KEYCHAIN" ]]; then
  codesign_args+=(--keychain "$CODESIGN_KEYCHAIN")
fi

echo "Signing app bundle: $APP_BUNDLE_PATH"
codesign "${codesign_args[@]}" "$APP_BUNDLE_PATH"
codesign --verify --deep --strict --verbose=2 "$APP_BUNDLE_PATH"

echo "Creating signed zip: $SIGNED_ZIP_PATH"
rm -f "$SIGNED_ZIP_PATH"
ditto -c -k --sequesterRsrc --keepParent "$APP_BUNDLE_PATH" "$SIGNED_ZIP_PATH"

if [[ -n "$INSTALLER_SIGN_IDENTITY" ]]; then
  productbuild_args=(--component "$APP_BUNDLE_PATH" /Applications --sign "$INSTALLER_SIGN_IDENTITY")

  if [[ -n "$CODESIGN_KEYCHAIN" ]]; then
    productbuild_args+=(--keychain "$CODESIGN_KEYCHAIN")
  fi

  echo "Creating signed installer: $SIGNED_PKG_PATH"
  rm -f "$SIGNED_PKG_PATH"
  productbuild "${productbuild_args[@]}" "$SIGNED_PKG_PATH"
  pkgutil --check-signature "$SIGNED_PKG_PATH" || true
else
  echo "INSTALLER_SIGN_IDENTITY not set; skipped signed installer package."
fi

echo "macOS signing complete."
echo "Signed zip: $SIGNED_ZIP_PATH"
if [[ -f "$SIGNED_PKG_PATH" ]]; then
  echo "Signed pkg: $SIGNED_PKG_PATH"
fi
