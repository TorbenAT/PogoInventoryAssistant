## Streaming Vision Phase 4 - 2026-07-28

- Clean integration worktree baseline: 28 solution projects; `dotnet restore`
  passed; `dotnet build PogoInventoryAssistant.sln --configuration Release
  --no-restore` passed with 0 warnings and 0 errors on .NET SDK 10.0.302
  (8.0.423 installed).
- Clean `scripts/test.ps1`: 249/249 passed, with one local-data evidence
  sanity check skipped because `local-data/` is absent in the clean clone.
  The earlier 249/250 result is only from the dirty workspace's pre-existing
  identity test change and is not caused by streaming integration.
- Phase 2 self-test: 3/3 PASS. Phase 3 self-test: 15/15 PASS.
- Phase 4 self-test: 7/7 PASS.
- Preflight: ADB PASS; authorized serial `192.168.1.185:5555`, model
  `ONEPLUS_A6013`, physical display `1080x2340` portrait, resolved stream
  `886x1920` at max-size 1920, output directory PASS, InputCommandsSent=0.
- Historical Phase 4 preflight was blocked before the local toolchain was
  installed; the completed Phase 5 preflight and observe acceptance are
  recorded in the Phase 5 section below.
- ROI profile candidate: `profiles/pokemon-go-oneplus6t-portrait.json`.
  It is normalized and unverified against stream evidence.
- Real-phone gate calibration, frame timing, bounded PNG evidence and
  shutdown lease measurement: NOT RUN because the decoder/server toolchain is
  unavailable. No transition PASS is claimed.

## Streaming Vision Phases 1-3 integration - 2026-07-28

- ZIP SHA-256 verified against handover: `9a409801e84b74f335bae37417884da1e940059e18057efa2658d6596943bce3`.
- Phase 2 package-free self-test: PASS, 3/3.
- Phase 3 package-free self-test: PASS, 15/15; `InputCommandsSent = 0`.
- An initial dirty-workspace baseline restore was blocked before compilation
  by the global NuGet.Config; the clean-worktree baseline above supersedes it.
- Real-phone scrcpy/ADB acceptance: NOT RUN; no device evidence was available.
- Repository scripts: build/demo/fake/device-free/iPhone/synthetic scripts
  passed. `test.ps1` reached 249/250; its single failure is the pre-existing
  dirty-worktree test `Tag mutation binding ignores tags and rejects identity
  changes` (changed CP header rejected), outside the streaming files.
- `prepare-android-phone.ps1`: NOT RUN successfully; ADB was unavailable.
- Streaming wrapper build and self-test scripts: PASS.
- CLI validation: both observe commands reject missing `--device` with
  explicit validation; no device stream was started.

## Task-K 15-case manual ground-truth analysis - 2026-07-26

- Manual labels: 30 run rows covering 15 ordinal pairs; all 30 are Verified
  with concrete screenshot sources. The remaining 85 observations are outside
  this focused review pack and remain Unverifiable.
- Evidence result: all 15 pairs are same-ordinal/same-Pokémon by the documented
  visible evidence; 0 true collisions and 0 false merges.
- Primary causes: 7 CP-not-extracted, 2 CP-incorrect, 3 IV-not-extracted and
  2 species-not-extracted.
- Counterfactual re-match: CP 44/50 (88%), IV 38/50 (76%), species 35/50
  (70%), CP+IV 48/50 (96%), all documented extractor errors 50/50 (100%).
- Recommended next scanner change: guarded CP extraction; not implemented in
  this checkpoint. Unknown remains the required outcome for unreadable CP.
- Phone: not accessed.

## Ground-truth labeling measurement tooling - 2026-07-26

- Offline CLI build: PASS.
- Self-tests: PASS, 248/248, including CSV parsing, field metrics and review
  cause classification.
- `prepare-ground-truth`: PASS; generated 100 rows from the two Task-K
  `captured-observations.json` inputs, with separate scanner columns and
  evidence paths.
- `analyze-field-completeness`: PASS; analyzed both Task-K SQLite databases,
  produced 15 individual review cases and overall/per-run reports.
- Current labels: 0 Verified, 100 Unverifiable, 0 NotApplicable. All six
  fields are therefore Unverifiable=100 with no accuracy/completeness claim.
- Review diagnosis: 8 CP-not-extracted, 3 IV-not-extracted, 2
  species-not-extracted and 2 CP-conflict cases. These are scanner-output
  diagnoses, not manually verified truth.
- Counterfactual gain: not calculable until shared ground-truth entity IDs and
  field values are manually verified.
- Phone: not accessed; no scanner logic changed.

