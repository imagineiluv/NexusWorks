# Guardian Publish Scripts

## Files

- `publish-mac.sh`
  - Run on macOS
  - Verifies UI hotkey mappings with `npm run test:hotkeys`
  - Builds Tailwind assets
  - Publishes `NexusWorks.Guardian.UI` for Mac Catalyst
  - Produces a `.pkg` installer and a zipped `.app` bundle under `artifacts/macos/<timestamp>/`

- `publish-windows.ps1`
  - Run on Windows
  - Verifies UI hotkey mappings with `npm run test:hotkeys`
  - Builds Tailwind assets
  - Publishes `NexusWorks.Guardian.UI` as an unpackaged, self-contained Windows build
  - Produces a publish folder and a zipped artifact under `artifacts/windows/<timestamp>/`
- `check-windows-dotnet8-prereqs.ps1`
  - Run on Windows
  - Verifies that the repository is using `.NET 8.0.416`
  - Verifies that the `.NET 8` `maui` workload is installed
  - Prints the exact command to install missing workloads for the selected `dotnet`
- `check-mac-dotnet8-prereqs.sh`
  - Run on macOS
  - Verifies that the repository is using `.NET 8.0.416`
  - Verifies that the `.NET 8` `maccatalyst` workload is installed
  - Prints the exact command to install missing workloads for the selected `dotnet`
- `install-local-dotnet8-maccatalyst.sh`
  - Run on macOS
  - Installs a user-local `.NET 8.0.416` SDK under `~/.dotnet-guardian8` by default
  - Installs `maccatalyst` and `maui` workloads without requiring system-level SDK changes
  - Prints the exact `DOTNET_BIN=...` commands to use afterward
- `run-guardian-sample.sh`
  - Run on macOS or Linux
  - Executes `NexusWorks.Guardian.Cli` against `sample/guardian`
  - Writes HTML, Excel, JSON, and log outputs under `sample/guardian/output/guardian/<timestamp>/`
- `run-guardian-sample.ps1`
  - Run on Windows
  - Executes `NexusWorks.Guardian.Cli` against `sample/guardian`
  - Writes HTML, Excel, JSON, and log outputs under `sample/guardian/output/guardian/<timestamp>/`

## Prerequisites

- .NET 8 SDK and MAUI workload
- Node.js and npm

## Usage

### macOS

```bash
./scripts/install-local-dotnet8-maccatalyst.sh
./scripts/check-mac-dotnet8-prereqs.sh
./scripts/publish-mac.sh
```

Optional environment variables:

```bash
CONFIGURATION=Release ARTIFACTS_DIR="$PWD/artifacts/macos/manual-run" ./scripts/publish-mac.sh
```

Additional macOS publish options:

```bash
DOTNET_BIN="$HOME/.dotnet-guardian8/dotnet" ./scripts/check-mac-dotnet8-prereqs.sh
DOTNET_BIN="$HOME/.dotnet-guardian8/dotnet" ./scripts/publish-mac.sh
RUNTIME_IDENTIFIER=maccatalyst-arm64 ./scripts/publish-mac.sh
SKIP_LAUNCH_SMOKE_TEST=1 ./scripts/publish-mac.sh
ALLOW_UNSUPPORTED_DOTNET_SDK=1 ./scripts/publish-mac.sh
```

### Windows

```powershell
pwsh -File .\scripts\check-windows-dotnet8-prereqs.ps1
pwsh -File .\scripts\publish-windows.ps1
```

Optional parameters:

```powershell
pwsh -File .\scripts\publish-windows.ps1 -Configuration Release -Architecture x64
pwsh -File .\scripts\check-windows-dotnet8-prereqs.ps1 -DotnetExe C:\tools\dotnet8\dotnet.exe
pwsh -File .\scripts\publish-windows.ps1 -DotnetExe C:\tools\dotnet8\dotnet.exe
```

### Sample Dataset

```bash
./scripts/run-guardian-sample.sh
```

Optional environment variables:

```bash
CONFIGURATION=Release REPORT_TITLE="Guardian Sample Smoke" OUTPUT_ROOT="$PWD/sample/guardian/output" ./scripts/run-guardian-sample.sh
```

```powershell
pwsh -File .\scripts\run-guardian-sample.ps1
```

Optional parameters:

```powershell
pwsh -File .\scripts\run-guardian-sample.ps1 -Configuration Release -ReportTitle "Guardian Sample Smoke"
```

## Notes

- The macOS script expects a `.NET 8.x` SDK to be selected. It fails fast on `.NET 9+` unless `ALLOW_UNSUPPORTED_DOTNET_SDK=1` is set.
- `DOTNET_BIN` can point at a user-local `.NET 8` SDK when the system `dotnet` cannot be changed.
- Use `./scripts/check-mac-dotnet8-prereqs.sh` first when setting up a new macOS machine or after changing installed SDKs.
- Use `./scripts/install-local-dotnet8-maccatalyst.sh` when the machine does not have a writable system `.NET 8` + `maccatalyst` toolchain.
- The macOS script publishes `maccatalyst-arm64` by default and runs a launch smoke test against the produced `.app`. Set `SKIP_LAUNCH_SMOKE_TEST=1` only when you intentionally want packaging without a local launch check.
- The macOS script disables code signing by default so local publish works without a signing identity.
- Unsigned `.pkg` and `.app` artifacts can be rejected by Gatekeeper until you add signing and notarization.
- The Windows script expects a `.NET 8.x` SDK to be selected. Use `-DotnetExe` or `DOTNET_EXE` if the default `dotnet` points to another SDK.
- Use `.\scripts\check-windows-dotnet8-prereqs.ps1` first on Windows machines to confirm the `maui` workload is available before publishing.
- The Windows script publishes an unpackaged build to avoid certificate setup for MSIX signing.
- If you need signed store distribution, add platform-specific signing and notarization steps separately.
- The sample-run scripts depend on `sample/guardian` being present in the repository.
