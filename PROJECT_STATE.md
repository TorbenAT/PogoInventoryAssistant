# Project state

## Current version

0.14.3

## Task 5 sequence orchestration checkpoint

`VerifiedInventoryTaskSequence` composes only
`IVerifiedInventoryNamedOperations`. It validates bounded limits and tags,
atomically checkpoints each item, resumes only on matching request context,
preserves Partial evidence, attempts the named ReturnToInventory recovery and
continues only after Inventory is verified. It stops input on Unknown or failed
Partial recovery and assigns ordinal instance IDs independently of hashes.
AI-Delete cannot be auto-applied and no delete operation is exposed. This
checkpoint is offline-only; real-phone Task 5 acceptance is not claimed.

## Task 4 dynamic identity implementation checkpoint

`PokemonDetailsIdentityAnalyzer` keeps the concrete PNG SHA-256 evidence hash
separate from a stable multi-ROI fingerprint. It records dynamic tag-section
bounds, mutable tag observation, lower-content anchor evidence and three-frame
consensus. `PokemonIdentityInstance` assigns `ScanRunId:ordinal` independently
of both hashes, so identical fingerprints remain separate instances.

Offline self-tests are 155/155. Three real five-frame Details groups complete
with the tuned profile; a fourth captured group is Inventory rather than
Details and is correctly Unavailable. The zero/one/two-tag Task 3 captures are
counted as 0/1/2, and the zero-tag versus tagged stable fingerprint similarity
is 0.9815 against the 0.965 threshold. Real-phone acceptance remains PARTIAL
because the complete three-state acceptance set is not a 20-Pokémon provider
gate and one local group is not a Details screen. No real-phone Task 4 approval
claim is made.

## Verified tag selection by name accepted on 2026-07-20

`device-set-pokemon-tag` now identifies visible rows geometrically and matches
the requested name through an ignored, device-calibrated visual profile at
three bounded normalized scales. It requires confidence plus separation from
the second-best row. Row position and row ordinal are never identity.

The operation verifies Details, Menu, selector visibility, selected/unselected
check state, Done and the resulting Details tag pill. It scrolls at most the
profile limit and records `TAG_NOT_FOUND_NO_MUTATION` without tapping any row
when a name is unavailable. Requests already in the desired state perform zero
row mutations.

Two real Trade add/remove cycles passed on Ekans CP616 with zero wrong tag
selections. Each addition was confirmed by the selector, Details and `#Trade`;
each removal was confirmed by the selector, Details, an empty `#Trade`, and
Ekans under `!#Trade`. Final tag state is unselected and Inventory is
unfiltered. Build passes and 148/148 self-tests pass. Real profiles and images
remain under ignored `local-data`.

Additional Task 3 acceptance passed for all four existing AI tag names. Each
named row was independently matched, selected, committed, observed on Details
and removed with zero wrong selections. Dynamic pill counting accepted zero,
one and two simultaneous tags and verified the 2 -> 1 -> 0 removal sequence.
AI-Delete was only a reversible tag-name test; no destructive action exists or
ran. The final phone state is unfiltered Inventory and Ekans CP616 has none of
the tested tags. Build passes and 149/149 self-tests pass.

## Verified Inventory Search accepted on 2026-07-20

`device-search-inventory` now owns a bounded Open -> Clear -> Enter -> Submit
workflow. It requires a visually verified Inventory search surface before any
input, checks each postcondition, records the expected ordinary query and local
screenshot hashes, and can clear the completed query. Android shell escaping is
centralized inside `PogoInventory.Device`; higher layers never supply raw ADB
syntax.

Two complete real-phone rounds passed for `age0-7`, `age0-365`, `age0-1825`,
`#Trade` and `!#Trade`. Visual review confirmed the exact text and result lists;
the visible counts were 7, 7, 303 and 0 for the currently empty Trade tag. The
`!#Trade` result was populated. Both rounds ended in unfiltered Inventory. The
first pre-input attempt stopped safely when the Search placeholder was too
broadly classified; the repaired analyzer was then accepted in all ten runs.
Build passes and 146/146 self-tests pass.

## Guarded appraisal recovery accepted on 2026-07-20

