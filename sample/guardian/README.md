# Guardian Sample Dataset

Use this dataset to validate the `NexusWorks.Guardian` compare flow.

## Input Paths

- Current root: `/Users/imagineiluv/Documents/GitHub/NexusWorks/sample/guardian/current`
- Patch root: `/Users/imagineiluv/Documents/GitHub/NexusWorks/sample/guardian/patch`
- Baseline workbook: `/Users/imagineiluv/Documents/GitHub/NexusWorks/sample/guardian/baseline.xlsx`
- Output root: `/Users/imagineiluv/Documents/GitHub/NexusWorks/sample/guardian/output`

## Expected Highlights

- `conf/app.xml`: `Changed`
- `conf/layout.xml`: `Ok` after XML normalization
- `conf/settings.yaml`: `Changed`
- `conf/feature-flags.yaml`: `Ok` after YAML normalization
- `lib/core-guardian.jar`: `Changed` with class-level summary
- `notes/release.txt`: `Changed`
- `conf/required.xml`: `MissingRequired`
- `only-current.txt`: `Removed`
- `only-patch.txt`: `Added`

## Notes

- The baseline includes one excluded rule (`logs/*.log`) to exercise preview counts.
- The output folder is intentionally empty so the app can generate fresh artifacts there.

## Quick Run

From the repository root:

```bash
./scripts/run-guardian-sample.sh
```

On Windows:

```powershell
pwsh -File .\scripts\run-guardian-sample.ps1
```
