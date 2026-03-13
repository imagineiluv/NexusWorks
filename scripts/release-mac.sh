#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
ARTIFACTS_DIR="${ARTIFACTS_DIR:-$ROOT_DIR/artifacts/macos/$TIMESTAMP}"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
SIGNED_STATUS="false"
NOTARIZED_STATUS="false"

git_metadata() {
  local key="$1"

  if ! command -v git >/dev/null 2>&1 || ! git -C "$ROOT_DIR" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "unknown"
    return
  fi

  case "$key" in
    commit)
      git -C "$ROOT_DIR" rev-parse HEAD
      ;;
    branch)
      git -C "$ROOT_DIR" rev-parse --abbrev-ref HEAD
      ;;
    dirty)
      if [[ -n "$(git -C "$ROOT_DIR" status --porcelain --untracked-files=no)" ]]; then
        echo "true"
      else
        echo "false"
      fi
      ;;
    *)
      echo "unknown"
      ;;
  esac
}

write_manifest() {
  node "$ROOT_DIR/scripts/write-release-manifest.mjs" \
    --artifacts-dir "$ARTIFACTS_DIR" \
    --platform macos \
    --metadata configuration="${CONFIGURATION:-Release}" \
    --metadata runtimeIdentifier="${RUNTIME_IDENTIFIER:-maccatalyst-arm64}" \
    --metadata dotnetVersion="$("$DOTNET_BIN" --version)" \
    --metadata signed="$SIGNED_STATUS" \
    --metadata notarized="$NOTARIZED_STATUS" \
    --metadata gitCommit="$(git_metadata commit)" \
    --metadata gitBranch="$(git_metadata branch)" \
    --metadata gitDirty="$(git_metadata dirty)" \
    --metadata workflow=release-mac
}

write_summary() {
  node "$ROOT_DIR/scripts/write-release-summary.mjs" \
    --artifacts-dir "$ARTIFACTS_DIR"
}

run_release_script_tests() {
  node --test "$ROOT_DIR/scripts/tests/release-scripts.test.mjs"
}

export ARTIFACTS_DIR

run_release_script_tests
"$ROOT_DIR/scripts/publish-mac.sh"

if [[ "${SKIP_SIGNING:-0}" == "1" ]]; then
  write_manifest
  write_summary
  echo "release-mac: signing skipped."
  exit 0
fi

"$ROOT_DIR/scripts/check-mac-signing-prereqs.sh"
"$ROOT_DIR/scripts/sign-mac-artifacts.sh"
SIGNED_STATUS="true"

if [[ "${SKIP_NOTARIZATION:-0}" == "1" ]]; then
  write_manifest
  write_summary
  echo "release-mac: notarization skipped."
  exit 0
fi

"$ROOT_DIR/scripts/notarize-mac-artifacts.sh"
NOTARIZED_STATUS="true"

write_manifest
write_summary
