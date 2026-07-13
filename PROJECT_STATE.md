# Project state

## Current version

0.5.0

## Accepted checkpoint

Torben reported that the complete 0.4.0 GitHub Actions run was green. The foundation, read-only Android Device Harness, generic Screen State Detector, PNG regression fix and synthetic calibration workflow are accepted at CI level.

## Completed

### M0: Foundation

- .NET 8 solution and repository structure
- Pokémon observation model
- configurable decision policy
- conservative KEEP / REVIEW / DELETE engine
- duplicate grouping and strictly-better duplicate requirement
- preliminary PvP preservation
- decision reports and package-free self-tests

### M1: Read-only Device Harness

- isolated `PogoInventory.Device` project
- ADB and fake transports
- exact-one-authorised-device selection
- device, screen and battery metadata
- validated screenshot capture
- atomic files and SHA-256 manifest
- timeouts, cancellation and structured errors
- no input API

### M2a: Generic Screen State Detector

- isolated `PogoInventory.Vision` project
- package-free PNG decoder
- normalised UI regions
- Color, Grayscale and Edge fingerprints
- Required, Optional and Forbidden anchors
- geometry validation
- deterministic state score and winner margin
- fail-closed Unknown
- detailed evidence reports
- synthetic fixtures and CI tests

### M2b: Calibration and acceptance harness

- isolated `PogoInventory.Calibration` project
- private workspace marker and local layout
- fixture indexing by expected-state folder
- SHA-256 fixture locking
- approval preservation only for unchanged files
- automatic approval reset after file changes
- path traversal protection
- explicit privacy and redaction review fields
- versioned fixture manifest and anchor plan
- multiple sample fixtures per anchor
- local screen-profile generation
- acceptance policy with per-state coverage and recall
- false-positive, false-negative and misclassification classification
- confusion matrix
- weak-anchor and positive-negative separation analysis
- JSON, Markdown and CSV reports
- synthetic end-to-end profile generation and acceptance in CI

### M2c-a: Guided private real-screen capture tooling

- versioned capture plan with state-specific instructions and variation targets
- capture-plan SHA-256 lock preventing mid-session requirement changes
- private `incoming/<ScreenState>/` staging area
- read-only ADB screenshot capture after explicit user confirmation
- fixed-device serial lock for each capture session
- exact image-geometry lock for each capture session
- portrait and minimum-resolution enforcement
- SHA-256 capture-session integrity checks
- pixel-identical duplicate detection
- duplicate captures excluded from variation coverage
- required-state progress and next-state recommendation
- JSON and Markdown capture-status reports
- explicit privacy confirmation before fixture promotion
- reviewed capture promotion into the local fixture manifest
- protection against overwriting untracked fixture files
- promoted fixture hash linkage and idempotent retry handling
- guided and single-state PowerShell scripts
- no taps, swipes, text input, app launching or game changes

## Important limitation

The software can now collect the required real screenshots safely, but no real screenshots or phone-specific anchors have been supplied. The committed detector profile remains synthetic and must not be used for an inventory scanner.

## Not completed

- real Android screenshot fixture set
- real phone-specific anchor plan
- accepted real screen profile
- Calcy integration
- OCR or icon recognition
- inventory scanning state machine
- SQLite and checkpoints
- exact Pokémon identity
- full PvPoke / Ohbem integration
- any input-control or tagging executor

## Required checkpoint after push

1. Confirm GitHub Actions is green for version 0.5.0.
2. Confirm all 47 self-tests pass.
3. On the Windows computer, rerun `init-local-calibration.ps1` to upgrade the private workspace.
4. Connect the fixed Android phone with USB debugging enabled.
5. Run `start-local-calibration-capture.ps1`.
6. Capture the required core, negative, Loading and NetworkError states through manual phone navigation. Loading and NetworkError are ordered last and may be completed later, but profile acceptance remains blocked until both exist.
7. Review every incoming screenshot locally.
8. Promote only screenshots that pass the full privacy checklist.
9. Keep all real screenshots, session files, device serials and generated profiles out of Git.

## Next recommended milestone

M2c-b: Real-screen fixture collection, anchor selection and detector acceptance.

The next development work is data-driven. Do not begin the Calcy spike until the real fixture report has:

- zero false positives
- zero wrong known-state classifications
- zero weak anchors
- required state coverage
- accepted recall thresholds
- manual review of every promoted fixture

## Design decisions preserved

- C# and .NET 8
- no hidden game API
- no automatic transfer
- no anti-detection or human imitation
- phone navigation remains manual during calibration capture
- unknown data produces REVIEW or stop
- DELETE requires exact identity and a better documented duplicate
- all ADB process execution remains isolated in `PogoInventory.Device`
- vision remains independent of device control
- calibration may use the read-only device interface only for screenshots
- real data stays local while the repository is public
- every release updates project state and continuation prompt
