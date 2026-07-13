# Project state

## Current version

0.3.1

## Hotfix status

The first 0.3.0 GitHub Actions build found a compile-time type mismatch in the PNG Paeth filter. Version 0.3.1 contains the narrow compiler fix plus a dedicated Paeth-filter regression fixture and exact pixel test. No functional milestone scope changed.

## Accepted previous checkpoint


Torben reported that the 0.2.0 GitHub Actions run was fully green. M1 is therefore accepted at CI level. A real-phone screenshot capture is still useful before later phone-specific calibration.

## Completed

### M0: Foundation

- repository and .NET 8 solution structure
- Pokémon observation model with nullable special-status fields
- JSON decision policy
- conservative KEEP / REVIEW / DELETE analysis
- duplicate grouping and strictly-better duplicate requirement
- preliminary PvP candidate preservation
- JSON and Markdown decision reports
- package-free self-tests

### M1: Read-only Device Harness

- isolated `PogoInventory.Device` project
- ADB and fake transports
- exact-one-authorised-device selection
- device, screen and battery metadata
- validated PNG screenshot capture
- atomic output and SHA-256 manifest
- timeouts, cancellation, structured errors and CI coverage
- no input-control API

### M2: Generic Screen State Detector

- isolated `PogoInventory.Vision` project
- package-free PNG decoder for normal Android screenshot formats
- decompression and dimension safety limits
- normalised screen regions
- Color, Grayscale and Edge fingerprints
- Required, Optional and Forbidden anchors
- profile validation with self-contained Base64 reference samples
- explicit orientation, resolution and aspect-ratio checks
- deterministic state scoring and winner-margin handling
- `Unknown` for incomplete, conflicting or unsupported screens
- detailed JSON evidence reports
- CLI commands `screen-detect` and `screen-fingerprint`
- synthetic, non-personal fixtures for all initial states
- tests for known, incomplete, conflicting and landscape screens
- CI commands for synthetic detection and fingerprint extraction

## Important limitation

The detector architecture is implemented, but the supplied profile is synthetic. It cannot yet classify real Pokémon GO screenshots. Phone-specific and layout-specific anchors must be created from real, redacted screenshots.

## Not completed

- compilation of 0.3.1 in the assistant build environment
- 0.3.1 GitHub Actions acceptance
- real Pokémon GO screenshot fixture set
- real-screen anchor calibration and false-positive testing
- popup and network-error examples from the actual phone
- Calcy integration
- OCR and icon recognition
- inventory scanning loop
- SQLite database and checkpoints
- exact Pokémon fingerprinting
- full PvPoke / Ohbem integration
- device-side tagging

## Required checkpoint after push

1. Confirm the 0.3.1 GitHub Actions run is green.
2. Run `scripts\detect-synthetic-screen.ps1` locally.
3. Confirm the report selects `InventoryList`.
4. Run `scripts\extract-synthetic-fingerprint.ps1`.
5. Capture a small, redacted set of real Pokémon GO screens at the phone's fixed resolution.
6. Keep those screenshots outside the public repository until they are reviewed and redacted.

## Next recommended milestone

M2b: Real-screen calibration and detector acceptance.

Required real screenshots:

- inventory list
- Pokémon details
- appraisal open
- Pokémon menu open
- tag dialog open
- search open
- loading screen if reproducible
- ordinary popup
- network-error popup or banner if reproducible
- at least three visually different examples of the normal dynamic screens

The next milestone must create a local private profile, select stable anchors and prove that unknown and conflicting screens fail closed. It must remain read-only.

## Design decisions preserved

- C# and .NET 8
- no hidden game API
- no automatic transfer
- no anti-detection behaviour or human imitation
- unknown data results in REVIEW, never DELETE
- DELETE requires exact identity and a documented better duplicate
- all ADB execution remains isolated in `PogoInventory.Device`
- vision is independent of ADB and file storage
- every release updates project state and continuation prompt