## Phase 2 field-completeness baseline - 2026-07-26

- Tooling: applied and pushed in `cba0bf8`.
- Input: two fresh Task-K pilot databases, 100 observations total.
- Rows: 500 critical fields (Species, CP, AttackIV, DefenseIV, HpIV).
- Result: 0 Correct, 0 Incorrect, 500 Unknown; effective correct coverage 0%.
- Safety: PASS; no incorrect automatic value was accepted. The tool emitted
  its expected under-coverage warning because all values are Unknown.
- Ground truth: unavailable. This is a conservative baseline, not field
  completeness acceptance or an OCR correctness claim.
- Phase 3 and phase 4 were not applied.

## Appraisal carousel real-phone acceptance - 2026-07-21

- Acceptance: PASS, 20/20 items.
- Cursor evidence: 19 appraisal swipes, zero Details swipes, 20 unique
  fingerprints, one appraisal open and one final exit.
- SQLite integrity: `ok`; reports generated after database reopen.
- Safety: zero tag mutations and zero destructive actions.
- Final detected state: `GameplayMap`.

## Canonical close Android scale repair checkpoint - 2026-07-21

- The first real diagnostic visually contained the canonical Details X but
  stopped before input because the single-radius locator found no compatible
  target.
- The locator now evaluates bounded scale-normalized radii and retains shell,
  crossing-stroke, position, dimensions and contrast checks.
- Build: PASS.
- Self-tests: PASS, 162/162.
- Direct diagnostic rerun and real acceptance: pending; no acceptance claim.

## Canonical close unwind checkpoint - 2026-07-21

- Added screenshot-derived canonical lower-centre close locator and named
  `close-canonical-screen` operation.
- Added bounded five-input `UnwindToGameplayMapAsync` orchestration with fresh
  target revalidation, one-tap transitions, stable changed-state postconditions
  and no Android Back fallback.
- Added synthetic positive/negative locator coverage, including clear-X and
  arbitrary-cross rejection.
- Build: PASS.
- Self-tests: PASS, 162/162.
- Real Android canonical unwind/value proof: pending; no acceptance claim.

## Cleanup startup stability repair checkpoint - 2026-07-21

- Direct autonomous run `autonomous-pidgey-age0-365` stopped before input when
  strict Details recovery evidence did not reach consensus; recovery input
  count was 0 and no SQLite proof was created.
- A read-only post-failure check detected `PokemonDetails` with full topology.
- The focused repair saves start-state recovery frames and accepts three
  independent same-state ordinary Details frames; strict appraisal ROI
  consensus is unchanged.
- Build: PASS.
- Self-tests: PASS, 162/162.
- The repaired phone run was intentionally not repeated in the same iteration.
- Real Android value proof: pending; no acceptance claim.

## Autonomous cleanup start recovery checkpoint - 2026-07-21

- `device-run-cleanup-proof` now calls the shared bounded canonical-close unwind.
- Supported reversible start states are normalized through named operations with stable pre/post frames, fresh input authorization, loop detection and a six-input budget.
- AppraisalIntro continuation uses the locator target once and requires stable AppraisalBars; Unknown/unsafe states send zero input.
- Baseline persistence precedes appraisal; appraisal and semantic-review enrichment are transactional.
- Build: PASS.
- Self-tests: PASS, 162/162.
- `git diff --check`: PASS.
- Real Android value proof: pending code checkpoint; no acceptance claim.

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

## Long self-recovering database acceptance — 2026-07-21

- Baseline: HEAD/origin/main `3c78c225718b269647e437b39d512d69f0b5c592`.
- Build PASS; self-tests PASS 162/162; diff-check PASS.
- Query `age0-1825`, requested 20, captured 4 Complete items, then safely
  stopped at `CursorProgression:Unknown`. No blind retry or source repair was
  performed.
- SQLite integrity: `ok`; ScanRuns 1, Observations 4, PokemonRecords 4,
  InventoryEvents 12. Database was reopened before analysis and reports were
  generated from reloaded rows.
- Final screenshot was visually PokemonDetails/Fletchling while the detector
  returned Unknown. No further phone input was sent. This is not a long-run
  acceptance because it is below the required 10 captured items.

## Canonical close and cleanup value-proof checkpoint — 2026-07-21

- Code commits: `2c34f70` and `5a5ffc1`, both pushed to `main`.
- Build: PASS. Self-tests: PASS, 162/162. `git diff --check`: PASS.
- Real phone: the direct unwind and three program-created Inventory, Details
  and Appraisal cycles returned to GameplayMap. Appraisal used the existing
  guarded named exit before canonical-close layers; no raw ADB input, blind
  retry, destructive confirmation, tag mutation or affirmative input occurred.
