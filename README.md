# Pogo Inventory Assistant

Version 0.7.0

Pogo Inventory Assistant is a local tool for building a complete Pokémon GO inventory, analysing it and later applying safe batch tags. Final transfer remains manual.

Version 0.7.0 adds two major pieces:

- automatic generation of the four core screen states from one known InventoryList start
- a structured Calcy observation pipeline attached to every scanned Pokémon

## What works now

### Automatic core profile bootstrap

The program can now start from the Pokémon inventory list and automatically capture:

```text
InventoryList
PokemonDetails
PokemonMenuOpen
AppraisalOpen, three different Pokémon
```

It uses only the existing allow-listed taps and swipe. It then builds and validates a local `screen-profile.local.json` without per-image approval.

The generated profile is rejected if the captured core states produce a false positive or a misclassification.

### Automatic inventory navigation

- opens the first inventory card
- opens the Pokémon menu
- selects Appraise
- swipes through the inventory automatically
- verifies the state and Pokémon identity after each action
- detects the end of the inventory
- saves one PNG and one structured item record per Pokémon
- writes an atomic checkpoint after every Pokémon
- can resume only from the matching last Pokémon
- stops on Unknown, popup, network error, profile mismatch or timeout

There is no image-by-image approval and no manual navigation during the long scan.

### Structured observations

Each scanned item now contains a nullable observation with:

- species and Pokédex number
- form
- CP, HP and level
- Attack, Defense and HP IV
- gender
- fast move and charged moves
- provider status and confidence
- raw provider output and SHA-256
- warnings and error details

Observation states are:

```text
Complete
Partial
Conflicting
Failed
Unavailable
```

Unknown or conflicting values remain empty. The system does not guess.

The committed fake provider supplies deterministic observations for CI. The real Calcy adapter is the next step and is deliberately not faked as complete.

### Checkpoint migration

The inventory checkpoint schema is now `2.0`.

Old schema `1.0` checkpoints are migrated in memory. Their previous items are marked `Unavailable`, because no Calcy observation existed when those items were captured.

### Inventory analysis

- configurable KEEP, REVIEW and DELETE policy
- duplicate grouping and strictly-better duplicate requirement
- preliminary PvP candidate preservation
- JSON and Markdown decision reports

## Validate the repository

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-fake-core-profile-bootstrap.ps1
.\scripts\run-fake-inventory-scan.ps1
```

The expected test count is 58.

The fake scan should contain exactly:

```text
Pikachu
Machop
Eevee
```

All three fake observations must have status `Complete`.

## One-time setup on the Android phone

The fixed phone still needs an adjusted automation profile with the correct control points.

From a known Pokémon inventory list screen:

```powershell
.\scripts\bootstrap-local-core-profile.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe" `
  -AutomationProfile "C:\Path\automation-profile.local.json"
```

This creates the local screen profile automatically. It is not work repeated for 10,000 Pokémon.

## Current real-device limitation

The real Calcy integration is not finished yet.

A real scan can still capture the ordered evidence, but its observations remain `Unavailable` until a verified Calcy provider is configured. The next release must test the current Calcy IV version on the actual Android phone and implement the working adapter behind `ICalcyObservationProvider`.

## Safety boundary

The phone input interface exposes only:

- tap first inventory card
- tap details menu
- tap Appraise
- swipe to next Pokémon

The repository contains no functions for:

- transfer
- evolve, power-up, purify or TM use
- purchases
- catching, spinning, raids or battles
- location changes
- tagging or text input in this version
- random timing or random positions intended to hide automation

Real screenshots, checkpoints, device details and local profiles stay outside Git through `.gitignore`.

Read next:

- `PROJECT_STATE.md`
- `NEXT_PROMPT.md`
- `docs/AUTOMATIC_CORE_BOOTSTRAP.md`
- `docs/CALCY_OBSERVATION_PIPELINE.md`
- `docs/GUARDRAILS.md`
- `docs/ARCHITECTURE.md`
- `VALIDATION_REPORT.md`
