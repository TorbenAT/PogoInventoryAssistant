## Version 0.14.2 decoder correction

The failing file is `data/iphone-images/IMG_7699.png`.

Recorded evidence:

- SHA-256: `ef40abb395c0e17f87706731322ea492d7071b2bd9ee26c26ab97c7242551738`
- error type: `ScreenVisionException`
- error detail: `Only 8-bit PNG screenshots are supported.`

Static checks confirm:

- `AppraisalPretestRunner` imports `PogoInventory.Vision.Errors`
- its decoder exception filter includes `ScreenVisionException`
- the report retains rejected image diagnostics
- a synthetic unsupported-PNG regression test is declared
- the guarded removal script verifies SHA-256 before deletion
- the declared self-test count is 113

GitHub Actions remains the authoritative compiler and execution environment.

## Version 0.14.1 compile correction

The reported CS0173 failure was isolated to local type inference in
`AppraisalAnalyzer.MeasureBar`.

Static checks confirm:

- `estimatedIv` is explicitly declared as `int?`
- the true branch returns an integer clamped to 0 through 15
- the false branch remains null
- `AppraisalBarMeasurement.EstimatedIv` is also `int?`
- the declared self-test count remains 113
- no project reference or phone action changed

The preparation environment does not contain the .NET SDK. GitHub Actions
remains the authoritative compiler and test runner.

# Validation report

## Version

0.14.2

## Accepted prior checkpoint

Torben reported version 0.13.1 fully green.

## Static validation

- all 15 project files parse
- every project reference resolves
- every project is present in the solution
- the CLI contains `appraisal-pretest` and `phone-prepare`
- the default appraisal profile parses
- the workflow runs the real iPhone appraisal pretest
- 113 self-tests are declared
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
