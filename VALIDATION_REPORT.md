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

## Task 5 sequence orchestration checkpoint on 2026-07-20

- build: PASS for Automation and SelfTest
- self-tests: PASS, 156/156
- bounded item limit, per-item atomic checkpoint and resume: PASS
- Partial preservation, bounded continuation after verified Inventory restore,
  and Unknown stop without further input: PASS
- tags disabled by default and AI-Delete auto-apply rejected: PASS
- real-phone sequence acceptance: NOT RUN; no named-operation host was bound
- production raw ADB actions: 0

## Task A identity consensus contract on 2026-07-21

- minimum Complete consensus: PASS; one/two frames are Partial and three
  compatible frames are Complete
- two usable plus one Unavailable: PASS; result remains Partial
- order-independent bytewise-median canonical fingerprint: PASS
- separate frame evidence hashes and mutable tag observations: PASS
- CLI exit codes: Complete 0, Partial 2, Unavailable 3
- package-free self-tests: PASS, 156/156

## Task 4 dynamic identity tuning checkpoint on 2026-07-21

- implementation build: PASS for Vision, Automation, CLI and SelfTest
- package-free self-tests: PASS, 155/155
- CLI evidence reports: PASS for three five-frame real Details groups; the
  fourth local group was correctly rejected as Inventory/Unavailable
- screenshot evidence hashes are retained separately from stable fingerprints
- reversible named AI-Indexed and AI-Review add/remove: completed; wrong
  selections 0; final phone state unfiltered Inventory with no test tags
- real Task 4 tag-layout acceptance: PARTIAL and explicitly not claimed green;
  the captured Task 3 zero/one/two-tag states now count 0/1/2, and the
  zero-tag versus tagged stable fingerprint similarity is 0.9815 against the
  0.965 threshold, but this is not the 20-Pokémon provider gate
- one local five-frame evidence group was an Inventory screen and produced
  `Unavailable` without guessing identity
- required fake, synthetic, iPhone-pretest, appraisal-pretest and parser
  scripts: PASS; `prepare-android-phone.ps1` was invoked read-only but the
  environment reported `[AdbNotFound]` because no `adb.exe` is available
- production raw ADB actions: 0

## Version

0.14.3

## Verified tag selection by name validation on 2026-07-20

- build: PASS, zero warnings and zero errors
- self-tests: PASS, 148/148
- real test Pokemon: Ekans CP616
- match: normalized three-scale visual template after geometric row discovery
- accepted Trade confidence: 0.806; second-best margin: 0.228
- fixed tag row index/coordinate used: NO
- add cycles: 2/2; remove cycles: 2/2; wrong tag selections: 0
- idempotence: verified removal requested while already unselected, zero row
  mutation actions
- final `#Trade`: 0 visible results
- final `!#Trade`: Ekans CP616 visible
- final state: unfiltered Inventory; Trade unselected on Ekans CP616
- initial selector-gate and first pill-detector failures were preserved locally,
  repaired, and rerun; neither caused an unintended mutation
- limitation: templates are device-local visual evidence, not portable OCR
- real screenshots, serial, profile and audits remain in ignored `local-data`

### Additional AI-tag and Details-layout acceptance

- self-tests after pill-count regression: PASS, 149/149
- AI-Indexed: add/remove PASS, confidence 0.910
- AI-Review: add/remove PASS, confidence 0.937
- AI-Keep: add/remove PASS, confidence 0.931
- AI-Delete tag name: add/remove PASS, confidence 0.923; destructive actions 0
- wrong tag selections: 0
- Details layouts: zero tags PASS; one tag PASS; simultaneous AI-Indexed and
  AI-Review PASS; removal 2 -> 1 -> 0 PASS
- full screenshot hashes differed across zero/one/two layouts as expected
- identity implication recorded for Task 4: tag section must be dynamically
  detected and lower stable ROIs anchor-aligned; tags are never identity
- final phone state: unfiltered Inventory; Ekans CP616 has no tested tag

