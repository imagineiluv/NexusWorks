#!/usr/bin/env bash
set -euo pipefail

LOCAL_DOTNET_ROOT="${LOCAL_DOTNET_ROOT:-$HOME/.dotnet-guardian8}"
DOTNET_VERSION="${DOTNET_VERSION:-8.0.416}"
INSTALL_SCRIPT="${INSTALL_SCRIPT:-/tmp/dotnet-install.sh}"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This script must be run on macOS." >&2
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required." >&2
  exit 1
fi

echo "Installing local .NET SDK $DOTNET_VERSION into $LOCAL_DOTNET_ROOT"

rm -f "$INSTALL_SCRIPT"
curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$INSTALL_SCRIPT"
bash "$INSTALL_SCRIPT" --version "$DOTNET_VERSION" --install-dir "$LOCAL_DOTNET_ROOT"

"$LOCAL_DOTNET_ROOT/dotnet" workload install maccatalyst maui --skip-manifest-update

echo
echo "Local .NET install complete."
echo "Use one of the following:"
echo "  DOTNET_BIN=\"$LOCAL_DOTNET_ROOT/dotnet\" ./scripts/check-mac-dotnet8-prereqs.sh"
echo "  DOTNET_BIN=\"$LOCAL_DOTNET_ROOT/dotnet\" ./scripts/publish-mac.sh"
echo "  export PATH=\"$LOCAL_DOTNET_ROOT:\$PATH\""
echo "  ./scripts/publish-mac.sh"
