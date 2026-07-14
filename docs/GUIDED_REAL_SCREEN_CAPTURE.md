# Guided private real-screen capture

> Legacy fallback: This manual capture and approval workflow is retained for diagnostics. Version 0.6.0 and later target automatic local navigation and do not require per-image approval for the inventory scan.


## Purpose

Version 0.5.0 collects the real screenshots needed for calibration without automating navigation in Pokémon GO.

The user opens each requested screen manually. The program only:

- discovers one authorised Android device
- reads device metadata
- captures the current screen as PNG
- validates geometry
- hashes and records the image locally

It does not tap, swipe, type, launch apps or change anything in Pokémon GO.

## Prepare the computer and phone

Keep one fixed configuration for the whole session:

- same Android phone
- portrait orientation
- same display resolution and display-size setting
- same font scale
- same Android navigation mode
- same Pokémon GO language
- no floating windows or notification banners

Connect the phone by USB and approve USB debugging.

Verify ADB from PowerShell:

```powershell
C:\Android\platform-tools\adb.exe devices -l
```

Exactly one authorised device should be shown, unless an explicit serial is supplied to the scripts.

## Initialise or upgrade the private workspace

```powershell
.\scripts\init-local-calibration.ps1
```

The workspace contains:

```text
local-data\screen-calibration\
├── .pogo-private-calibration
├── capture-plan.local.json
├── capture-session.local.json          created after first capture
├── fixture-manifest.local.json
├── anchor-plan.local.json
├── incoming\                           unreviewed screenshots
├── fixtures\                           reviewed screenshots
├── profiles\
└── reports\capture\
```

All of this is ignored by Git when the default workspace is used.

## Guided session

```powershell
.\scripts\start-local-calibration-capture.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

For every step:

1. Read the requested state and variation hint.
2. Navigate to that screen manually on the phone.
3. Check that no notification or personal overlay is visible.
4. Press Enter on the computer.
5. The tool captures one screenshot and reports its id.

Type `q` to stop safely. The session resumes from the existing local session file later.

## Capture a particular state

```powershell
.\scripts\capture-local-calibration-state.ps1 `
  -State AppraisalOpen `
  -AdbPath "C:\Android\platform-tools\adb.exe" `
  -Notes "Different IV bars and species"
```

Allowed states:

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

`Loading` and `NetworkError` are required for final detector acceptance, but the default plan places them last so they can be collected after the easier states.

## Session locks

The capture session is locked to the exact capture-plan fingerprint. The first accepted screenshot additionally locks it to:

- one Android serial
- one exact screenshot width
- one exact screenshot height
- the required orientation

The capture fails closed if the phone, resolution, display scaling or orientation changes.

Delete neither the session nor incoming screenshots during capture. Every existing file is re-hashed before another image is added.

## Duplicate handling

A pixel-identical screenshot is recorded as a duplicate but does not count toward variation coverage.

The same bytes cannot be labelled as two different expected states. That is treated as a contradiction and stops the capture.

A duplicate cannot be promoted as a fixture. Promote its original capture instead.

Promotion also refuses to overwrite any fixture file that exists outside the approved manifest.

## Status

```powershell
.\scripts\show-local-calibration-capture-status.ps1
```

The output shows:

- unique and duplicate counts
- promoted count
- remaining examples per state
- next recommended state
- capture ids needed for approval

Reports are written to:

```text
local-data\screen-calibration\reports\capture\capture-status.json
local-data\screen-calibration\reports\capture\capture-status.md
```

## Privacy review and promotion

Incoming screenshots are not approved fixtures.

Open each image locally and check:

- account and trainer identity
- location information
- Android notifications
- contacts, messages or email
- other personal or security information
- correct expected state
- useful visual variation

Then promote it:

```powershell
.\scripts\approve-local-calibration-capture.ps1 `
  -CaptureId "0001-inventorylist-20260713-..." `
  -ReviewedBy "Torben"
```

The script requires the exact confirmation `APPROVE`.

Promotion:

- verifies the original capture hash
- rejects duplicates
- copies the PNG into `fixtures/<ScreenState>/`
- adds a complete safety review to the local manifest
- links the fixture back to the capture id
- supports safe idempotent retry

Approval is only for local calibration. It is not permission to commit the screenshot publicly.

## After capture coverage is complete

1. Review and promote the useful captures.
2. Run `index-local-calibration.ps1` as an additional consistency check.
3. Edit `anchor-plan.local.json`.
4. Build the local profile.
5. Run acceptance.
6. Inspect every false positive, misclassification and weak anchor.

Do not proceed to Calcy or continuous scanning until real-screen acceptance is green.
