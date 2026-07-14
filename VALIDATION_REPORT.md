# Validation report

## Version

0.14.0

## Accepted prior checkpoint

Torben reported version 0.13.1 fully green.

## Static validation

- all 15 project files parse
- every project reference resolves
- every project is present in the solution
- the CLI contains `appraisal-pretest` and `phone-prepare`
- the default appraisal profile parses
- the workflow runs the real iPhone appraisal pretest
- 112 self-tests are declared
- no new phone input action exists
- the committed profile is unverified
- Complete output requires both verification metadata and an explicit verified-provider enable flag
- phone preparation uses `DeviceSnapshotService` and read-only screenshot capture
- ZIP integrity passes

The local preparation environment does not contain the .NET SDK. GitHub Actions
is authoritative for compilation, self-tests and the real screenshot pretest.

## Runtime expectations

The green workflow must show:

- at least five appraisal candidates
- candidates concentrated in one real visual cluster
- zero Complete observations
- generated overlays and bar crops
- generated `appraisal-review-pack.zip`

## Limitation

Candidate IV estimates are diagnostics. Version 0.14.0 does not yet provide a
verified IV observation provider.
