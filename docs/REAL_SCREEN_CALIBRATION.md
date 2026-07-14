# Real-screen calibration workflow

> Legacy fallback: This manual capture and approval workflow is retained for diagnostics. Version 0.6.0 and later target automatic local navigation and do not require per-image approval for the inventory scan.


## Purpose

Version 0.5.0 provides a controlled workflow for turning private Pokémon GO screenshots into a local screen-detection profile and proving that the detector fails closed.

The workflow is read-only. It does not add taps, swipes, text input, tagging or transfer.

## Private workspace

Initialise or upgrade the default ignored workspace:

```powershell
.\scripts\init-local-calibration.ps1
```

Default layout:

```text
local-data\screen-calibration\
├── .pogo-private-calibration
├── PRIVATE-README.txt
├── capture-plan.local.json
├── capture-session.local.json
├── fixture-manifest.local.json
├── anchor-plan.local.json
├── incoming\
│   ├── InventoryList\
│   ├── PokemonDetails\
│   ├── AppraisalOpen\
│   ├── PokemonMenuOpen\
│   ├── TagDialogOpen\
│   ├── SearchOpen\
│   ├── Loading\
│   ├── Popup\
│   ├── NetworkError\
│   └── Unknown\
├── fixtures\
│   ├── InventoryList\
│   ├── PokemonDetails\
│   ├── AppraisalOpen\
│   ├── PokemonMenuOpen\
│   ├── TagDialogOpen\
│   ├── SearchOpen\
│   ├── Loading\
│   ├── Popup\
│   ├── NetworkError\
│   └── Unknown\
├── profiles\screen-profile.local.json
└── reports\
    ├── capture\
    └── acceptance\
```

The marker file is mandatory. Real-screen commands refuse to use an arbitrary directory that has not been explicitly initialised.

## Fixed phone configuration

Keep the Android phone fixed to one configuration:

- same physical phone
- same effective screen resolution
- portrait orientation
- same display size and font scale
- same Pokémon GO language
- same Android navigation mode
- no floating windows or notification banners

The first capture locks the local session to the device serial and exact screenshot dimensions.

## Capture coverage

The default capture plan targets:

- 3 visually different InventoryList screens
- 3 visually different PokemonDetails screens
- 3 visually different AppraisalOpen screens
- 2 PokemonMenuOpen screens
- 2 TagDialogOpen screens
- 2 SearchOpen screens
- 2 Popup screens
- 1 genuine Loading example and 1 genuine NetworkError example, collected last if necessary but required before acceptance
- 6 Unknown negatives

Unknown negatives should include:

- an unrelated screen or app
- the Pokémon GO map
- a partial transition
- an unsupported orientation or layout
- an interrupting overlay
- a visually conflicting screen

## Guided capture

Start the guided session:

```powershell
.\scripts\start-local-calibration-capture.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

For each requested state:

1. Navigate manually on the phone.
2. Confirm the correct screen is fully visible.
3. Confirm no notification or personal overlay is visible.
4. Press Enter on the computer.
5. The read-only ADB layer captures one screenshot.

Type `q` to stop. The session can resume later.

A screenshot first lands in:

```text
incoming\<ExpectedState>\
```

Incoming screenshots are not approved fixtures and cannot be used to build a detector profile.

## Status and capture ids

```powershell
.\scripts\show-local-calibration-capture-status.ps1
```

The report shows:

- unique captures
- pixel-identical duplicates
- promoted fixtures
- remaining examples per state
- next recommended state
- all capture ids

## Single-state capture

```powershell
.\scripts\capture-local-calibration-state.ps1 `
  -State PokemonDetails `
  -AdbPath "C:\Android\platform-tools\adb.exe" `
  -Notes "Different species and background"
```

## Duplicate and integrity rules

- Every capture is locked by SHA-256.
- Existing capture files are re-verified before another screenshot is added.
- A changed or missing file stops the workflow.
- Pixel-identical captures do not count toward variation coverage.
- Identical bytes cannot be assigned to different expected states.
- Duplicate captures cannot be promoted.

## Privacy review and promotion

Open each incoming PNG locally and complete the fixture checklist.

Then promote the capture:

```powershell
.\scripts\approve-local-calibration-capture.ps1 `
  -CaptureId "<capture-id>" `
  -ReviewedBy "Torben"
```

The script requires the exact confirmation text `APPROVE`.

Promotion:

- re-verifies the incoming hash
- rejects duplicate captures
- copies the PNG into `fixtures\<ScreenState>\`
- records a complete local safety review
- links the fixture to the original capture id
- supports safe retry if a previous promotion was interrupted

Approval applies only to local calibration. It does not approve public sharing.

Run the indexer afterwards as an additional consistency check:

```powershell
.\scripts\index-local-calibration.ps1
```

## Anchor plan

Edit `anchor-plan.local.json`.

Each classified state needs at least one Required anchor. Prefer several small independent anchors over one large screenshot region.

Good anchors:

- stable button shape
- stable panel border
- fixed header or footer control
- appraisal bar geometry
- search field shape
- menu or dialog frame

Bad anchors:

- Pokémon artwork
- Pokémon name
- CP number
- account name
- location text
- dynamic Candy, Stardust, weight or height values
- full-screen fingerprint

Each anchor lists promoted fixture ids used to generate its reference samples:

```json
{
  "name": "details-close-button",
  "region": { "x": 0.88, "y": 0.03, "width": 0.09, "height": 0.08 },
  "mode": "Edge",
  "expectation": "Required",
  "fingerprintWidth": 12,
  "fingerprintHeight": 12,
  "matchThreshold": 0.92,
  "weight": 1.0,
  "sampleFixtureIds": [
    "guided-0001-pokemondetails-...",
    "guided-0002-pokemondetails-..."
  ]
}
```

Required and Optional samples must come from the same state. Forbidden anchors may use a fixture from the state or overlay they are intended to reject.

## Build the local profile

```powershell
.\scripts\build-local-calibration-profile.ps1
```

The generated profile contains compact fingerprints, not the original PNG files. It remains local and ignored by Git.

The build fails when:

- a referenced fixture is missing or unapproved
- a file hash changed after approval
- an anchor has no sample
- a state has no anchor
- the plan winner margin is weaker than the acceptance policy
- the generated profile is invalid

## Run acceptance

```powershell
.\scripts\validate-local-calibration.ps1
```

Outputs:

```text
reports\acceptance\calibration-acceptance.json
reports\acceptance\calibration-acceptance.md
reports\acceptance\confusion-matrix.csv
reports\acceptance\fixture-results.csv
```

The report includes:

- expected versus actual state for every approved fixture
- false positives
- false negatives
- known-state misclassifications
- recall per state
- confusion matrix
- weak anchors
- positive and negative similarity separation
- missing fixture coverage

## Acceptance rules

The default real-screen policy requires:

- zero false positives on Unknown fixtures
- zero wrong known-state classifications
- no weak anchors
- profile winner margin at least 0.05
- minimum fixture coverage for every required state
- at least 90 percent recall for the three core dynamic states
- Unknown recall of 100 percent

False negatives may be tolerated only within an explicit per-state recall threshold. A false positive is a hard blocker.

## Stop condition

Do not begin Calcy integration or a continuous scanning loop until:

- required real fixtures are captured and promoted
- all fixture hashes and reviews are valid
- acceptance is green
- false positives are zero
- misclassifications are zero
- weak anchors are zero
- the report has been manually reviewed