## Verified Inventory Search validation on 2026-07-20

- build: PASS, 18 projects, zero warnings and zero errors
- self-tests: PASS, 146/146
- central encoding: `#Trade -> %s#Trade`, `!#Trade -> !\#Trade`
- acceptance rounds: 2
- accepted queries: 10/10
- `age0-7`: PASS twice, visible count 7
- `age0-365`: PASS twice, visible count 7
- `age0-1825`: PASS twice, visible count 303
- `#Trade`: PASS twice, visible count 0 in the current untagged state
- `!#Trade`: PASS twice, populated result including Ekans CP616
- final clear: PASS twice; unfiltered Inventory visually restored
- initial failed attempt: no input sent because the placeholder Search text was
  conservatively mistaken for an existing query; analyzer repaired and rerun
- production raw ADB fragments outside Device: 0
- evidence: ignored
  `local-data/validation/sol-high-android-implementation/task-02-search`

## Guarded appraisal recovery validation on 2026-07-20

Offline result:

- `scripts/build.ps1`: PASS, 18 projects, zero warnings and zero errors
- `scripts/test.ps1`: PASS, 144/144
- demo, fake-device, fake-bootstrap, fake-inventory, fake-Calcy probe and fake
  Calcy live-check scripts: PASS
- iPhone pretest: PASS, 23/24 decoded, four clusters
- appraisal pretest: PASS, 10 candidates, 90.0% dominant cluster, zero Complete
- synthetic Calcy parse, screen detection, calibration build and calibration
  validation: PASS
- `prepare-android-phone.ps1`: PASS when invoked with the required local ADB
  path and serial; appraisal Candidate, calibration ready, Complete disabled
- `git diff --check`: PASS

Real recovery result:

- three complete appraisal cycles: PASS, 3/3
- AppraisalIntro -> AppraisalBars: one normalized `ExitAppraisal` tap at
  `(0.1001, 0.5002)` with stable bars required afterward
- AppraisalBars -> PokemonDetails: one normalized `ExitAppraisal` tap at the
  same documented target with stable Details required afterward
- PokemonDetails -> Inventory: one Android Back after Details verification
- Back actions on AppraisalBars: 0
- Unknown states: 0
- wrong post-states: 0
- blind repeated taps: 0

The first cycle retained the earlier failed Back evidence and then completed
through the repaired action. Cycles two and three used the integrated command
without repair. Evidence remains under ignored
`local-data/validation/sol-high-android-implementation/task-01-appraisal-recovery`.

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

## Real-phone follow-up on 2026-07-19

Executed successfully:

- `powershell -ExecutionPolicy Bypass -File .\scripts\start-night-evidence-scan.ps1 -AdbPath .\tools\platform-tools\adb.exe -MaximumItems 3 -MaximumRuntimeHours 0.5 -OutputDirectory .\local-data\night-scans\stability-3`
- `powershell -ExecutionPolicy Bypass -File .\scripts\run-local-calcy-probe.ps1 -AdbPath .\tools\platform-tools\adb.exe -Serial 192.168.1.185:5555 -OutputDirectory .\local-data\calcy-probe-real-2`
- `dotnet run --project .\src\PogoInventory.Cli --configuration Release -- calcy-live-check --adb .\tools\platform-tools\adb.exe --serial 192.168.1.185:5555 --profile .\local-data\automation-profile.local.json --screen-profile .\local-data\screen-profile.local.json --settle-ms 2000 --out .\local-data\calcy-live-check-real-2`

Key results:

- the real phone scan completed 3/3 items with 2/2 verified swipes
- calibration stability was `True` with 0.00 percent scale spread and 0.00 percent normalized translation spread
- all three calibration cases remained Candidate-only with zero Complete observations
- the three selected IV triplets were `10/12/15`, `11/12/11` and `15/6/9`
- the real Calcy probe reported `CandidateEvidenceFound`
- the installed Calcy package was `tesmath.calcy` version `3.44`
- the real Calcy live-check captured one navigation item and then completed the read-only probe path
- no parser profile was supplied for the live-check, so no parsed observation was written