The recovery increment uses state-specific ROI stability rather than
full-screen pixels. Appraisal intro and appraisal bars each use documented
regions and a three-of-five consensus. `GuardedInventoryRecovery` owns every
transition and action-limit rule; the CLI recovery command contains no parallel
inline state machine. The self-test total is 144/144.

Real-phone acceptance passed on the connected OnePlus A6013 for three complete
Inventory -> Details -> Menu -> AppraisalIntro -> AppraisalBars -> Details ->
Inventory cycles. The documented left-middle normalized target `(0.1001,
0.5002)` is used once per appraisal substate. Android Back is never sent from
AppraisalBars and is sent once only after PokemonDetails is verified. The run
recorded zero Unknown states and zero wrong post-states. Evidence is retained
under ignored `local-data/validation/sol-high-android-implementation`.

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
- 134 expected self-tests

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

`scripts/start-night-evidence-scan.ps1` runs the same conservative appraisal
evidence path with profile hashes, heartbeat, battery/disk/device safety checks,
an item limit and a wall-clock limit. It performs no transfer or tagging action.

### Real phone validation update on 2026-07-19

The connected OnePlus A6013 completed a fresh 3-item real-phone appraisal run
with:

- 3/3 calibration cases marked stable
- zero Complete observations
- 2/2 verified swipes
- 3 candidate observations
- real `phone-calibration-stability.md` and `phone-calibration-stability.json`
- zero transfer actions

Calcy evidence on the same device was also rechecked:

- `calcy-probe` reported `CandidateEvidenceFound`
- the installed package was `tesmath.calcy` version `3.44`
- overlay permission was proven
- accessibility and running-service surfaces remained non-observational
- `calcy-live-check` completed one navigation item and the read-only probe path
- no parsed observation was produced because no parser profile was supplied

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

## Game-state detector and guarded recovery iteration (2026-07-20)

Added a shared read-only `PokemonGoGameStateDetector` in the exploration layer.
It reuses the existing visual control anchors and appraisal analyzer and emits
one of Inventory, PokemonDetails, PokemonMenu, Appraisal or Unknown together
with confidence, evidence and screenshot SHA-256. Added CLI commands
`device-detect-game-state` and `device-recover-inventory`; recovery sends at
most two Back actions and stops on Unknown or an unexpected post-state.

The real phone detector identified the current Details screen at confidence
1.000. The guarded recovery sent one Back, but the stable post-action frame
remained Details, so the run stopped without a second blind action.

## Not completed

### Gameplay map state detection (2026-07-20)

The shared detector now has an explicit `GameplayMap` state. It checks the
existing main-menu Poké Ball anchor before Inventory and Details, preventing
the map's lower-right teal control from being misclassified as PokemonDetails.
PokemonDetails now also requires an independent details-page topology anchor.
The saved real map frame is detected as GameplayMap at confidence 1.000.
No phone input was sent in this iteration.

- extraction of exact semantic variant identity from Android screenshots
- twenty-case appraisal truth verification
- verified Complete visual IV provider
- real Calcy provider selection and verified parsed-observation extraction
- species and CP extraction
- caught-on location/origin persistence for later tagging
- SQLite inventory database
- final tagging plan
- transfer remains manual

## Required checkpoint after push

1. Build all 18 projects.
2. Confirm 138 of 138 self-tests pass.
3. Confirm the existing iPhone evidence stages remain green.
4. Confirm appraisal pretest finds at least five candidates.
5. Confirm candidates are concentrated at least 70 percent in one cluster.
6. Confirm the unverified profile produces zero Complete observations.
7. Confirm `appraisal-review-pack.zip` is created.
8. Preserve zero new phone actions.

The M1 real-phone hardening is now implemented and pushed. WiFi ADB and wall
charging were stable during the controlled run, which captured 125 unique
appraisal frames. The run stopped safely at `UnknownScreen` when the current
inventory position was exhausted, so the 30-minute appraisal acceptance test
is still open and must not be reported as passed.

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

The first three steps were completed on 2026-07-19 against the connected
OnePlus A6013. Until the 20-case verification gate is passed, improve only
diagnostics and verification scaffolding. Do not add location changes,
transfer automation, anti-detection logic or arbitrary shell execution.
