# Pogo Inventory Assistant

Version 0.4.0

Pogo Inventory Assistant is a conservative, local inventory and decision assistant for Pokémon GO. The final transfer remains manual.

Version 0.4.0 adds a complete read-only workflow for calibrating the Screen State Detector from private screenshots and proving that it fails closed before any later scanner work begins.

## What works now

### Inventory analysis

- domain model for scanned Pokémon
- configurable KEEP / REVIEW / DELETE policy
- duplicate grouping and strictly-better duplicate requirement
- preliminary PvP candidate preservation
- JSON and Markdown decision reports

### Read-only Android Device Harness

- authorised-device discovery through ADB
- explicit serial selection
- device, screen and battery metadata
- validated PNG screenshot capture
- SHA-256 capture manifest
- timeouts, cancellation and fake transport

### Read-only Screen State Detector

- package-free PNG decoding
- normalised UI regions
- Color, Grayscale and Edge fingerprints
- Required, Optional and Forbidden anchors
- orientation, resolution and aspect-ratio checks
- deterministic state scoring and winner margin
- full JSON evidence
- fail-closed `Unknown`

Supported states:

```text
InventoryList
PokemonDetails
AppraisalOpen
PokemonMenuOpen
TagDialogOpen
SearchOpen
Loading
Popup
NetworkError
Unknown
```

### Real-screen calibration and acceptance

- private local workspace with a mandatory marker
- state-folder fixture indexing
- SHA-256 locking of approved PNGs
- approval reset when a fixture changes
- explicit privacy and redaction review
- anchor plans with multiple samples
- local profile generation
- false-positive, false-negative and misclassification accounting
- per-state recall and coverage rules
- confusion matrix
- weak-anchor and similarity-separation analysis
- JSON, Markdown and CSV reports
- synthetic end-to-end calibration in CI

No real Pokémon GO screenshots or phone-specific profile are committed. The next checkpoint is local capture and real-screen acceptance.

## Requirements

- Windows 10 or 11
- Visual Studio 2022 or .NET 8 SDK
- Android Platform Tools for real phone screenshots
- USB debugging enabled on the Android phone

## Validate the repository

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-demo.ps1
.\scripts\run-fake-device.ps1
.\scripts\detect-synthetic-screen.ps1
.\scripts\extract-synthetic-fingerprint.ps1
.\scripts\build-synthetic-calibration-profile.ps1
.\scripts\validate-synthetic-calibration.ps1
```

## Initialise private real-screen calibration

```powershell
.\scripts\init-local-calibration.ps1
```

Default workspace:

```text
local-data\screen-calibration
```

Place PNGs under:

```text
fixtures\<ExpectedState>\
```

Then run:

```powershell
.\scripts\index-local-calibration.ps1
```

Review every entry in `fixture-manifest.local.json`. A fixture is ignored until all privacy fields and `approvedForCalibration` are true.

Edit `anchor-plan.local.json`, then run:

```powershell
.\scripts\build-local-calibration-profile.ps1
.\scripts\validate-local-calibration.ps1
```

Acceptance reports are written under:

```text
local-data\screen-calibration\reports\acceptance
```

See:

- `docs/REAL_SCREEN_CALIBRATION.md`
- `docs/FIXTURE_APPROVAL_CHECKLIST.md`

## Capture a real Android screenshot

```powershell
.\scripts\capture-device.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

Do not commit real screenshots, device serials, inventory exports, local profiles or databases while the repository is public.

## Safety boundary

The repository contains no methods for:

- taps or swipes
- text input or tagging
- automatic transfer
- evolve, power-up, purify or TM use
- purchases, catching, spinning, raids or battles
- location changes
- randomised or human-like input

Read next:

- `PROJECT_STATE.md`
- `NEXT_PROMPT.md`
- `docs/GUARDRAILS.md`
- `docs/ARCHITECTURE.md`
- `VALIDATION_REPORT.md`
