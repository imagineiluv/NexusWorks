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
- `check-mac-signing-prereqs.sh`
  - Run on macOS
  - Lists available code-signing identities
  - Checks whether the requested Developer ID identities are present
  - Validates the requested notary profile with `xcrun notarytool history`
- `setup-mac-notary-profile.sh`
  - Run on macOS
  - Creates a `notarytool` keychain profile from `APPLE_ID`, `TEAM_ID`, and `APP_SPECIFIC_PASSWORD`
  - Prints the exact follow-up validation command
- `sign-mac-artifacts.sh`
  - Run on macOS
  - Signs the built `.app` with a Developer ID Application identity
  - Creates a signed zip and optionally a signed installer pkg
- `notarize-mac-artifacts.sh`
  - Run on macOS
  - Submits signed app/package artifacts with `xcrun notarytool`
  - Staples accepted tickets and recreates a notarized app zip
- `import-mac-signing-certificate.sh`
  - Run on macOS
  - Imports a base64-encoded `.p12` bundle into a temporary keychain
  - Prepares `CODESIGN_KEYCHAIN` for CI signing and `productbuild`
- `release-mac.sh`
  - Run on macOS
  - Orchestrates publish -> signing preflight -> sign -> notarize
  - Supports `SKIP_SIGNING=1` and `SKIP_NOTARIZATION=1`
  - Runs `node --test ./scripts/tests/release-scripts.test.mjs` before publish
- `release-windows.ps1`
  - Run on Windows
  - Orchestrates prereq check -> publish -> manifest generation
  - Supports `-SkipPrereqCheck`
  - Runs `node --test .\scripts\tests\release-scripts.test.mjs` before publish
- `.github/workflows/guardian-release-validation.yml`
  - Run on GitHub Actions
  - Executes `release-mac.sh` on `macos-14` and `release-windows.ps1` on `windows-latest`
  - Optionally executes a signed/notarized macOS release when Apple signing secrets are configured
  - Verifies each generated `manifest.json`
  - Uploads the produced release folders as workflow artifacts
- `write-release-manifest.mjs`
  - Run on macOS or Windows
  - Writes `manifest.json` with file sizes and SHA-256 checksums for each artifact
