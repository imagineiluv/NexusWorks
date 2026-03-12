#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UI_DIR="$ROOT_DIR/src/NexusWorks.Guardian.UI"
PROJECT_FILE="$UI_DIR/NexusWorks.Guardian.UI.csproj"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS." >&2
  exit 1
fi

if ! command -v "$DOTNET_BIN" >/dev/null 2>&1 && [[ ! -x "$DOTNET_BIN" ]]; then
  echo "dotnet is required. Set DOTNET_BIN if you want to use a non-default SDK path." >&2
  exit 1
fi

if ! command -v npm >/dev/null 2>&1; then
  echo "npm is required." >&2
  exit 1
fi

DOTNET_SDK_VERSION="$("$DOTNET_BIN" --version)"

if [[ "$DOTNET_SDK_VERSION" != 8.* ]]; then
  if [[ "${ALLOW_UNSUPPORTED_DOTNET_SDK:-0}" != "1" ]]; then
    echo ".NET 8 SDK is required for macOS publish. Current SDK: $DOTNET_SDK_VERSION" >&2
    echo "Select .NET 8 with global.json or PATH, set DOTNET_BIN to a local .NET 8 SDK, or set ALLOW_UNSUPPORTED_DOTNET_SDK=1 to bypass this guard." >&2
    exit 1
  fi

  echo "warning: using unsupported SDK $DOTNET_SDK_VERSION for a net8.0 MAUI Mac Catalyst publish." >&2
fi

CONFIGURATION="${CONFIGURATION:-Release}"
RUNTIME_IDENTIFIER="${RUNTIME_IDENTIFIER:-maccatalyst-arm64}"
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

npm run test:hotkeys
npm run tailwind:build

"$DOTNET_BIN" publish "$PROJECT_FILE" \
  -f net8.0-maccatalyst \
  -c "$CONFIGURATION" \
  -r "$RUNTIME_IDENTIFIER" \
  -p:EnableCodeSigning=false \
  -o "$ARTIFACTS_DIR"

popd >/dev/null

PUBLISH_APP_BUNDLE="$UI_DIR/bin/$CONFIGURATION/net8.0-maccatalyst/$RUNTIME_IDENTIFIER/$APP_BUNDLE_NAME"

if [[ -d "$PUBLISH_APP_BUNDLE" ]]; then
  if [[ "${SKIP_LAUNCH_SMOKE_TEST:-0}" != "1" ]]; then
    APP_EXECUTABLE="$PUBLISH_APP_BUNDLE/Contents/MacOS/NexusWorks.Guardian.UI"

    if [[ ! -x "$APP_EXECUTABLE" ]]; then
      echo "Published app executable not found: $APP_EXECUTABLE" >&2
      exit 1
    fi

    open -n "$PUBLISH_APP_BUNDLE"
    sleep 5

    if ! pgrep -f "$APP_EXECUTABLE" >/dev/null 2>&1; then
      echo "Launch smoke test failed: $PUBLISH_APP_BUNDLE did not stay running." >&2
      echo "This usually means the published release app is not launchable on the current SDK/workload combination." >&2
      exit 1
    fi

    pkill -f "$APP_EXECUTABLE" >/dev/null 2>&1 || true
  fi

  ditto -c -k --sequesterRsrc --keepParent "$PUBLISH_APP_BUNDLE" "$APP_ZIP_PATH"
fi

echo "macOS publish complete."
echo "Artifacts: $ARTIFACTS_DIR"
find "$ARTIFACTS_DIR" -maxdepth 1 -type f | LC_ALL=C sort
