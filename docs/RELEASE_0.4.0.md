# Release 0.4.0

Version 0.4.0 adds the real-screen calibration and acceptance harness.

## Added

- isolated `PogoInventory.Calibration` project
- private workspace marker and folder initialiser
- fixture indexing by expected-state folder
- SHA-256 locking and automatic approval reset on file change
- explicit five-part fixture safety review
- JSON anchor plan with multiple fixture samples per anchor
- local profile generation from approved PNG fixtures
- strict path-containment checks
- calibration acceptance runner
- per-state recall and coverage requirements
- false-positive, false-negative and misclassification accounting
- confusion matrix
- weak-anchor and similarity-separation report
- JSON, Markdown and CSV outputs
- synthetic calibration manifest and anchor plan
- CI profile generation and full synthetic acceptance validation
- local PowerShell workflow scripts
- real-screen calibration and fixture-approval documentation

## Safety

- no taps
- no swipes
- no text input
- no tagging
- no transfer
- no randomised or human-like behaviour
- real screenshots and generated local profiles remain ignored

## Limitation

The release provides the complete workflow, but no real Pokémon GO screenshots or real phone-specific anchors are included. Real-screen detector acceptance remains pending local fixture capture and calibration.
