# Project state

## Current version

0.11.0

## Accepted checkpoint

Torben reported that version 0.10.1 is fully green in GitHub Actions.

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

All candidate meanings are provisional. Version 0.11.0 does not perform OCR and does not claim species, CP or IV extraction.

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

Version 0.11.0 adds no phone input action.

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

1. Build all 13 projects.
2. Confirm 91 of 91 self-tests pass.
3. Confirm the iPhone pretest remains accepted with 23 decoded images.
4. Confirm visual-region discovery is accepted.
5. Confirm one geometry group and at least two clusters.
6. Confirm the report contains 288 grid cells.
7. Confirm at least one provisional candidate of each kind.
8. Inspect the candidate and cluster reports from `validation-output`.
9. Preserve zero new phone actions.

## Next recommended milestone

Use the real region report to build an automatic crop atlas and cluster explanation report:

- select the strongest non-overlapping state-discriminator regions
- create read-only crops for each cluster representative
- produce a compact contact-sheet manifest without copying private source images into Git
- determine whether the current images contain enough detail for name, CP and appraisal-bar experiments
- add OCR or IV-bar interpretation only after the crop evidence supports it

No additional screenshots are required before the 0.11.0 real report is inspected. More images should be requested only for a specific missing state or weak candidate region.

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
