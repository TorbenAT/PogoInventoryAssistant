# Project state

## Current version

0.14.3

## Build correction in 0.14.1

GitHub Actions built the existing projects and reached
`PogoInventory.Appraisal`, where `AppraisalAnalyzer.cs` failed with CS0173.

The conditional expression returned an `int` when a bar track was detected and
`null` otherwise. Because the local variable used `var`, the compiler had no
nullable target type available for the conditional expression.

Version 0.14.1 explicitly declares the variable as `int?`. The intended
behavior is unchanged: a measured candidate IV is 0 to 15, while an
unmeasurable bar remains null.

## Decoder correction in 0.14.2

The appraisal pretest initially terminated on `IMG_7699.png` because
`PngDecoder` reports unsupported PNG variants through `ScreenVisionException`,
while the appraisal runner only caught framework decoder exceptions.

Version 0.14.2 catches `ScreenVisionException` and retains the file name,
SHA-256, error code and error detail in the report. One unsupported image can
therefore no longer terminate an otherwise valid 23-image fixture set.

The known file can also be removed safely with:

```powershell
.\scripts\remove-known-unsupported-iphone-fixture.ps1
```

The script deletes only `IMG_7699.png` with the exact known SHA-256 and refuses
to delete a changed file.

## Build correction in 0.14.3

The 0.14.2 exception filter used `exception is` twice inside one C# `or`
pattern. After the first type pattern, the remaining alternatives must be type
names only.

Version 0.14.3 uses this valid pattern:

```csharp
catch (Exception exception) when (
    exception is ScreenVisionException or
    InvalidDataException or
    NotSupportedException or
    ArgumentException or
    OverflowException)
```

The runtime policy is unchanged: unsupported image files remain diagnostics and
do not terminate the appraisal pretest.

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
- 127 expected self-tests

### Real Android navigation and variant-safe evidence

The connected OnePlus 6T has completed a fresh 20-item appraisal scan with:

- 20 persisted captures and unique screenshot/fingerprint hashes
- 19 verified `SwipeNextPokemon` actions
- 3/3 stable phone calibration cases
- 20 Candidate-only appraisal observations and zero Complete observations
- 20 conservative REVIEW decisions and zero DELETE decisions
- schema-versioned semantic variant identity and per-run instance evidence

Unknown form, costume, background and special state values remain Unknown. They
cannot share an ordinary duplicate group or authorize DELETE.

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

Version 0.14.3 adds no phone input action.

## What the iPhone images now provide

The screenshots provide reusable normalised definitions and initial colour
thresholds. They do not lock the solution to iPhone pixels.

When the Android phone is connected, the profile searches translation and scale
around those definitions. A single visible appraisal screen can therefore
generate phone-specific definitions automatically.

## Not completed

- extraction of exact semantic variant identity from Android screenshots
- twenty-case appraisal truth verification
- verified Complete visual IV provider
- real Calcy evidence and provider selection
- species and CP extraction
- caught-on location/origin persistence for later tagging
- SQLite inventory database
- final tagging plan
- transfer remains manual

## Required checkpoint after push

1. Build all 15 projects.
2. Confirm 127 of 127 self-tests pass.
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
