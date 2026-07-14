# Project state

## Current version

0.12.0

## Build correction in 0.12.0

GitHub Actions compiled the new `PogoInventory.RegionDiscovery` project but the
CLI failed with CS0246/CS0103 because `Program.cs` lacked the explicit
`PogoInventory.RegionDiscovery.Models` and
`PogoInventory.RegionDiscovery.Services` imports.

Version 0.12.0 adds those two imports. The project reference was already
present and no algorithm or phone-action boundary changed.

## Accepted checkpoint

Torben reported that version 0.11.1 is fully green in GitHub Actions.

The accepted real iPhone pretest result is:

- 24 committed PNG screenshots
- 23 successfully decoded screenshots
- 95.8 percent decode rate
- one screenshot geometry group
- four automatically discovered visual clusters
- zero exact duplicate pairs
- zero near-duplicate pairs
- one rejected PNG retained as a decoder diagnostic

This proves the real screenshots can pass through the package-free image pipeline. It does not yet prove semantic Pokémon-data extraction.

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

### M4 phase 4a: real iPhone image pretest

- real PNG decoding
- geometry and orientation inventory
- SHA-256 evidence
- grayscale and edge fingerprints
- all-pairs visual similarity
- duplicate and near-duplicate checks
- automatic visual clustering
- accepted isolated decode-failure policy

### M4 phase 4b: visual-region discovery

New `PogoInventory.RegionDiscovery` project:

- normalised 12 by 24 screen grid
- mean luminance and edge density per cell
- global cross-image variation
- consecutive-image variation
- within-cluster variation
- between-cluster separation
- provisional stable-chrome candidates
- provisional screen-state discriminator candidates
- provisional dynamic-content candidates
- provisional text-dense candidates
- JSON, Markdown and three CSV reports
- deterministic candidate grouping
- CI execution against the committed iPhone screenshots
- 91 expected self-tests

All candidate meanings are provisional. Version 0.12.0 does not perform OCR and does not claim species, CP or IV extraction.

### M4 phase 4c: crop atlas and evidence sufficiency

New `PogoInventory.CropAtlas` project:

- deterministic selection of strong candidate regions
- same-kind overlap suppression
- representative images from every visual cluster
- read-only crops from the committed screenshots
- package-free PNG encoding for derived evidence
- cluster overview contact sheet
- one contact sheet per candidate region
- crop, cluster and region manifests
- explicit `NeedsMoreImages` decision
- exact underrepresented cluster identifiers
- no semantic screen label, OCR or IV claim
- 97 expected self-tests

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

Version 0.12.0 adds no phone input action.

## Not completed

- semantic labels for the four real visual clusters
- OCR of name, CP, HP or other text
- IV-bar measurement from real screenshots
- status-marker extraction
- real Android Calcy evidence
- production `ICalcyObservationProvider`
- automatic twenty-case real evidence collection
- SQLite inventory database
- full real-inventory decision plan
- automatic tagging
- transfer remains manual and is not implemented

## Required checkpoint after push

1. Build all 14 projects.
2. Confirm 97 of 97 self-tests pass.
3. Confirm the accepted iPhone pretest remains unchanged.
4. Confirm region discovery still produces 288 cells and four real visual clusters.
5. Confirm crop-atlas generation is accepted.
6. Confirm every selected region contains evidence from every cluster.
7. Inspect `readiness.needsMoreImages` and the named underrepresented clusters.
8. Inspect `cluster-overview.png` and the candidate contact sheets in `validation-output`.
9. Preserve zero new phone actions.

## Next recommended milestone

Use the real crop atlas to choose the first evidence-backed semantic experiment:

- map visual clusters to provisional screen-state names only when the contact sheets make the distinction unambiguous
- identify whether a consistent name/CP crop exists
- identify whether appraisal bars occupy a consistent region
- add measurement only for fields supported by the real crops
- keep unsupported fields null
- request more screenshots only for the exact clusters listed by the crop-atlas readiness report

When the Android phone becomes available, the real Calcy probe remains mandatory before selecting a production provider.

## Design decisions preserved

- no hidden game API
- no transfer automation
- no anti-detection logic
- no random timing or coordinates
- deterministic state-aware waiting
- Unknown means stop
- incomplete data remains incomplete
- screenshots are read only
- real data remains local or explicitly committed by Torben
