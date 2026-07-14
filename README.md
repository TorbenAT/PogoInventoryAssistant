# Pogo Inventory Assistant

Version 0.6.0

Pogo Inventory Assistant is a local tool for building a complete Pokémon GO inventory, analysing it and later applying safe batch tags. Final transfer remains manual.

Version 0.6.0 adds the first automatic phone-navigation engine. Once the two local profiles are adjusted to the fixed Android phone, the program can open the first Pokémon, open appraisal, swipe through the inventory, capture every item and stop or resume without user interaction per Pokémon.

## What works now

### Automatic inventory navigation

- uses a strict, named action whitelist
- opens the first inventory card
- opens the Pokémon menu
- selects Appraise
- swipes to the next Pokémon while appraisal remains open
- verifies the screen state after every action
- verifies that the Pokémon identity region changed after every swipe
- detects the end of the inventory when repeated verified swipes do not change the identity region
- writes one local PNG evidence file per Pokémon
- writes SHA-256 hashes and a persistent checkpoint after every Pokémon
- can resume only when the current screen matches the last checkpointed Pokémon
- locks the run to one device serial, one screen geometry and exact hashes of both local profiles
- checks battery level and temperature
- stops on Unknown, popup, network error, unexpected state or timeout
- uses deterministic, adaptive waits rather than random human imitation

There is no image-by-image approval in the automatic scan path. Local evidence is recorded automatically.

### Inventory analysis

- domain model for scanned Pokémon
- configurable KEEP / REVIEW / DELETE policy
- duplicate grouping and strictly-better duplicate requirement
- preliminary PvP candidate preservation
- JSON and Markdown decision reports

### Screen-state detection

- package-free PNG decoding
- normalised UI regions
- Color, Grayscale and Edge fingerprints
- Required, Optional and Forbidden anchors
- deterministic state scoring and winner margin
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

## Important current limitation

The automatic engine is implemented and tested with synthetic screens. A real run still requires two local files adjusted once for the fixed Android setup:

```text
automation-profile.local.json
screen-profile.local.json
```

That one-time adjustment is not work on 10,000 Pokémon. After it is accepted, the inventory traversal itself is automatic.

Version 0.6.0 captures the inventory evidence and sequence. It does not yet extract species, CP, IVs, moves or special status from every captured Pokémon. Calcy integration and observation extraction are the next milestone.

## Validate the repository

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-demo.ps1
.\scripts\run-fake-device.ps1
.\scripts\run-fake-inventory-scan.ps1
.\scripts\detect-synthetic-screen.ps1
.\scripts\build-synthetic-calibration-profile.ps1
.\scripts\validate-synthetic-calibration.ps1
```

The fake automatic scan should finish with three captured items and no manual input.

## Run on the fixed Android phone

Do not use the synthetic profiles on the real phone. Use accepted local profiles.

```powershell
.\scripts\start-local-inventory-scan.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe" `
  -AutomationProfile "C:\Path\automation-profile.local.json" `
  -ScreenProfile "C:\Path\screen-profile.local.json"
```

The starting screen may be:

- Pokémon inventory list
- Pokémon details
- Pokémon menu
- appraisal already open

The program navigates to appraisal automatically and then continues through the inventory.

Outputs remain under an ignored local directory:

```text
local-data\inventory-scans\<run>\
  inventory-scan-checkpoint.json
  captures\000001.png
  captures\000002.png
  ...
```

Rerunning the same command against a running checkpoint resumes only if the same last Pokémon is still open. A completed or safely stopped checkpoint is not changed.

## Safety boundary

The input interface exposes only:

- one configured tap for the first inventory card
- one configured tap for the details menu
- one configured tap for Appraise
- one configured swipe to the next Pokémon

The repository contains no functions for:

- transfer
- evolve, power-up, purify or TM use
- purchases
- catching, spinning, raids or battles
- location changes
- text input or tags in this version
- randomised timing or randomised tap positions intended to hide automation

Do not commit real screenshots, device serials, inventory exports, local profiles, checkpoints or databases while the repository is public.

Read next:

- `PROJECT_STATE.md`
- `NEXT_PROMPT.md`
- `docs/AUTOMATIC_NAVIGATION.md`
- `docs/AUTOMATION_PROFILE.md`
- `docs/GUARDRAILS.md`
- `docs/ARCHITECTURE.md`
- `VALIDATION_REPORT.md`
