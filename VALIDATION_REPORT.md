## Version 0.14.3 compile correction

The five compiler errors were caused by one malformed C# `or` pattern.

Static checks confirm:

- the filter contains exactly one `exception is`
- all five recoverable exception types remain present
- the malformed phrase `or exception is` is absent
- the unsupported-PNG regression test remains declared
- the expected self-test count remains 114
- no phone action changed

GitHub Actions remains the authoritative compiler and execution environment.

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

0.14.3

## Accepted prior checkpoint

Torben reported version 0.13.1 fully green.

## Static validation

- all 15 project files parse
- every project reference resolves
- every project is present in the solution
- the CLI contains `appraisal-pretest` and `phone-prepare`
- the default appraisal profile parses
- the workflow runs the real iPhone appraisal pretest
- 114 self-tests are declared
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

## Runtime validation on 2026-07-19

Executed successfully:

- `dotnet build .\PogoInventoryAssistant.sln --configuration Release`
- `powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-demo.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-fake-device.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-fake-core-profile-bootstrap.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-fake-inventory-scan.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-fake-calcy-probe.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-fake-calcy-live-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\parse-synthetic-calcy-output.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\detect-synthetic-screen.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\build-synthetic-calibration-profile.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\validate-synthetic-calibration.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-iphone-image-pretest.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-iphone-region-discovery.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-appraisal-pretest.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\prepare-android-phone.ps1 -Adb .\tools\platform-tools\adb.exe`

Key results:

- build succeeded with 0 warnings and 0 errors
- self-test suite passed: 138/138
- fake device snapshot completed
- fake core profile bootstrap accepted
- fake inventory scan captured 3 items
- fake Calcy probe reported `CandidateEvidenceFound`
- fake Calcy live check parsed a `Complete` observation
- synthetic Calcy parsing produced `Pikachu`, CP 501, IV `15/14/13`
- synthetic screen detection classified `InventoryList`
- synthetic calibration profile built and validated successfully
- iPhone image pretest decoded 23/24 screenshots and retained the unsupported PNG diagnostic
- iPhone region discovery accepted 23 decoded images, 4 visual clusters and a 12x24 grid
- appraisal pretest found 10 candidates, 0 Complete observations, and a 90.0 percent dominant cluster
- Android phone preparation succeeded with repo-local ADB and a real device at `192.168.1.185:5555`

Limitations observed during validation:

- `run-appraisal-pretest` required the region-discovery report, so `run-iphone-region-discovery` had to be run first.
- `prepare-android-phone` failed with the default `adb` path because ADB was not on PATH in this environment.
- The real phone preparation run confirmed `Verified IV extraction ready: False`, so verified IV extraction is still not available.
