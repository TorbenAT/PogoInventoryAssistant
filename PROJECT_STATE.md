# Project state

## Current version

0.10.0

## Accepted checkpoint

Torben reported that version 0.9.0 is fully green in GitHub Actions.

Torben then committed 24 uncropped iPhone screenshots under `data/iphone-images` in commit `d83a3f5f8010a1fc301830176007dbe010a397ce`.

Version 0.10.0 uses those screenshots for an automatic cross-platform image pretest while the fixed Android phone and Windows PC are unavailable.

## Completed

### M0 to M3

- .NET 8 foundation and conservative KEEP, REVIEW and DELETE engine
- read-only Android device harness
- deterministic screen detection and calibration
- automatic inventory navigation with only four named actions
- local evidence, checkpoints and safe resume

### M4 phases 1 to 3

- automatic core-profile bootstrap
- structured nullable Calcy observations
- checkpoint schema 2.0
- package, process, accessibility, app-ops, service and log probe
- automatic one-Pokémon live check
- profile-driven raw-text parser
- twenty-case Calcy provider verification gate
- zero-false-Complete gate and provider hash locking

### M4 phase 4a: iPhone image pretest

New `PogoInventory.ImagePretest` project:

- deterministic PNG enumeration
- package-free decoding of real iPhone screenshots
- geometry, orientation, length and SHA-256 inventory
- combined grayscale and edge fingerprints
- all-pairs visual similarity
- exact and near-duplicate detection
- deterministic visual clustering
- JSON, Markdown and CSV reports
- CI gate over committed `data/iphone-images/*.png`
- six new self-tests, bringing the expected total to 84

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

Version 0.10.0 adds no phone input action.

## What the iPhone pretest proves

A green run proves that the real screenshots decode and can be normalised, fingerprinted, compared, clustered and reported by the current image pipeline.

## What it does not prove

- ADB connectivity
- Android control-point coordinates
- Android timing and animation waits
- Calcy package output
- Calcy overlay extraction
- Android end-of-inventory detection

## Required checkpoint after push

1. Build all 12 projects.
2. Confirm 84 of 84 self-tests pass.
3. Confirm the committed iPhone image pretest runs automatically.
4. Confirm at least 20 images are found.
5. Confirm every committed PNG decodes.
6. Confirm every committed image is portrait.
7. Download or inspect `iphone-image-pretest.json` from the validation artifact.
8. Preserve zero new phone input actions.

## Next recommended milestone

Use the iPhone pretest report to identify the visual groups and stable normalised regions before the Android phone is available.

When the Android phone is available:

1. run `calcy-probe`
2. run `calcy-live-check`
3. compare Android screenshots with the iPhone clusters
4. select PID-windowed text or visual overlay extraction only from real evidence
5. collect 20 real verification cases automatically
6. pass the existing zero-wrong-Complete verification gate

## Design decisions preserved

- no hidden game API
- no transfer automation
- no anti-detection or human imitation
- no random timing or coordinates
- deterministic state-aware waiting
- Unknown means stop
- incomplete data stays incomplete
- source screenshots are never modified
- every release updates this file and `NEXT_PROMPT.md`
