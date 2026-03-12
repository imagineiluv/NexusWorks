#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION="${CONFIGURATION:-Debug}"
REPORT_TITLE="${REPORT_TITLE:-Guardian Sample Dataset}"
OUTPUT_ROOT="${OUTPUT_ROOT:-$ROOT_DIR/sample/guardian/output}"

cd "$ROOT_DIR"

dotnet run \
  --project src/NexusWorks.Guardian.Cli/NexusWorks.Guardian.Cli.csproj \
  -c "$CONFIGURATION" \
  -- \
  --sample \
  --output-root "$OUTPUT_ROOT" \
  --report-title "$REPORT_TITLE"
