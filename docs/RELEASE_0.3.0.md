# Release 0.3.0

## Scope

M2 Generic Screen State Detector.

## Added

- package-free PNG decoding
- normalised screen regions
- Color, Grayscale and Edge fingerprints
- Required, Optional and Forbidden anchors
- validated JSON profiles with embedded reference samples
- deterministic classification and conflict handling
- full JSON evidence reports
- synthetic fixtures and fail-closed tests
- screen detection and fingerprint CLI commands

## Safety

This release remains fully read-only. No phone input or account-changing function was added.

## Acceptance

Push the release and require a green CI run. Then run the two new PowerShell scripts locally.

The synthetic detector must classify `InventoryList.png` as `InventoryList`.

## Limitation

The synthetic profile is not a real Pokémon GO detector. Real-screen calibration is the next milestone and must use private redacted screenshots.
