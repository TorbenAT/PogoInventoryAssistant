# Project state

## Current version

0.10.1

## Accepted checkpoint

Torben reported version 0.9.0 fully green. Version 0.10.0 then processed the 24 committed iPhone screenshots and produced this real result:

- 23 of 24 images decoded
- one geometry group
- four visual clusters
- no exact duplicates
- no near duplicates
- one image failed decoding

The 0.10.0 gate rejected the entire pretest solely because one extra image failed, even though 23 usable screenshots exceeded the requested minimum of 20. Version 0.10.1 corrects that gate.

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
- Calcy package and live-check probe
- profile-driven raw-text parser
- twenty-case Calcy provider verification gate
- zero-false-Complete gate and provider hash locking

### M4 phase 4a: iPhone image pretest

- deterministic PNG enumeration
- package-free decoding
- geometry, orientation, length and SHA-256 inventory
- combined grayscale and edge fingerprints
- all-pairs visual similarity
- exact and near-duplicate detection
- deterministic visual clustering
- JSON, Markdown and CSV reports
- CI processing of `data/iphone-images/*.png`

### 0.10.1 correction

- `--min-images` now means successfully decoded images
- default minimum decode rate is 90 percent
- isolated rejected files remain warnings and diagnostics
- widespread decoder failure still stops CI
- console output prints rejected filename, error type and error detail
- Markdown contains a rejected-images table
- two regression tests added, bringing the expected total to 86

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

Version 0.10.1 adds no phone input action.

## Required checkpoint after push

1. Build all 12 projects.
2. Confirm 86 of 86 self-tests pass.
3. Confirm the committed iPhone pretest runs automatically.
4. Confirm 23 of 24 images decode.
5. Confirm the 95.8 percent decode rate exceeds the 90 percent requirement.
6. Confirm the pretest is accepted.
7. Record the rejected filename and exact decoder error from the Action log or report.
8. Preserve zero new phone input actions.

## Next recommended milestone

Use the accepted iPhone report to build a deterministic visual-region discovery report. Keep all region labels provisional until supported by real evidence.

When the Android phone is available:

1. run `calcy-probe`
2. run `calcy-live-check`
3. compare Android screenshots with the iPhone visual groups
4. implement only the extraction mechanism proven by real evidence
5. collect 20 real verification cases automatically
6. pass the existing zero-wrong-Complete gate

## Design decisions preserved

- no hidden game API
- no transfer automation
- no anti-detection or human imitation
- no random timing or coordinates
- deterministic state-aware waiting
- Unknown means stop
- incomplete data stays incomplete
- source screenshots are never modified
- rejected images remain traceable rather than silently discarded