- `verify-release-manifest.mjs`
  - Run on macOS or Windows
  - Recomputes file size and SHA-256 values from `manifest.json`
  - Fails when any artifact has changed or is missing
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
APPLE_ID="name@example.com" TEAM_ID="TEAMID1234" APP_SPECIFIC_PASSWORD="xxxx-xxxx-xxxx-xxxx" NOTARY_PROFILE="guardian-notary" ./scripts/setup-mac-notary-profile.sh
./scripts/check-mac-signing-prereqs.sh
./scripts/publish-mac.sh
APP_SIGN_IDENTITY="Developer ID Application: Example Corp (TEAMID1234)" INSTALLER_SIGN_IDENTITY="Developer ID Installer: Example Corp (TEAMID1234)" ./scripts/sign-mac-artifacts.sh
NOTARY_PROFILE="guardian-notary" ./scripts/notarize-mac-artifacts.sh
```

Optional environment variables:

```bash
CONFIGURATION=Release ARTIFACTS_DIR="$PWD/artifacts/macos/manual-run" ./scripts/publish-mac.sh
```

Additional macOS publish options:

```bash
DOTNET_BIN="$HOME/.dotnet-guardian8/dotnet" ./scripts/check-mac-dotnet8-prereqs.sh
APP_SIGN_IDENTITY="Developer ID Application: Example Corp (TEAMID1234)" INSTALLER_SIGN_IDENTITY="Developer ID Installer: Example Corp (TEAMID1234)" NOTARY_PROFILE="guardian-notary" ./scripts/check-mac-signing-prereqs.sh
DOTNET_BIN="$HOME/.dotnet-guardian8/dotnet" ./scripts/publish-mac.sh
ARTIFACTS_DIR="$PWD/artifacts/macos/manual-run" APP_SIGN_IDENTITY="Developer ID Application: Example Corp (TEAMID1234)" INSTALLER_SIGN_IDENTITY="Developer ID Installer: Example Corp (TEAMID1234)" ./scripts/sign-mac-artifacts.sh
ARTIFACTS_DIR="$PWD/artifacts/macos/manual-run" NOTARY_PROFILE="guardian-notary" ./scripts/notarize-mac-artifacts.sh
DOTNET_BIN="$HOME/.dotnet-guardian8/dotnet" APP_SIGN_IDENTITY="Developer ID Application: Example Corp (TEAMID1234)" INSTALLER_SIGN_IDENTITY="Developer ID Installer: Example Corp (TEAMID1234)" NOTARY_PROFILE="guardian-notary" ./scripts/release-mac.sh
node --test ./scripts/tests/release-scripts.test.mjs
node ./scripts/verify-release-manifest.mjs --artifacts-dir ./artifacts/macos/<timestamp>
RUNTIME_IDENTIFIER=maccatalyst-arm64 ./scripts/publish-mac.sh
SKIP_LAUNCH_SMOKE_TEST=1 ./scripts/publish-mac.sh
SKIP_SIGNING=1 ./scripts/release-mac.sh
SKIP_NOTARIZATION=1 ./scripts/release-mac.sh
ALLOW_UNSUPPORTED_DOTNET_SDK=1 ./scripts/publish-mac.sh
```

### Windows

```powershell
pwsh -File .\scripts\check-windows-dotnet8-prereqs.ps1
pwsh -File .\scripts\publish-windows.ps1
pwsh -File .\scripts\release-windows.ps1
```

Optional parameters:

```powershell
pwsh -File .\scripts\publish-windows.ps1 -Configuration Release -Architecture x64
pwsh -File .\scripts\check-windows-dotnet8-prereqs.ps1 -DotnetExe C:\tools\dotnet8\dotnet.exe
pwsh -File .\scripts\publish-windows.ps1 -DotnetExe C:\tools\dotnet8\dotnet.exe
pwsh -File .\scripts\release-windows.ps1 -DotnetExe C:\tools\dotnet8\dotnet.exe
pwsh -File .\scripts\release-windows.ps1 -SkipPrereqCheck
node --test .\scripts\tests\release-scripts.test.mjs
node .\scripts\verify-release-manifest.mjs --artifacts-dir .\artifacts\windows\<timestamp>
```

### GitHub Actions

- Workflow file:
  - [.github/workflows/guardian-release-validation.yml](/Users/imagineiluv/Documents/GitHub/NexusWorks/.github/workflows/guardian-release-validation.yml)
- Trigger:
  - `workflow_dispatch`
  - `pull_request` and `push` when Guardian release-related files change
- Behavior:
  - macOS CI runs `release-mac.sh` with `SKIP_SIGNING=1` and `SKIP_LAUNCH_SMOKE_TEST=1`
  - Windows CI runs `release-windows.ps1`
  - optional signed macOS CI imports a temporary keychain, stores a notary profile, then runs `release-mac.sh`
  - both jobs run manifest verification and upload the release directory
- Secrets for signed macOS CI:
  - `MACOS_CERTIFICATES_P12_BASE64`
  - `MACOS_CERTIFICATES_P12_PASSWORD`
  - `MACOS_KEYCHAIN_PASSWORD`
  - `APP_SIGN_IDENTITY`
  - `INSTALLER_SIGN_IDENTITY` when a signed pkg is needed
  - `NOTARY_APPLE_ID`
  - `NOTARY_TEAM_ID`
  - `NOTARY_APP_PASSWORD`

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
- Use `./scripts/setup-mac-notary-profile.sh` once per machine to store the `notarytool` credentials in the keychain.
- The macOS script publishes `maccatalyst-arm64` by default and runs a launch smoke test against the produced `.app`. Set `SKIP_LAUNCH_SMOKE_TEST=1` only when you intentionally want packaging without a local launch check.
- The macOS script disables code signing by default so local publish works without a signing identity.
- `check-mac-signing-prereqs.sh` is the fastest way to confirm whether the expected Developer ID identities are installed before starting a signed release.
- `sign-mac-artifacts.sh` expects `APP_SIGN_IDENTITY` and optionally `INSTALLER_SIGN_IDENTITY`.
- `notarize-mac-artifacts.sh` expects a keychain profile name in `NOTARY_PROFILE`. Create it with `xcrun notarytool store-credentials`.
- `release-mac.sh` and `release-windows.ps1` write `manifest.json` with checksums for the produced release directory.
- `release-mac.sh` and `release-windows.ps1` run `scripts/tests/release-scripts.test.mjs` before publish so manifest/summary helpers fail fast.
- `.github/workflows/guardian-release-validation.yml` closes the local OS gap by validating the wrappers on actual GitHub-hosted macOS and Windows runners.
- `.github/workflows/guardian-release-validation.yml` can also validate signed/notarized macOS release output once Apple secrets are configured.
- `verify-release-manifest.mjs` should be run before handing artifacts to another team or uploading them to a release system.
- The GitHub-hosted macOS runner is treated as headless, so the workflow skips the local launch smoke test there. Keep the default smoke test enabled for local macOS release verification.
- Unsigned `.pkg` and `.app` artifacts can be rejected by Gatekeeper until you add signing and notarization.
- The Windows script expects a `.NET 8.x` SDK to be selected. Use `-DotnetExe` or `DOTNET_EXE` if the default `dotnet` points to another SDK.
- Use `.\scripts\check-windows-dotnet8-prereqs.ps1` first on Windows machines to confirm the `maui` workload is available before publishing.
- The Windows script publishes an unpackaged build to avoid certificate setup for MSIX signing.
- If you need signed store distribution, add platform-specific signing and notarization steps separately.
- The sample-run scripts depend on `sample/guardian` being present in the repository.
