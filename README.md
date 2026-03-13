# NexusWorks

`NexusWorks.Guardian` is a desktop comparison tool for validating current and patch deliverables against a baseline workbook. It compares XML, YAML, JAR, and general files, then emits HTML, Excel, JSON, and log artifacts.

## Repository Layout

- `src/NexusWorks.Guardian`
  - Core comparison engine, reporting, orchestration, and models
- `src/NexusWorks.Guardian.UI`
  - MAUI Blazor desktop UI with Tailwind-based styling
- `src/NexusWorks.Guardian.Tests`
  - Core regression tests
- `src/NexusWorks.Guardian.Cli`
  - Headless execution entry point for sample and automation flows
- `sample/guardian`
  - Sample current/patch/baseline dataset
- `scripts`
  - Build, publish, and prerequisite helper scripts
- `docs`
  - Design, implementation, and deployment planning documents

## Common Commands

### Core Tests

```bash
dotnet test ./src/NexusWorks.Guardian.Tests/NexusWorks.Guardian.Tests.csproj -v minimal
```

### Sample Dataset Run

```bash
./scripts/run-guardian-sample.sh
```

### macOS Publish

Use the local `.NET 8` toolchain path when the system `dotnet` cannot be changed:

```bash
./scripts/install-local-dotnet8-maccatalyst.sh
DOTNET_BIN="$HOME/.dotnet-guardian8/dotnet" ./scripts/check-mac-dotnet8-prereqs.sh
APPLE_ID="name@example.com" TEAM_ID="TEAMID1234" APP_SPECIFIC_PASSWORD="xxxx-xxxx-xxxx-xxxx" NOTARY_PROFILE="guardian-notary" ./scripts/setup-mac-notary-profile.sh
APP_SIGN_IDENTITY="Developer ID Application: Example Corp (TEAMID1234)" INSTALLER_SIGN_IDENTITY="Developer ID Installer: Example Corp (TEAMID1234)" NOTARY_PROFILE="guardian-notary" ./scripts/check-mac-signing-prereqs.sh
DOTNET_BIN="$HOME/.dotnet-guardian8/dotnet" APP_SIGN_IDENTITY="Developer ID Application: Example Corp (TEAMID1234)" INSTALLER_SIGN_IDENTITY="Developer ID Installer: Example Corp (TEAMID1234)" NOTARY_PROFILE="guardian-notary" ./scripts/release-mac.sh
node --test ./scripts/tests/release-scripts.test.mjs
node ./scripts/verify-release-manifest.mjs --artifacts-dir ./artifacts/macos/<timestamp>
```

### Windows Publish

Run these on a Windows machine:

```powershell
pwsh -File .\scripts\check-windows-dotnet8-prereqs.ps1
pwsh -File .\scripts\release-windows.ps1
node --test .\scripts\tests\release-scripts.test.mjs
node .\scripts\verify-release-manifest.mjs --artifacts-dir .\artifacts\windows\<timestamp>
```

### GitHub Actions Validation

`guardian-release-validation.yml` runs the existing release wrappers on GitHub-hosted macOS and Windows runners:

- [.github/workflows/guardian-release-validation.yml](/Users/imagineiluv/Documents/GitHub/NexusWorks/.github/workflows/guardian-release-validation.yml)
- macOS job runs `./scripts/release-mac.sh` with `SKIP_SIGNING=1` and `SKIP_LAUNCH_SMOKE_TEST=1`
- Windows job runs `./scripts/release-windows.ps1`
- optional signed macOS job runs `./scripts/release-mac.sh` with imported Developer ID certificates and notarization credentials
- both jobs verify `manifest.json` and upload the generated release folder as a workflow artifact

Required GitHub secrets for the optional signed macOS job:

- `MACOS_CERTIFICATES_P12_BASE64`
- `MACOS_CERTIFICATES_P12_PASSWORD`
- `MACOS_KEYCHAIN_PASSWORD`
- `APP_SIGN_IDENTITY`
- `INSTALLER_SIGN_IDENTITY` if a signed `.pkg` is required
- `NOTARY_APPLE_ID`
- `NOTARY_TEAM_ID`
- `NOTARY_APP_PASSWORD`

## Important Notes

- The repository pins `.NET SDK 8.0.416` through `global.json`.
- macOS publish runs a launch smoke test and fails if the produced `.app` does not stay running.
- Signed distribution can be run end-to-end with `scripts/release-mac.sh`.
- Windows distribution can be run end-to-end with `scripts/release-windows.ps1`.
- GitHub Actions validates both wrappers on real `macos-14` and `windows-latest` runners.
- The optional signed macOS CI job stays skipped until the required GitHub secrets are configured.
- Release directories now include `manifest.json` with SHA-256 checksums.
- Release wrappers run `scripts/tests/release-scripts.test.mjs` before publish to catch helper regressions.
- `scripts/verify-release-manifest.mjs` can re-check a release folder before delivery.
- The CI macOS job skips the launch smoke test because hosted runners are headless. Local `publish-mac.sh` still keeps the launch check enabled by default.
- Unsigned macOS artifacts can still be rejected by Gatekeeper until signing and notarization are added.
- `scripts/README.md` contains the detailed script matrix and environment variables.

## Key Documents

- `docs/nexusworks-guardian-design-plan.md`
- `scripts/README.md`
