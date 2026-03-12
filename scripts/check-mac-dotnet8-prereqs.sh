#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EXPECTED_SDK_VERSION="8.0.416"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS." >&2
  exit 1
fi

if ! command -v "$DOTNET_BIN" >/dev/null 2>&1 && [[ ! -x "$DOTNET_BIN" ]]; then
  echo "dotnet is required. Set DOTNET_BIN if you want to use a non-default SDK path." >&2
  exit 1
fi

pushd "$ROOT_DIR" >/dev/null

CURRENT_SDK_VERSION="$("$DOTNET_BIN" --version)"
WORKLOADS_OUTPUT="$("$DOTNET_BIN" workload list 2>&1 || true)"

echo "Repository: $ROOT_DIR"
echo "global.json: $ROOT_DIR/global.json"
echo "dotnet: $DOTNET_BIN"
echo "Selected SDK: $CURRENT_SDK_VERSION"
echo
echo "Installed workloads for the selected SDK:"
echo "$WORKLOADS_OUTPUT"
echo

if [[ "$CURRENT_SDK_VERSION" != "$EXPECTED_SDK_VERSION" ]]; then
  echo "status: sdk-mismatch"
  echo "expected: $EXPECTED_SDK_VERSION"
  echo "next:"
  echo "  1. Run this script from the repository root so global.json is applied."
  echo "  2. If the selected SDK is still different, install .NET SDK $EXPECTED_SDK_VERSION."
  echo "  3. Or point DOTNET_BIN to a local .NET 8 SDK binary."
  popd >/dev/null
  exit 1
fi

if ! grep -q '^maccatalyst[[:space:]]' <<<"$WORKLOADS_OUTPUT"; then
  echo "status: missing-maccatalyst-workload"
  echo "next:"
  echo "  $DOTNET_BIN workload install maccatalyst maui --skip-manifest-update"
  popd >/dev/null
  exit 1
fi

echo "status: ready"
echo "next:"
echo "  DOTNET_BIN=$DOTNET_BIN ./scripts/publish-mac.sh"

popd >/dev/null
