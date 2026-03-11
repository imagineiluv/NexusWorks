# Guardian Publish Scripts

## Files

- `publish-mac.sh`
  - Run on macOS
  - Builds Tailwind assets
  - Publishes `NexusWorks.Guardian.UI` for Mac Catalyst
  - Produces a `.pkg` installer and a zipped `.app` bundle under `artifacts/macos/<timestamp>/`

- `publish-windows.ps1`
  - Run on Windows
  - Builds Tailwind assets
  - Publishes `NexusWorks.Guardian.UI` as an unpackaged, self-contained Windows build
  - Produces a publish folder and a zipped artifact under `artifacts/windows/<timestamp>/`

## Prerequisites

- .NET 8 SDK and MAUI workload
- Node.js and npm

## Usage

### macOS

```bash
./scripts/publish-mac.sh
```

Optional environment variables:

```bash
CONFIGURATION=Release ARTIFACTS_DIR="$PWD/artifacts/macos/manual-run" ./scripts/publish-mac.sh
```

### Windows

```powershell
pwsh -File .\scripts\publish-windows.ps1
```

Optional parameters:

```powershell
pwsh -File .\scripts\publish-windows.ps1 -Configuration Release -Architecture x64
```

## Notes

- The macOS script disables code signing by default so local publish works without a signing identity.
- The Windows script publishes an unpackaged build to avoid certificate setup for MSIX signing.
- If you need signed store distribution, add platform-specific signing and notarization steps separately.
