# Pogo Inventory Assistant

Version 0.3.1

Pogo Inventory Assistant is being built as a conservative, local inventory and decision assistant for Pokémon GO. The final transfer remains manual.

Version 0.3.1 adds a read-only Screen State Detector. It analyses PNG screenshots and returns a state plus a full evidence report. It does not control the phone.

## What works now

### Inventory analysis

- domain model for scanned Pokémon
- configurable decision policy
- conservative KEEP / REVIEW / DELETE rule engine
- duplicate grouping
- preliminary PvP preservation heuristic
- JSON and Markdown decision reports

### Read-only Android Device Harness

- discovers Android devices through ADB
- requires exactly one authorised device unless `--serial` is supplied
- reads device, screen and battery metadata
- captures a validated PNG screenshot
- writes a SHA-256 capture manifest
- supports timeouts, cancellation and a fake Android transport

### Read-only Screen State Detector

- decodes 8-bit, non-interlaced PNG screenshots without a third-party image package
- validates orientation, dimensions and aspect ratio
- uses named anchors in normalised screen regions
- supports Color, Grayscale and Edge fingerprints
- supports Required, Optional and Forbidden anchors
- returns confidence, per-state scores and per-anchor evidence
- fails closed to `Unknown` on incomplete, conflicting or unsupported screens
- writes a deterministic JSON evidence report
- includes synthetic fixtures for all initial states

Initial states:

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

The included profile is synthetic test data. It does not yet recognise the real Pokémon GO interface. Real anchors must be calibrated from redacted screenshots captured on Torben's Android phone.

## Requirements

- Windows 10 or 11
- Visual Studio 2022 or .NET 8 SDK
- Android Platform Tools for real phone screenshots
- USB debugging enabled on the Android phone

## Validate the repository

From PowerShell in the repository folder:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-demo.ps1
.\scripts\run-fake-device.ps1
.\scripts\detect-synthetic-screen.ps1
.\scripts\extract-synthetic-fingerprint.ps1
```

## Detect a screen from a PNG

```powershell
dotnet run --project .\src\PogoInventory.Cli -- screen-detect `
  --image .\data\screen-fixtures\InventoryList.png `
  --profile .\data\screen-profile.synthetic.json `
  --out .\out\screen-detection\inventory-list.json
```

Expected state:

```text
InventoryList
```

The JSON report contains:

- selected state
- confidence
- image dimensions, orientation and aspect ratio
- rejection or classification reasons
- every state score
- every anchor similarity and threshold

## Extract an anchor fingerprint

```powershell
dotnet run --project .\src\PogoInventory.Cli -- screen-fingerprint `
  --image .\data\screen-fixtures\InventoryList.png `
  --region "0.05,0.70,0.25,0.20" `
  --mode Color `
  --width 8 `
  --height 8 `
  --out .\out\screen-fingerprint\inventory-grid.json
```

Regions use normalised values:

```text
x,y,width,height
```

Each value is relative to the full screenshot and must stay inside 0 to 1.

## Capture from a real Android phone

Connect the phone by USB, unlock it and approve USB debugging.

```powershell
.\scripts\capture-device.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

Output:

```text
out\device\screen.png
out\device\device-metadata.json
out\device\device-snapshot.json
```

Do not commit real screenshots, device serials, inventory exports or scan databases while the repository is public.

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
- `docs/SCREEN_STATE_DETECTOR.md`
- `docs/GUARDRAILS.md`
- `docs/ARCHITECTURE.md`
- `VALIDATION_REPORT.md`
