# Release 0.5.0

## Guided private real-screen capture

This release closes the tooling gap between synthetic calibration and real phone calibration. It provides a controlled way to collect real screenshots without adding phone input automation.

## Added

- versioned `capture-plan.local.json`
- private `incoming/<ScreenState>/` capture folders
- `capture-session.local.json` with capture-plan fingerprint, device, geometry, hash and sequence locks
- `calibration-capture`
- `calibration-capture-session`
- `calibration-capture-status`
- `calibration-capture-approve`
- guided PowerShell scripts
- duplicate screenshot detection
- capture coverage report
- explicit local privacy-review confirmation
- safe promotion into the approved fixture manifest without overwriting untracked files
- idempotent promoted-fixture verification and recovery
- thirteen new self-tests

## Safety properties

- phone navigation remains manual
- the device interface remains read-only
- no tap, swipe, text input, app launch or arbitrary shell command was added
- screenshots remain unapproved in `incoming` until explicit review
- changed captures fail SHA-256 verification
- a capture session is locked to the exact capture plan, one device serial and one exact screenshot geometry
- pixel-identical duplicates do not count toward variation coverage
- duplicate captures cannot be promoted
- real data remains ignored by Git

## Expected CI

- build all six projects with warnings as errors
- run 45 package-free self-tests
- run all previous synthetic analysis, device, vision and calibration checks
