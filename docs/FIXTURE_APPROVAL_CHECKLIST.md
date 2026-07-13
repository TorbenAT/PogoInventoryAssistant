# Fixture approval checklist

Use this checklist for every real screenshot before running `approve-local-calibration-capture.ps1` or setting `approvedForCalibration` to true.

## Privacy

- [ ] Trainer name, account name and avatar identity are absent or acceptable for private local use.
- [ ] Location names, map information and coordinates are absent.
- [ ] Android notifications, contact names and message previews are absent.
- [ ] Email addresses, authentication data and other personal information are absent.
- [ ] The filename and notes do not contain a person, account, location or device identifier.

## Calibration quality

- [ ] PNG is an original-resolution screenshot captured through the local workflow.
- [ ] Phone orientation matches the capture plan.
- [ ] Display size, font scale, game language and navigation mode match the locked session.
- [ ] Expected state is correct.
- [ ] The screenshot is visually different enough to add useful coverage.
- [ ] It is not recorded as a pixel-identical duplicate.
- [ ] Dynamic artwork or values will not be selected as future anchors.
- [ ] Any overlay, popup or error is labelled as its actual expected state.

## Capture integrity

- [ ] `calibration-capture-status` reports no missing or changed capture file.
- [ ] The capture id matches the screenshot being reviewed.
- [ ] The SHA-256 has not changed since capture.
- [ ] The device serial and exact geometry lock remain valid.

## Promotion

- [ ] Run `approve-local-calibration-capture.ps1` with the correct capture id.
- [ ] Enter the exact confirmation `APPROVE` only after completing this checklist.
- [ ] Verify that the promoted fixture appears in `fixture-manifest.local.json`.
- [ ] Verify all five safety fields are true.
- [ ] Verify reviewer and timestamp are recorded.
- [ ] Run `index-local-calibration.ps1` as an additional consistency check.
- [ ] Run validation and inspect the fixture result.

## Public sharing

Local calibration approval is not public-sharing approval. Real screenshots must remain outside Git while the repository is public unless a separate explicit review approves a minimal redacted crop or synthetic replacement.
