#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UI_DIR="$ROOT_DIR/src/NexusWorks.Guardian.UI"
PROJECT_FILE="$UI_DIR/NexusWorks.Guardian.UI.csproj"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS." >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet is required." >&2
  exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
  echo "npm is required." >&2
  exit 1
fi

CONFIGURATION="${CONFIGURATION:-Release}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-$ROOT_DIR/artifacts/macos/$TIMESTAMP}"
APP_BUNDLE_NAME="NexusWorks.Guardian.UI.app"
APP_ZIP_PATH="$ARTIFACTS_DIR/NexusWorks.Guardian-macos-app.zip"

mkdir -p "$ARTIFACTS_DIR"

pushd "$UI_DIR" >/dev/null

if [[ -f package-lock.json ]]; then
  npm ci
else
  npm install
fi

npm run tailwind:build

dotnet publish "$PROJECT_FILE" \
  -f net8.0-maccatalyst \
  -c "$CONFIGURATION" \
  -p:EnableCodeSigning=false \
  -o "$ARTIFACTS_DIR"

popd >/dev/null

UNIVERSAL_APP_BUNDLE="$UI_DIR/bin/$CONFIGURATION/net8.0-maccatalyst/$APP_BUNDLE_NAME"

if [[ -d "$UNIVERSAL_APP_BUNDLE" ]]; then
  ditto -c -k --sequesterRsrc --keepParent "$UNIVERSAL_APP_BUNDLE" "$APP_ZIP_PATH"
fi

echo "macOS publish complete."
echo "Artifacts: $ARTIFACTS_DIR"
find "$ARTIFACTS_DIR" -maxdepth 1 -type f | LC_ALL=C sort
