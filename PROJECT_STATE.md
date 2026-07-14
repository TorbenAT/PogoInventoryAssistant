# Project state

## Current version

0.13.1

## Build correction in 0.13.1

GitHub Actions compiled the existing projects until
`PogoInventory.CropAtlas`, where `SemanticEvidenceRunner.cs` failed with three
CS0103 errors. The runner reused `PixelImageTransforms` and `CropAtlasJson`,
which are declared in `PogoInventory.CropAtlas.Services`, but the nested
semantic namespace did not import that namespace.

Version 0.13.1 adds the missing namespace import. No crop logic, semantic
policy, report schema, test expectation or phone-action boundary changed.

## Accepted checkpoint

Torben reported version 0.12.0 fully green in GitHub Actions.

Accepted real iPhone evidence:

- 24 committed PNG screenshots
- 23 successfully decoded screenshots
- 95.8 percent decode rate
- one screenshot geometry group
- four automatically discovered visual clusters
- zero exact duplicate pairs
- zero near-duplicate pairs
- one rejected PNG retained as a decoder diagnostic
- accepted visual-region discovery
- accepted crop-atlas generation

## Completed

### M0 to M3

- .NET 8 foundation
- conservative KEEP, REVIEW and DELETE analysis
- Android device harness
- deterministic screen detection and calibration
- automatic inventory navigation
- ordered local evidence, checkpoints and safe resume
- four named phone actions only

### M4 Calcy and observation foundation

- automatic core-profile bootstrap
- nullable structured observations
- checkpoint schema 2.0
- Calcy package and live-check probe
- profile-driven raw-text parser
- twenty-case zero-wrong-Complete verification gate
- provider selection locked to report and parser hashes

### M4 iPhone visual evidence

- real PNG decoding and geometry inventory
- SHA-256 evidence
- grayscale and edge fingerprints
- all-pairs similarity and clustering
- normalised 12 by 24 visual-region discovery
- stable, changing and cluster-separating region candidates
- deterministic crop atlas
- representative evidence for every visual cluster
- package-free derived PNG output
- explicit cluster-coverage readiness

### M4 phase 4d: semantic evidence review pack

Version 0.13.0 adds:

- one evidence case per decoded screenshot
- one derived crop per case and selected candidate region
- copied cluster overview and candidate contact sheets
- deterministic JSON, Markdown and CSV manifests
- `semantic-review-pack.zip`
- intentionally empty truth values for screen state, species, CP and IV
- external-review readiness
- automated-extraction readiness fixed to false
- exact underrepresented-cluster reporting
- no source screenshot copied into the review ZIP
- 103 expected self-tests

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

Version 0.13.1 adds no phone input action.

## Not completed

- semantic labels for the four real visual clusters
- populated truth data for the real screenshots
- OCR of name, CP, HP or other text
- IV-bar measurement from real screenshots
- status-marker extraction
- real Android Calcy evidence
- production `ICalcyObservationProvider`
- automatic twenty-case real provider verification
- SQLite inventory database
- full real-inventory decision plan
- automatic tagging
- transfer remains manual and is not implemented

## Required checkpoint after push

1. Build all 14 projects.
2. Confirm 103 of 103 self-tests pass.
3. Confirm the accepted iPhone pretest and region discovery remain unchanged.
4. Confirm crop-atlas generation remains accepted.
5. Confirm semantic evidence contains at least twenty cases.
6. Confirm every case contains every selected candidate region.
7. Confirm `semantic-review-pack.zip` is created.
8. Confirm the truth template starts unreviewed with null semantic fields.
9. Record `needsMoreImages` and any named underrepresented clusters.
10. Preserve zero new phone actions.

## Next recommended milestone

Inspect the real semantic review pack and select one evidence-backed semantic
experiment:

- name and CP extraction, or
- appraisal-bar measurement

Do not implement both merely because both are possible. Select the field with
the clearest and most consistent crop evidence, populate truth for at least
twenty cases, and require zero false Complete results.

More source screenshots should be requested only when the semantic report
names an underrepresented cluster or the selected field lacks enough visible
variation.

## Design decisions preserved

- no hidden game API
- no transfer automation
- no anti-detection logic
- no random timing or coordinates
- deterministic state-aware waiting
- Unknown means stop
- incomplete data remains incomplete
- screenshots are read only
- generated review data contains derived crops, not copied full screenshots