- Value proof: `device-run-cleanup-proof` ran with query
  `pidgey&age0-365`, item limit 6 and continue-on-partial. The query showed no
  results, so first-card Details verification was not attempted. The reopened
  SQLite database reported integrity `ok`, with 0 observations and 0 Pokémon
  records. No real value-proof acceptance is claimed.
- Evidence root: `local-data/validation/cleanup-value-proof`.

## Cleanup proof pipeline implementation on 2026-07-21

- wrong-screen CI packaging fix: PASS; synthetic `PixelImage`/`PngEncoder`
  fixtures, no `local-data` dependency
- clean-checkout-equivalent self-tests with `local-data` absent: PASS, 159/159
- cleanup-proof focused tests: PASS; full offline self-tests 160/160
- full build: PASS
- `device-run-cleanup-proof`: implemented, bounded and read-only
- real-phone SQLite/report proof: pending; no acceptance claim

## Deterministic navigation safety acceptance tooling on 2026-07-21

- CLI command: implemented as `device-validate-navigation-safety`, cycles
  bounded to 1 through 3, with GameplayMap precondition and guarded final map.
- Named host integration: PASS; no locator, detector, recovery or raw ADB
  logic is duplicated in the CLI.
- Action trace contract: PASS; input is recorded only after transport return,
  five post-input frames are bounded and ordered before POSTCONDITION.
- offline self-tests: PASS, 159/159
- full build: PASS; full self-tests: PASS, 159/159
- real-phone acceptance: PASS; three bounded cycles on the authorized OnePlus
  A6013, each with five inputs, two Back actions, 25 postframes, zero unsafe
  inputs and verified GameplayMap final state
- final evidence: `local-data/validation/deterministic-navigation-safety`,
  including `action-trace.jsonl`, `cycle-summary.json`,
  `phone-summary.md` and the complete-repository ZIP

## Wrong-screen action authorization repair on 2026-07-21

- incident evidence inspection: PASS; the recorded `(300,1837)` tap was on a
  Fletchling Details screen and the later Power Up confirmation was not
  treated as pre-existing
- normal Details versus Power Up modal detector regression: PASS
- MainMenu typed precondition conflict/stale/fallback/unsafe cases: PASS
- package-free self-tests: PASS, 158/158
- full phone safety check: NOT RUN; manual CANCEL and safe GameplayMap or
  unfiltered Inventory state are required before phone input
- real-phone acceptance: NOT CLAIMED
- destructive action confirmation after repair: 0
- tag mutations in this checkpoint: 0

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

## Android sequence host real-phone acceptance on 2026-07-21

- ADB preflight passed for `192.168.1.185:5555` using the bundled platform
  tools; one bounded reconnect was sufficient.
- Task 7 passed on a clean `age0-7`, limit-3, no-tag run: three records,
  two observed transitions, three independent post frames per transition,
  zero appraisal Back actions and zero tag mutations.
- Task 8 passed with controlled stop after 2 and resume: ordinal 2 was used
  only for overlap comparison, not recorded again, and one new progression
  reached ordinal 3.
- Task 9 is partial/fail-closed. After three records and two observed
  transitions, one swipe at the fourth position had `NO_EFFECT`; the runtime
  entered `TerminalUnknown` and sent no second swipe.
- A guarded close-inventory recovery left the phone at verified
  `GameplayMap`. No destructive operation or tag mutation occurred.
- Final build and self-tests: PASS, 157/157. The evidence root is
  `local-data/validation/android-sequence-host`; the ZIP is generated locally
  and is intentionally not committed.

## Cursor changed-identity repair on 2026-07-21

- Added a bounded post-swipe identity fallback. One swipe with no captured
  transient now captures three independent Details frames before classification.
- Different stable fingerprint: `SUCCESS_CHANGED_IDENTITY`; identical stable
  fingerprint: explicit `NoEffectOrEndOfFilter`; no blind second swipe.
- Guarded recovery and start-state handling use allow-listed Details topology
  only when the strict detector returns Unknown, while Back remains guarded by
  an Inventory postcondition.
- Build: PASS; self-tests: PASS, 157/157; diff check: PASS.
- The first real `age0-1825`, limit-10 attempt reached four complete records.
  Later bounded attempts encountered unstable start/open-inventory state and a
  visible `POWER UP: FLETCHLING` confirmation screen. No further input was sent
  and Task G ten-item acceptance is blocked, not passed.
# Validation report

The persistent Appraisal carousel implementation builds successfully and the
complete self-test suite passes 163/163. Real-phone acceptance has not yet
been run for this checkpoint.
## Task-K fresh 2x50 pilot and phase-1 measurement - 2026-07-26

