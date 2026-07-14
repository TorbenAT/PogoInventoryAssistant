# Release 0.13.0

## Purpose

Produce a compact review artifact that can be uploaded and visually
inspected before choosing the first semantic extraction field.

## New outputs

`out/iphone-semantic-evidence/` contains:

- `semantic-evidence.json`
- `semantic-evidence.md`
- `semantic-evidence-cases.csv`
- `semantic-truth-template.json`
- `semantic-review-pack.zip`
- `atlas/`
- `crops/`

## Truth policy

All semantic truth fields start null. The release does not assert screen
state, species, CP or IV values.

## Readiness policy

The pack can be accepted for external review even when one visual cluster has
fewer than two cases. That condition is reported explicitly as
`needsMoreImages` with the exact cluster identifiers.

Automated extraction remains false in every 0.13.0 report.

## Safety

- no source screenshot is copied into the review ZIP
- no phone action is added
- no OCR or IV result is claimed
- long inventory scanning is not enabled from this evidence pack

Expected total: 103 self-tests.
