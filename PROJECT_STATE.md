# Project state

## Current version

0.4.0

## Accepted checkpoint

Torben reported that the complete 0.3.1 GitHub Actions run was green. The foundation, read-only Device Harness, generic Screen State Detector and PNG Paeth regression fix are accepted at CI level.

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
- private workspace marker and standard local layout
- fixture indexing by expected-state folder
- SHA-256 fixture locking
- approval preservation only for unchanged files
- automatic approval reset after file changes
- path traversal protection
- explicit privacy and redaction review fields
- versioned fixture manifest
- versioned anchor plan
- multiple sample fixtures per anchor
- local screen-profile generation
- acceptance policy with per-state coverage and recall
- false-positive, false-negative and misclassification classification
- confusion matrix
- weak-anchor and positive-negative separation analysis
- JSON, Markdown and CSV reports
- synthetic end-to-end profile generation and acceptance in CI
- PowerShell workflow scripts
- calibration and fixture-approval documentation

## Important limitation

The calibration engine is complete, but no real screenshots or real Pokémon GO anchor plan have been supplied. The committed profile remains synthetic and must not be used for phone automation.

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

1. Confirm GitHub Actions is green for version 0.4.0.
2. Confirm all 34 self-tests pass.
3. Confirm synthetic calibration profile generation succeeds.
4. Confirm synthetic acceptance returns ACCEPTED with zero false positives, misclassifications, false negatives and weak anchors.
5. On the Windows computer, initialise `local-data\screen-calibration`.
6. Capture and approve the first real fixture set.
7. Keep all real screenshots and generated local profiles out of Git.

## Next recommended milestone

M2c: Real-screen fixture capture, anchor selection and detector acceptance.

The next milestone is data-driven. Do not begin Calcy integration until the real fixture report has:

- zero false positives
- zero wrong known-state classifications
- zero weak anchors
- required state coverage
- accepted recall thresholds

## Design decisions preserved

- C# and .NET 8
- no hidden game API
- no automatic transfer
- no anti-detection or human imitation
- unknown data produces REVIEW or stop
- DELETE requires exact identity and a better documented duplicate
- all ADB process execution remains isolated in `PogoInventory.Device`
- calibration and vision remain read-only
- real data stays local while the repository is public
- every release updates project state and continuation prompt
