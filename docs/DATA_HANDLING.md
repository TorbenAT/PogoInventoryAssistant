# Data handling

The repository may remain public while it contains only source code and synthetic data.

Before real inventory collection begins, make the repository private or keep every real file outside the repository.

## Never commit while public

- phone serial numbers
- real Pokémon GO screenshots
- incoming capture-session files
- account names or trainer identifiers
- full inventory exports
- capture manifests from a real phone
- local screen profiles generated from real screenshots
- SQLite inventory databases
- log files containing personal data
- authentication tokens or ADB keys

## Default private workspace

Use:

```text
local-data\screen-calibration\
```

The entire `local-data` tree is ignored by Git.

The calibration workspace contains:

```text
incoming\                    unreviewed screenshots
fixtures\                    reviewed local fixtures
capture-plan.local.json      required screen coverage
capture-session.local.json   device serial, geometry, hashes and capture ids
fixture-manifest.local.json  approved fixture hashes and privacy review
anchor-plan.local.json       local anchor design
profiles\                    generated local detector profile
reports\                     capture and acceptance reports
```

## Incoming versus fixture

A screenshot captured through ADB is written to `incoming/<ScreenState>/`.

Incoming means:

- the expected state has not necessarily been confirmed
- privacy has not been approved
- the screenshot cannot be used to build a detector profile
- it must not be committed

Promotion into `fixtures/<ScreenState>/` requires explicit local review and hash verification.

## Capture-session personal data

The session records the Android device serial and model to prevent accidental mixing of phones. That file is private even when screenshots appear harmless.

Do not paste the complete capture session into a public issue, commit or chat. Share only aggregate capture status unless a specific field is required for debugging.

## Screenshot review

Before promotion, verify:

- trainer and account identity are absent or acceptably protected for local use
- map or location information is absent
- notification banners and message previews are absent
- email addresses, contacts and device identifiers are absent from the image
- the expected screen state is correct
- the image adds useful variation rather than repeating identical pixels

The promotion confirmation approves the image only for local calibration. It does not approve public distribution.

## Test fixtures

Only synthetic, cropped or manually redacted fixtures may be committed to a public repository. Prefer a synthetic replacement or the smallest possible UI crop.

## Hash-locked trust

The capture session and fixture manifest record SHA-256 values.

- a changed incoming capture is rejected
- a changed approved fixture loses trust
- a duplicate screenshot does not count toward variation coverage
- a promoted fixture is linked to its original capture id

Approval applies only to the exact bytes that were reviewed.

## Capture-session integrity

The private session stores a SHA-256 fingerprint of the exact capture plan. Changing requirements mid-session invalidates the session instead of silently mixing two collection plans. Promotion never overwrites an untracked fixture file.