Output locations:

- `local-data/night-scans/stability-3/calibration/phone-calibration-stability.md`
- `local-data/night-scans/stability-3/calibration/phone-calibration-stability.json`
- `local-data/calcy-probe-real-2/calcy-probe-report.md`
- `local-data/calcy-live-check-real-2`

## Base validation results

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
- the real phone scan and real Calcy checks remained read-only and added no transfer actions

Limitations observed during validation:

- `run-appraisal-pretest` required the region-discovery report, so `run-iphone-region-discovery` had to be run first.
- `prepare-android-phone` failed with the default `adb` path because ADB was not on PATH in this environment.
- The real phone preparation run confirmed `Verified IV extraction ready: False`, so verified IV extraction is still not available.
- the real Calcy live-check did not parse a local observation because no parser profile was supplied yet
## 2026-07-20 game-state iteration

- Release build: passed.
- Self-tests: passed.
- Real read-only detection: `PokemonDetails`, confidence `1.000`, evidence
  `DetailsMenuDetected`.
- Real recovery: one Back action attempted; stable post-state remained
  `PokemonDetails`, so recovery stopped fail-closed and did not claim PASS.
- The required 3/3 Details-to-Inventory acceptance was not completed.
## 2026-07-20 gameplay-map detector iteration

- Build: passed.
- Self-tests: passed.
- Saved real gameplay-map frame: `GameplayMap`, confidence `1.000`.
- Saved real inventory frame: no longer classified as `PokemonDetails`.
- Live read-only captures: one `PokemonMenu`, followed by `GameplayMap`, then
  `PokemonMenu`; acceptance `3/3` was not claimed.
- Phone actions: zero.
## Android verified sequence host checkpoint on 2026-07-21

- `AndroidVerifiedInventoryNamedOperations` binds the sequence to the named
  Android transport, detector, guarded search/recovery and visual locators.
- Offline checks cover one first-card open, N-1 cursor advances, no normal
  return to Inventory, Partial Details continuation, Unknown/no-effect stops,
  checkpoint fields and resume overlap fail-closed behavior.
- `device-run-index-sequence` is bounded, read-only by default; AI-Indexed and
  classification tag mutation are separate options and are not enabled.
- Build: PASS; self-tests: 156/156; real-phone acceptance: NOT YET RUN.
- The real Android ADB path was found at
  `tools/platform-tools/adb.exe`; phone execution remains a separate evidence
  checkpoint and no approval is claimed here.
- A bounded real-phone attempt on 2026-07-21 found no authorized device. The
  expected Wi-Fi serial reconnect returned Windows socket error 10013, so the
  production host sent no phone input. Tasks E/F are blocked and Task G is
  ineligible; this is not real-phone acceptance.
## Android sequence runtime repair on 2026-07-21

- Appraisal host repair delegates Intro/Bars transitions and Inventory recovery
  to `GuardedInventoryRecovery`; no Back action is authorized on appraisal
  overlays.
- Cursor repair requires observed transition evidence, one swipe maximum and
  three independent post-swipe Details frames. Equal fingerprints remain
  separate instances when transition evidence exists.
- ControlledStopped checkpoints resume through overlap and Completed
  checkpoints are idempotent. Offline self-tests: 157/157.
- Real-phone acceptance is active in the current checkpoint; Tasks 7/8 passed
  and Task 9 reached three items before the item-4 Intro threshold repair.
- During the controlled resume acceptance, evidence numbering was observed to
  restart in an existing output. The host now scans existing evidence ordinals
  and appends safely; this was the first additional runtime repair required by
  the acceptance run.
- The ten-item run reached three complete items, then item 4 presented a clear
  AppraisalIntro at confidence 0.594 and stopped below the former 0.60 gate.
  One focused repair lowers the bounded gate to 0.58; the captured failure
  screen and state JSON are under `failures/ten-item-item4`.
