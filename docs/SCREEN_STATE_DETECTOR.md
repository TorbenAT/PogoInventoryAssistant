# Screen State Detector

## Purpose

The detector answers one narrow question:

> Which approved screen state does this PNG most reliably represent?

It never controls the phone.

## Output states

- InventoryList
- PokemonDetails
- AppraisalOpen
- PokemonMenuOpen
- TagDialogOpen
- SearchOpen
- Loading
- Popup
- NetworkError
- Unknown

`Unknown` is a valid and expected safety result.

## Profile structure

A profile defines:

- required orientation
- minimum dimensions
- allowed aspect-ratio range
- minimum state score
- minimum winner margin
- state definitions
- anchors for each state

Each anchor defines:

- a unique name inside the state
- a normalised region
- Color, Grayscale or Edge mode
- Required, Optional or Forbidden expectation
- fingerprint dimensions
- match threshold
- weight
- one or more Base64 reference fingerprints

Reference fingerprints are stored in the profile rather than linked image paths. This makes classification deterministic and keeps the runtime independent of the original reference screenshots.

## Why normalised regions

A fixed pixel rectangle only works at one resolution. A normalised region uses coordinates from 0 to 1 and is converted to pixels for the current screenshot.

Example:

```json
{
  "x": 0.05,
  "y": 0.70,
  "width": 0.25,
  "height": 0.20
}
```

The profile still validates orientation, minimum dimensions and aspect ratio. Normalisation does not mean every phone layout is automatically compatible.

## Classification rules

For each anchor:

1. Crop the normalised region.
2. Reduce it to a fixed fingerprint grid.
3. Compare it with all samples.
4. Keep the best similarity.
5. Apply the anchor expectation.

For each state:

- every Required anchor must match
- every Forbidden anchor must not match
- Optional anchors contribute without being mandatory
- positive anchors produce a weighted score

Across states:

- states below the minimum score are rejected
- a weak winner margin results in Unknown
- unsupported geometry results in Unknown before anchor evaluation

## Evidence report

`screen-detect` writes a complete JSON report. It contains enough information to explain every decision:

- selected state and confidence
- image geometry
- global reasons
- every state's score and eligibility
- every rejection reason
- every anchor's similarity, threshold and condition result

This report is intended for troubleshooting and later audit logs.

## Synthetic profile

The committed synthetic profile validates the engine and CI. It is not trained on Pokémon GO and must not be used as a real detector.

Synthetic fixtures include:

- one known fixture for every state
- incomplete content
- conflicting state evidence
- controlled visual noise
- unsupported landscape orientation

They contain no personal or account data.

## Real calibration workflow

Version 0.4.0 implements the workflow described in `REAL_SCREEN_CALIBRATION.md`. Use the ignored private workspace:

```text
local-data\screen-calibration\
```

Recommended steps:

1. Fix phone resolution, display scaling, language and orientation.
2. Capture at least three examples of each dynamic screen.
3. Redact trainer names and other personal data.
4. Identify stable controls and panel geometry.
5. Avoid artwork, Pokémon names and numeric values.
6. Extract fingerprints with `screen-fingerprint`.
7. Add multiple samples where appearance changes.
8. Run all approved fixtures and inspect confusion.
9. Add partial and unrelated screenshots as negative tests.
10. Increase thresholds or improve anchors until false positives are zero.

## Choosing anchor modes

Use Color when a control has stable color and geometry.

Use Grayscale when color may change but luminance layout remains stable.

Use Edge when outlines and separators are stable but backgrounds vary.

Do not rely on one large full-screen fingerprint. Prefer several small, independent, stable anchors.

## Performance

Version 0.3.1 validates profiles and decodes reference samples on every detection. This is acceptable for isolated screenshots and tests. A later continuous scanner should compile and cache a validated profile once per run.

## Known limitations

- no OCR
- no icon-specific classifier
- no real Pokémon GO anchors
- no accepted real Pokémon GO profile
- no cross-device layout adaptation
- no CRC validation in the PNG decoder
- no continuous capture loop
