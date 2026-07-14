# Project state

## Current version

0.14.0

## Accepted checkpoint

Torben reported version 0.13.1 fully green in GitHub Actions.

Accepted real iPhone evidence:

- 24 committed PNG screenshots
- 23 decoded screenshots
- four visual clusters
- cluster 01 is inventory list
- cluster 02 is Pokémon details
- cluster 03 is appraisal
- cluster 04 is the details action menu
- accepted region discovery, crop atlas and semantic evidence pack

## Completed

### Foundation

- .NET 8 solution
- conservative KEEP, REVIEW and DELETE analysis
- read-only Android device harness
- deterministic screen calibration
- automatic inventory navigation limited to four named actions
- checkpoints and safe resume
- Calcy probe and verification gate

### iPhone evidence pipeline

- decoding, hashing, similarity and clustering
- normalised region discovery
- crop atlas and semantic evidence review pack
- no full source screenshot copied into review packs

### M4 phase 4e: appraisal definitions and phone preparation

Version 0.14.0 adds:

- `PogoInventory.Appraisal`
- normalised Attack, Defense and HP bar definitions
- automatic X/Y translation and uniform-scale fitting
- orange-fill and neutral-track measurement
- candidate IV estimates
- diagnostic overlays and bar crops
- iPhone appraisal pretest
- dominant-cluster concentration gate
- hard zero-Complete gate for unverified profiles
- read-only `phone-prepare`
- local device-adjusted profile generation
- Android readiness report
- 112 expected self-tests

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

Version 0.14.0 adds no phone input action.

## What the iPhone images now provide

The screenshots provide reusable normalised definitions and initial colour
thresholds. They do not lock the solution to iPhone pixels.

When the Android phone is connected, the profile searches translation and scale
around those definitions. A single visible appraisal screen can therefore
generate phone-specific definitions automatically.

## Not completed

- real Android phone preparation run
- three-screen Android profile stability check
- twenty-case appraisal truth verification
- verified Complete visual IV provider
- real Calcy evidence and provider selection
- species and CP extraction
- SQLite inventory database
- final tagging plan
- transfer remains manual

## Required checkpoint after push

1. Build all 15 projects.
2. Confirm 112 of 112 self-tests pass.
3. Confirm the existing iPhone evidence stages remain green.
4. Confirm appraisal pretest finds at least five candidates.
5. Confirm candidates are concentrated at least 70 percent in one cluster.
6. Confirm the unverified profile produces zero Complete observations.
7. Confirm `appraisal-review-pack.zip` is created.
8. Preserve zero new phone actions.

## Next recommended milestone

When the Android phone and PC are available:

1. manually open a Pokémon appraisal screen
2. run `scripts/prepare-android-phone.ps1`
3. inspect `phone-readiness.json`
4. confirm a device-adjusted profile is generated
5. repeat on at least three different Pokémon
6. compare fitted regions and candidate IV values
7. run Calcy probe and live check
8. collect twenty real verification cases before allowing Complete IV output

Until then, improve only diagnostics and verification scaffolding. Do not add
location changes, transfer automation, anti-detection logic or arbitrary shell
execution.
