# Pogo Inventory Assistant

Version 0.5.0

Pogo Inventory Assistant is a conservative, local inventory and decision assistant for Pokémon GO. The final transfer remains manual.

Version 0.5.0 adds a guided, read-only workflow for collecting the private Android screenshots required to calibrate the screen-state detector. Navigation on the phone remains manual. The software captures screenshots only after the user confirms that the requested screen is visible.

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

### Calibration and guided capture

- private local workspace with a mandatory marker
- guided capture plan with required variation coverage
- manual phone navigation and explicit Enter-to-capture workflow
- capture-plan fingerprint, device-serial and exact-geometry session lock
- private `incoming` screenshots separated from approved fixtures
- SHA-256 verification of every recorded capture
- duplicate screenshot detection
- status report with missing states and next recommendation
- explicit privacy-review confirmation before promotion
- safe promotion into the approved fixture manifest
- approval reset and hash validation if fixture bytes change
- anchor-plan profile generation and strict acceptance reports
- synthetic end-to-end calibration in CI

No real Pokémon GO screenshots, device serials or phone-specific profiles are committed.

## Requirements

- Windows 10 or 11
- Visual Studio 2022 or .NET 8 SDK
- Android Platform Tools for real phone screenshots
- USB debugging enabled on the Android phone
- one fixed Android display configuration during a capture session

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

## Start private real-screen capture

Initialise or upgrade the ignored local workspace:

```powershell
.\scripts\init-local-calibration.ps1
```

Start the guided capture session:

```powershell
.\scripts\start-local-calibration-capture.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

The program tells you which screen to open. You navigate manually on the phone and press Enter on the computer when the requested screen is ready.

Loading and NetworkError samples are required before a real profile can be accepted. They are placed last in the default plan so the rest of the fixture set can be completed first.

Show progress and capture ids:

```powershell
.\scripts\show-local-calibration-capture-status.ps1
```

Capture one specific state without the guided loop:

```powershell
.\scripts\capture-local-calibration-state.ps1 `
  -State PokemonDetails `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

Captured files remain under:

```text
local-data\screen-calibration\incoming\<ExpectedState>\
```

They do not become calibration fixtures automatically.

## Review and approve a capture

Open the screenshot locally and complete the privacy checklist. Then promote it:

```powershell
.\scripts\approve-local-calibration-capture.ps1 `
  -CaptureId "0001-inventorylist-..." `
  -ReviewedBy "Torben"
```

The script requires the exact confirmation text `APPROVE`. Promotion copies the hash-verified screenshot into the correct fixture folder and adds a complete local safety review to the manifest.

Then edit `anchor-plan.local.json` and run:

```powershell
.\scripts\build-local-calibration-profile.ps1
.\scripts\validate-local-calibration.ps1
```

Acceptance reports are written under:

```text
local-data\screen-calibration\reports\acceptance
```

See:

- `docs/GUIDED_REAL_SCREEN_CAPTURE.md`
- `docs/REAL_SCREEN_CALIBRATION.md`
- `docs/FIXTURE_APPROVAL_CHECKLIST.md`

## Safety boundary

The repository contains no methods for:

- taps or swipes
- text input or tagging
- automatic transfer
- evolve, power-up, purify or TM use
- purchases, catching, spinning, raids or battles
- location changes
- randomised or human-like input

Do not commit real screenshots, device serials, capture sessions, inventory exports, local profiles or databases while the repository is public.

Read next:

- `PROJECT_STATE.md`
- `NEXT_PROMPT.md`
- `docs/GUARDRAILS.md`
- `docs/ARCHITECTURE.md`
- `VALIDATION_REPORT.md`