- Run 1: 50/50, 39 Complete, 11 Partial, 0 Unresolved, SQLite integrity ok.
- Run 2: 50/50, 40 Complete, 10 Partial, 0 Unresolved, SQLite integrity ok.
- Re-identification: 35 matched, 15 no-match candidates, 0 ambiguous, 0
  false-merge guard blocks, 70% re-match rate.
- Phase-1 CSV tool: PASS with 35 ConfirmedMatch and 15 PossibleMatch review;
  100% accounted/reviewed. This is a pilot result, not formal ID-006 approval,
  because ground-truth labels are absent and the measured re-match rate is
  below the intended quality target.
- Final phone state: GameplayMap, detector confidence 1.0.
- No-match diagnosis: same-ordinal species in 15/15 cases; missing CP 9,
  missing IV 3, Unknown species/CP 2, CP mismatch 2.

## Task-K sand-background detector repair - 2026-07-26

- Build: PASS.
- Self-tests: PASS, 245/245.
- Corpus verification: 0/343 colliders passed the canonical-close X locator.
- Evidence gate: 13/13 frames classified `PokemonDetails` (8 sand-exit and 5
  original frames), confidence 1.0.
- Real-phone 2x50 pilot: complete; see the acceptance section above.
## Streaming Vision Phase 5 - 2026-07-28

- Full Release solution build: PASS, 28 projects, 0 warnings, 0 errors.
- Repository self-test: PASS, 249/249.
- Phase 4 self-test: PASS, 7/7; input commands sent: 0.
- Local toolchain preflight: PASS for ADB, FFmpeg, scrcpy server, authorized
  `ONEPLUS_A6013`, and automatic `886x1920` portrait dimensions.
- Zero-frame root cause: FFmpeg `-fflags nobuffer` suppressed rawvideo output
  in the pipe-fed H.264 mode; removing it restored decoded BGRA frames.
- Real-phone observe acceptance: PASS for 30s/30s/60s; decoded/published
  frames were 837/837, 833/832 and 1691/1691; all shutdowns clean.
- Phase 5 self-test: PASS, 8/8.
- Three gate observations completed read-only with 68-80 frames and zero
  outstanding leases, but timed out conservatively on required Header
  motion/sharpness. Regional gate calibration is NOT ACCEPTED.
- An isolated read-only raw-protocol diagnostic read H.264 bytes from the same
  phone/server combination; this is diagnostic evidence only and is not
  application-level stream acceptance.
- Input commands, navigation and state-changing phone operations: 0.
- Local toolchain binaries remain ignored under `tools/local/`.
# Validation report — Streaming Vision Phase 6A

Date: 2026-07-28

## Local runtime completion update

- Python 3.13.14 local embeddable runtime: PASS.
- GPU: NVIDIA GeForce RTX 4060 Ti, driver 591.86, 8188 MiB.
- PyTorch 2.11.0+cu128: PASS; CUDA 12.8 and `cuda:0` tensor smoke test.
- Embedding-equivalent ResNet18 benchmark: PASS; P50/P95/P99 3.570/3.883/
  4.016 ms, peak VRAM 112,626,688 bytes.
- EasyOCR 1.7.2 fixed-crop benchmark: PASS; P50/P95/P99 69.848/73.289/
  73.289 ms, peak VRAM 344,411,648 bytes.
- Synthetic truth manifest: PASS and versioned at
  `data/phase6a-truth-manifest.synthetic.json`.
- Real screenshot field accuracy: NOT MEASURABLE; real crop truth is
  Unverifiable. False Known and False Complete are null for that crop.

## Executed results

- Solution restore/build: PASS, 33 projects, 0 warnings, 0 errors.
- Repository self-test: PASS, 249/249; one expected local-data sanity check skipped.
- Phase 2 self-test: PASS, 3/3; Phase 3: PASS, 15/15; Phase 4: PASS, 7/7.
- Phase 6A self-test: PASS, 8/8.
- Required build/demo/fake/offline scripts: PASS, 15/15 returned exit code 0.
- Phase 6A benchmark: PASS, offline synthetic case; false Complete 0; input commands 0.

## Limitations

The committed iPhone fixtures are pretest evidence only. Phase 6A local runtime
and GPU/OCR benchmarks are recorded in the Phase 6A section; real screenshot
field accuracy remains NOT MEASURABLE because the crop truth is Unverifiable.
Read-only Android preparation reported ADB unavailable and sent no input.

Local manifests are outside Git at `C:\Data\PokemonGo-Tools\manifests`.
