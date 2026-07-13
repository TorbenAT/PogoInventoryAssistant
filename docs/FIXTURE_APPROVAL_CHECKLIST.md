# Fixture approval checklist

Use this checklist for every real screenshot before setting `approvedForCalibration` to true.

## Privacy

- [ ] Trainer name, account name and avatar identity are absent or irreversibly redacted.
- [ ] Location names, map information and coordinates are absent or irreversibly redacted.
- [ ] Android notifications, contact names and message previews are absent or irreversibly redacted.
- [ ] Device serials, email addresses, authentication data and other personal information are absent.
- [ ] The filename does not contain a person, account, location or device identifier.

## Calibration quality

- [ ] PNG is an original-resolution screenshot, not resized by a messaging app.
- [ ] Phone orientation matches the anchor plan.
- [ ] Display size, font scale, game language and navigation mode match the approved phone configuration.
- [ ] Expected state folder is correct.
- [ ] The screenshot is visually different enough to add useful coverage.
- [ ] Dynamic artwork or values are not being selected as future anchors.
- [ ] Any overlay, popup or error is labelled as its actual expected state.

## Manifest approval

- [ ] Run `index-local-calibration.ps1` after the final edit.
- [ ] Verify the SHA-256 in the manifest corresponds to the final PNG.
- [ ] Set all four safety booleans to true.
- [ ] Set `approvedForCalibration` to true.
- [ ] Record reviewer and review timestamp.
- [ ] Run validation and inspect the fixture result.

## Public sharing

Real screenshots are not approved for public sharing merely because they are approved for local calibration. Public use requires a separate review and an explicit decision to add only a minimal redacted crop or synthetic replacement.
