# Release 0.14.0

## Purpose

Make the repository ready for the fixed Android phone while reusing the real
iPhone screenshots as normalised definitions rather than fixed iPhone pixels.

## Offline capability

The appraisal pretest searches and measures the three IV bars across the
committed screenshots. It outputs diagnostics and candidate values.

## Phone capability

`phone-prepare` captures one read-only Android screenshot. When the user has
manually opened an appraisal screen, the command creates a device-adjusted
profile containing normalised regions for that phone geometry.

## Safety

- no new phone input action
- no automatic navigation
- source profile unverified
- candidate values cannot become Complete
- generated device profile unverified
- Calcy probe still required before provider selection

Expected total: 112 self-tests.
