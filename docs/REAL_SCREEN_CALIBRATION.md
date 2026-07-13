# Real-screen calibration workflow

## Purpose

Version 0.4.0 adds a controlled workflow for turning private, redacted Pokémon GO screenshots into a local screen-detection profile and proving that the profile fails closed.

The workflow is read-only. It does not connect calibration to taps, swipes, text input, tagging or transfer.

## Private workspace

Initialise the default ignored workspace:

```powershell
.\scripts\init-local-calibration.ps1
```

Default layout:

```text
local-data\screen-calibration\
├── .pogo-private-calibration
├── PRIVATE-README.txt
├── fixture-manifest.local.json
├── anchor-plan.local.json
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
└── reports\acceptance\
```

The marker file is mandatory. Real-screen commands refuse to use an arbitrary directory that has not been explicitly initialised.

## Capture coverage

Keep the Android phone fixed to one configuration:

- same physical phone
- same effective screen resolution
- portrait orientation
- same display size and font scale
- same Pokémon GO language
- same Android navigation mode
- no floating overlays except the UI being deliberately tested

Minimum target set:

- 3 visually different InventoryList screens
- 3 visually different PokemonDetails screens
- 3 visually different AppraisalOpen screens
- 1 or more examples of every menu, dialog and search state
- loading, popup and network-error examples where reproducible
- at least 4 Unknown negatives

Unknown negatives must include:

- unrelated screen
- partial Pokémon GO screen
- deliberately mixed/conflicting screen if safely reproducible
- unsupported orientation or layout

## File placement and indexing

Place each PNG under the folder matching its expected state. Example:

```text
fixtures\PokemonDetails\details-001.png
```

Then run:

```powershell
.\scripts\index-local-calibration.ps1
```

Indexing:

- computes SHA-256 for every PNG
- creates stable fixture ids
- infers the expected state from the folder name
- preserves approval only when the file hash is unchanged
- resets approval when a file changes
- rejects files outside a recognised state folder

## Manual safety approval

Open `fixture-manifest.local.json` and review each image. Set these fields to `true` only after visual review:

```json
{
  "accountIdentitySafe": true,
  "locationSafe": true,
  "notificationsSafe": true,
  "otherPersonalDataSafe": true,
  "approvedForCalibration": true
}
```

Also set `reviewedBy` and `reviewedAtUtc`.

A fixture is ignored until every safety field is true. If the PNG changes, its hash changes and approval is rejected or reset.

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
- dynamic candy, Stardust or weight values
- full-screen fingerprint

Each anchor lists fixture ids used to generate its reference samples:

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
  "sampleFixtureIds": ["pokemondetails-details-001", "pokemondetails-details-002"]
}
```

Required and Optional samples must come from the same state. Forbidden anchors may use a fixture from the state or overlay they are intended to reject.

## Build the local profile

```powershell
.\scripts\build-local-calibration-profile.ps1
```

The generated profile contains only compact fingerprints, not the original PNG files. It remains local and ignored by Git.

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
- minimum fixture coverage for every state
- at least 90 percent recall for the three core dynamic states
- Unknown recall of 100 percent

False negatives may be tolerated only within the explicit per-state recall threshold. They remain visible in the report. A false positive is a hard blocker.

## Negative-fixture tags

The acceptance engine evaluates every fixture for classification. For anchor-quality separation, these tags have special meaning on Unknown fixtures:

```text
mixed-state
partial-state
unsupported-layout
```

They are excluded only from individual anchor separation because they can intentionally contain a valid anchor. They are still included in the full false-positive acceptance test.

Use `unrelated` for a clean negative that should contribute to anchor separation.

## Stop condition

Do not begin Calcy integration or a continuous scanning loop until:

- all required real fixtures are approved
- acceptance is green
- false positives are zero
- misclassifications are zero
- weak anchors are zero
- the report has been manually reviewed
