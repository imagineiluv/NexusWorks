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
DOTNET_BIN="$HOME/.dotnet-guardian8/dotnet" ./scripts/publish-mac.sh
```

### Windows Publish

Run these on a Windows machine:

```powershell
pwsh -File .\scripts\check-windows-dotnet8-prereqs.ps1
pwsh -File .\scripts\publish-windows.ps1
```

## Important Notes

- The repository pins `.NET SDK 8.0.416` through `global.json`.
- macOS publish runs a launch smoke test and fails if the produced `.app` does not stay running.
- Unsigned macOS artifacts can still be rejected by Gatekeeper until signing and notarization are added.
- `scripts/README.md` contains the detailed script matrix and environment variables.

## Key Documents

- `docs/nexusworks-guardian-design-plan.md`
- `scripts/README.md`
