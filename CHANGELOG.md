# Changelog

## Semantic integration: OCR wired into cleanup flow — 2026-07-21

- `CleanupProofRunner` now derives species through `SearchQueryClassifier`
  (exact single-species query => QueryDerived) or `PokemonHeaderAnalyzer`
  consensus over captured frames (=> Automated), never from a broad filter
  query; a defensive guard throws if a broad query would persist as species.
  The raw query remains only in `ScanRuns.SearchQuery`. CP and nickname come
  from header consensus. Per-frame IV measurements
  (`CleanupProofAppraisalCapture.Frames`) are accepted on >=2-frame exact
  agreement, independent of the Calcy `Verified` gate, and
  `ObservationStatus` upgrades to Complete only when species, CP and all
  three IVs are known.
- New offline command `analyze-cleanup-evidence` reprocesses an existing
  cleanup database into a new copy (original untouched) with corrected
  species/CP/IV/semantic keys, regenerated reports and
  `species-cp-coverage.json`.
- `device-run-cleanup-proof` gained `--policy` and `--species-reference`
  options; comparative-suggestion logic extracted to
  `CleanupProofComparativeAnalyzer` shared by runner and reprocessor.
- Offline self-tests pass 201/201. No phone input in this increment.

## Semantic foundation: header OCR, reference data, semantic identity — 2026-07-21

- Added `PogoInventory.HeaderText` (OCR abstraction: `PokemonHeaderAnalyzer`
  with species/CP/nickname extraction, multi-frame consensus, UI-label
  blacklist, `SearchQueryClassifier` separating exact-species queries from
  broad filters) and `PogoInventory.HeaderOcr` (Windows.Media.Ocr recognizer,
  `net8.0-windows10.0.19041.0`). New CLI command `ocr-header-spike` analyzes a
  directory of PNG frames offline and reports species/CP hit rates; it must be
  run on the machine holding the real carousel evidence. See
  `docs/HEADER_OCR.md`. Acceptance target: >=19/20 species and CP on the
  20-item evidence.
- Added versioned reference data `data/reference/species-reference.json`
  (1025 species, gen 1-9, Legendary/Mythical/UltraBeast classification from
  pogoapi.net) with `SpeciesReferenceLoader`, plus `RulePolicyLoader` and
  `data/reference/rule-policy.default.json` for file-based policy
  configuration. `InventoryAnalyzer` optionally overrides rarity
  classification from reference data; unknown species never lose protection.
- Added `SemanticIdentityKey` (normalized species/variant/IV/CP/nickname/
  catch-date key with completeness classification) persisted on Observations
  and PokemonRecords (schema v3, additive migration), `SemanticIdentityMatcher`
  (exact comparable-key cross-run matching, ambiguous collisions never merged)
  and CLI command `analyze-reidentification` comparing two run databases and
  reporting the re-match rate for the double-scan acceptance test.
- Fixed duplicate-group degeneration: `GroupKey` now groups by
  species/form/costume/shiny/shadow/background when species is known instead
  of falling back to a per-instance key.
- Added `docs/kravspecifikation.md` (full requirements specification) and
  `docs/MINIMAL_EFFORT_PLAN.md` (code review and minimal-effort plan).
- Offline self-tests pass 193/193. No phone input, no real-phone acceptance
  claim; the OCR spike and re-identification measurements require the other
  machine's local evidence.

## Unreleased

- Cleanup proof now keeps Appraisal open across ordinary Pokémon transitions,
  persists appraisal fingerprints before each next swipe, and exits Appraisal
  once at the end. Offline self-tests pass 163/163; real-phone acceptance is
  complete: 20/20 items, 19 appraisal swipes, zero Details swipes, and final
  GameplayMap with SQLite integrity `ok`.

## Long database acceptance — 2026-07-21

- Ran the permanent read-only cleanup proof with `age0-1825` and limit 20.
- Four Complete rows were persisted before appraisal and retained through a
  bounded `CursorProgression:Unknown` SafeStopped result.
- SQLite was reopened and reports were generated from the reloaded rows;
  integrity was `ok`, with four Observations, four PokemonRecords and twelve
  InventoryEvents. This is below the ten-item long-run acceptance threshold.

## Unreleased

- Repaired real Android canonical-close scale/position evidence and integrated
  the existing guarded Appraisal exit into autonomous startup unwind.
- Accepted bounded read-only phone unwinds from current state, Inventory,
  Details and Appraisal. The requested `pidgey&age0-365` value proof found no
  results and safely persisted an empty, integrity-checked SQLite run; no
  value-proof acceptance is claimed.

## Canonical close Android scale repair - 2026-07-21

- The first real diagnostic found the canonical Details X visually present but
  outside the locator's single-radius model; it stopped before input.
- Canonical locator scoring now evaluates a bounded set of scale-normalized
  button radii while retaining shell, crossing-stroke, position, dimensions
  and contrast evidence. Offline self-tests remain 162/162.
- No real-phone acceptance is claimed from the failed diagnostic.

## Canonical close unwind for autonomous startup - 2026-07-21

- Replaced cleanup startup's state-specific normalizer with bounded
  `CanonicalCloseUnwindService` and the named `close-canonical-screen`
  operation.
- Added screenshot-derived lower-centre canonical close detection with shell,
  crossing-stroke, position, dimension and contrast evidence; no fixed
  coordinate fallback is available.
- Canonical close uses three compatible targets, fresh revalidation, one tap,
  stable changed-state postcondition and a five-input loop/budget limit.
- A verified canonical close is the only allowed input on unsafe confirmation
  surfaces; affirmative/destructive controls remain blocked.
- Added synthetic positive/negative locator tests. Offline self-tests pass
  162/162. Real-phone acceptance is not claimed yet.

## Cleanup startup stability repair - 2026-07-21

- Saved bounded start-state recovery frames under the ignored evidence root.
- Ordinary Details startup now accepts three independent same-state topology
  frames when strict evidence signatures vary during model settling; appraisal
  ROI transitions retain the stricter consensus path.
- The first direct phone attempt stopped with zero recovery inputs because the
  original strict window was not stable. No phone acceptance is claimed and
  the full run was not automatically repeated in this iteration.
- Offline self-tests remain 162/162.

## Autonomous cleanup start recovery - 2026-07-21

- `device-run-cleanup-proof` now calls the shared bounded canonical-close
  unwind before inventory search; manual GameplayMap preparation is no longer
  a product requirement.
- Identity and read-only tags are persisted transactionally before appraisal;
  appraisal is best-effort and enriches an already durable row.
- Appraisal exit has one bounded, topology-scoped Details postcondition
  fallback after the single authorized exit tap.
- Added semantic-review templates, strict recommendation exports and separate
  human-review-only comparative cleanup suggestions. No real-phone result is
  claimed in this checkpoint.
- Offline self-tests pass 162/162. Real-phone acceptance is pending this
  checkpoint and is not claimed from tests alone.

## Cleanup proof pipeline implementation - 2026-07-21

- Added self-contained wrong-screen modal fixtures using `PixelImage` and
  `PngEncoder`; the tests no longer read ignored `local-data` screenshots.
- Added permanent `device-run-cleanup-proof` orchestration with bounded
  Complete/Partial/Unresolved identity bursts, guarded appraisal recovery and
  no blind cursor retry.
- Added SQLite ScanRuns, Observations, PokemonRecords and InventoryEvents
  round-trip persistence plus reports generated from a fresh database read.
- Added cleanup-proof self-tests; offline self-tests pass 160/160. Real-phone
  cleanup proof is pending and is not claimed here.

## Deterministic navigation safety acceptance tooling - 2026-07-21

- Added the permanent `device-validate-navigation-safety` command with bounded
  1-3 cycles, GameplayMap precondition evidence and guarded named-operation
  navigation through MainMenu, Inventory, Details, recovery and map close.
- Added phase-aligned `action-trace.jsonl` evidence with authorization,
  transport-returned input and exactly five post-input frames per input.
- Added an offline trace contract test; self-tests are now 159/159. This is
  validation tooling only and is not a real-phone acceptance claim.

## Deterministic navigation safety acceptance - 2026-07-21

- Three bounded cycles passed on the authorized OnePlus A6013 from verified
  GameplayMap and returned to verified GameplayMap.
- Each cycle recorded five named inputs, two guarded Back actions, 25
  post-input frames, zero unsafe inputs and all required postconditions.
- Evidence and the complete-repository handoff ZIP remain under ignored
  `local-data/validation/deterministic-navigation-safety`.

## Wrong-screen navigation authorization repair - 2026-07-21

- MainMenu -> Inventory now requires three stable typed MainMenu frames with
  positive menu and Inventory topology, no Details/Menu/Appraisal/modal
  conflict, and a fresh same-screen pre-tap revalidation.
- A conservative destructive-confirmation interlock blocks all named taps,
  search text/submit, Back and cursor swipes on confirmation surfaces. The
  observed Power Up modal is distinguished from normal Details buttons; no
  automatic Cancel is sent.
- Named input audits now retain required state, strict/visual observations,
  conflicts, targets, precondition and fresh screenshot hashes and whether
  input was sent. Offline self-tests pass 158/158. No phone acceptance is
  claimed; phone safety cycles await manual CANCEL from the incident modal.

## Repair Android sequence runtime - 2026-07-21

- Appraisal now uses `GuardedInventoryRecovery` for one visual Intro continue
  tap, stable Bars evidence and one visual Bars exit tap; Back is never sent on
  appraisal overlays.
- Cursor progression now requires observed transition evidence and captures
  three independent post-swipe Details frames. Identical fingerprints are
  allowed for separate adjacent instances; no-effect produces a fail-closed
  result without a second swipe.
- Controlled stops are resumable; terminal Unknown/Failure and Completed
  checkpoints are distinct and idempotent. Offline self-tests pass 157/157.
- Resume evidence numbering now appends within an existing output directory and
  cannot overwrite the earlier replay evidence.
- Lowered the Intro locator gate to 0.58 after a visually clear real-device
  Intro scored 0.594 with the avatar overlapping the dialog edge; the
  three-frame ROI consensus and one-tap bound remain unchanged.

## Android real-phone attempt blocked - 2026-07-21

- The bounded Task E attempt found no authorized ADB device; reconnect to the
  expected Wi-Fi serial failed with Windows socket error 10013.
- No production host input was sent. Tasks E/F remain blocked and Task G was
  skipped; no real-phone acceptance is claimed.

## Android verified sequence host and cursor - 2026-07-21

- Added `AndroidVerifiedInventoryNamedOperations` and the
  `device-run-index-sequence` CLI command using only named Android transport
  operations.
- Normal progression opens the first card once, performs one guarded Details
  swipe per next item, rejects no-effect identity, and writes bounded evidence.
- Checkpoints now carry cursor ordinals/fingerprints, identity status, evidence
  hashes and structured tag observations; resume requires overlap agreement.
- Index and classification tag application are separate and disabled by
  default. AI-Delete is never an executable tag operation.
- Offline self-tests cover first-card/cursor counts, no normal inventory
  returns, Partial continuation and fail-closed resume overlap.

## Task A stable identity consensus hardening - 2026-07-21

- `PokemonDetailsIdentityAnalyzer.Consensus` now requires at least three
  compatible usable frames before returning Complete; one or two frames remain
  Partial.
- Consensus fingerprints are deterministic bytewise-median fingerprints across
  all compatible frames, with deterministic candidate tie-breaking. Individual
  frame fingerprints and evidence hashes remain in the report.
- `identity-fingerprint` now returns exit codes 0 Complete, 2 Partial and 3
  Unavailable. Identity contract regressions pass 156/156.

## Task 5 Partial continuation hardening - 2026-07-21

- A Partial identity observation is now checkpointed and passed through the
  existing named `ReturnToInventory` operation before the bounded sequence
  continues with the next item.
- If Inventory is not verified after that recovery, the sequence remains
  fail-closed and stops with the Partial evidence preserved. Unknown states
  still stop immediately without further input.
- Added regression coverage for both safe continuation and failed recovery;
  package-free self-tests pass 155/155. Real-phone Task 5 acceptance remains
  unclaimed because no named-operation host is bound in this environment.

## Task 4 dynamic identity tuning - 2026-07-21

- Narrowed the Android Details tag search to the observed tag band and bounded
  accepted pill geometry, eliminating false tag counts on the captured
  zero/one/two-tag states.
- Prioritised a long, near-gray Details divider as the lower-content anchor and
  reduced the stable lower ROI so fixed bottom controls are excluded.
- Added synthetic coverage for shifted divider/content geometry. Self-tests pass
  155/155. Real acceptance remains PARTIAL: the three captured Details groups
  complete, the captured zero/one/two-tag states report 0/1/2 tags, and the
  zero-tag versus tagged fingerprint similarity is 0.9815 against a 0.965
  threshold; one fourth evidence group is Inventory rather than Details and is
  correctly reported Unavailable. No real-phone Task 4 approval is claimed.

## Task 5 sequence orchestration checkpoint - 2026-07-20

- Added `VerifiedInventoryTaskSequence` with named-operation boundaries,
  bounded limits, atomic per-item checkpoints, resume matching, ordinal IDs,
  Partial/Unknown stops and reversible tag policy.
- AI-Delete is rejected for auto-apply and no delete operation is exposed.
  Offline package-free tests pass 155/155; real-phone Task 5 acceptance is
  not claimed.

## Task 4 dynamic identity checkpoint - 2026-07-20

- Added `PokemonDetailsIdentityAnalyzer` and package-free models for separate
  screenshot evidence SHA-256, stable multi-ROI fingerprint, mutable tag
  observation, three-frame consensus and run-scoped ordinal instances.
- Added the `identity-fingerprint` CLI command and five deterministic identity
  regressions. Self-tests pass 154/154.
- Real OnePlus evidence captured four five-frame Details groups. The phone
  ended in unfiltered Inventory after a reversible AI-Indexed/AI-Review
  add/remove sequence. Full real tag-layout acceptance remains unclaimed.

## Unreleased Android navigation increment - 2026-07-20

- Added `device-set-pokemon-tag` and `TagSelector`: visible rows are found
  geometrically, names are identified by device-calibrated normalized
  multi-scale templates, and neither a row ordinal nor fixed row coordinate is
  accepted as identity.
- Added confidence and second-best-margin gates, a maximum of three controlled
  list scrolls, checkmark verification, Details-pill verification and an
  explicit `TAG_NOT_FOUND_NO_MUTATION` outcome. Repeated requests are
  idempotent.
- Completed two real OnePlus 6T Trade add/remove cycles on Ekans CP616 with
  zero wrong tag selections. Final verification showed `#Trade` count 0 and
  Ekans present under `!#Trade`; the phone ended in unfiltered Inventory.
- Added tag profile and workflow safety regressions; 148/148 self-tests pass.
- Extended real-phone acceptance to `AI-Indexed`, `AI-Review`, `AI-Keep` and
  `AI-Delete` as tag names. Each passed one add/remove cycle on Ekans CP616;
  AI-Delete triggered no delete or transfer behavior.
- Replaced the green-only Details-pill check with dynamic gray/colored pill
  component counting and before/after count deltas. Zero-, one- and two-tag
  layouts passed, including simultaneous `AI-Indexed` + `AI-Review`, followed
  by verified removal back to zero tags. 149/149 self-tests pass.
- Added `device-search-inventory` with guarded Open, Clear, Enter and Submit
  phases, visual pre/postconditions, bounded polling and per-action audit.
- Centralized remote-shell text encoding in `PogoInventory.Device`; callers
  pass ordinary queries. Real commands use the proven `%s#Trade` and
  `!\#Trade` encodings and a named Enter key event.
- Completed two real-phone rounds of `age0-7`, `age0-365`, `age0-1825`,
  `#Trade` and `!#Trade`. All ten query fields were visually correct, every
  result changed, and both final clear operations restored unfiltered Inventory.
- Added generic encoding, injection-escaping and fail-closed search workflow
  regressions; 146/146 self-tests pass.
- Replaced full-screen byte stability with state-specific ROI signatures for
  appraisal intro dialog/overlay anchors and the three transformed IV bars.
- Added a three-of-five frame consensus that rejects Unknown or conflicting
  evidence without requiring pixel-identical screenshots.
- Added the named, normalized `ExitAppraisal` action. AppraisalIntro authorizes
  one left-middle tap and requires AppraisalBars; AppraisalBars authorizes one
  left-middle tap and requires PokemonDetails. Only PokemonDetails authorizes
  Android Back to Inventory.
- Moved Unknown-stop, unexpected-state-stop and action limits into
  `GuardedInventoryRecovery`. An unchanged post-action substate terminates with
  `ACTION_NOT_OBSERVED`; it never authorizes a blind repeated tap.
- Added ROI, action-count, action-type and legacy-inline-removal regressions;
  144/144 self-tests pass.
- Completed three real OnePlus 6T appraisal recovery cycles with zero Unknown
  states, zero wrong states and zero Back actions on AppraisalBars. Every bars
  exit used exactly one normalized tap at approximately `(0.10, 0.50)` before
  the single Details-to-Inventory Back action.
- No transfer, delete, Calcy, OCR, location or arbitrary-shell behavior was
  added. Tag mutation is limited to a confidently identified existing tag and
  is reversible.

## 0.14.3

- Fixed the C# exception-pattern syntax in `AppraisalPretestRunner`.
- Kept `ScreenVisionException`, `InvalidDataException`, `NotSupportedException`, `ArgumentException` and `OverflowException` as recoverable per-file diagnostics.
- Kept the SHA-256-guarded removal script for `IMG_7699.png`.
- Kept the expected self-test total at 113.
- Added no phone input action.
- Confirmed a real-phone 3-item appraisal stability run on the connected OnePlus A6013 with zero Complete observations.
- Confirmed a real Calcy probe on `tesmath.calcy` and a real Calcy live-check against the same device.
- Kept the four named phone actions unchanged.

## 0.14.2

- Fixed the appraisal pretest so `ScreenVisionException` decoder failures are retained as diagnostics instead of terminating the run.
- Added a regression test using a synthetic unsupported 16-bit PNG.
- Added a SHA-256-guarded script that removes the known unsupported `IMG_7699.png` fixture without risking deletion of a changed file.
- Kept the accepted minimum based on successfully decoded images.
- Increased the expected self-test total to 113.
- Added no phone input action.

## 0.14.1

- Fixed CS0173 in `AppraisalAnalyzer` by declaring the candidate IV variable as `int?`.
- Preserved `null` when no appraisal track is detected and an integer from 0 to 15 when it is detected.
- Kept all appraisal geometry, thresholds, report schemas, phone preparation behavior and the 112-test expectation unchanged.
- Added no phone input action.

## 0.14.0

- Added the `PogoInventory.Appraisal` project.
- Added a normalised cross-platform appraisal profile derived from the real iPhone evidence.
- Added automatic translation and scale fitting for the three appraisal bars.
- Added colour-based track and orange-fill measurement with candidate IV estimates.
- Added a hard verification lock: the committed profile can produce Candidate but never Complete observations.
- Added an appraisal pretest over the committed iPhone screenshots, diagnostic overlays and a review ZIP.
- Added the read-only `phone-prepare` command, which captures one Android screenshot and generates a device-adjusted local profile when an appraisal screen is visible.
- Added `scripts/prepare-android-phone.ps1`.
- Added nine self-tests, bringing the expected total to 112.
- Added no phone tap or swipe action.

## 0.13.1

- Fixed the CropAtlas semantic evidence build by importing `PogoInventory.CropAtlas.Services` in `SemanticEvidenceRunner.cs`.
- Restored visibility of the existing `PixelImageTransforms` and `CropAtlasJson` helpers from the nested semantic namespace.
- Kept all semantic evidence behavior, report formats, thresholds and the 103-test expectation unchanged.
- Added no phone input action.

## 0.13.0

- Added a semantic evidence review-pack pipeline inside `PogoInventory.CropAtlas`.
- Added one derived crop set for every decoded screenshot and every selected candidate region.
- Added an intentionally empty truth-template for screen state, species, CP and IV values.
- Added a deterministic review ZIP containing only derived crops, contact sheets and manifests.
- Added explicit readiness states for external visual review, automated extraction and screenshot coverage.
- Added exact underrepresented-cluster reporting without blocking review-pack generation.
- Added GitHub Actions validation and six self-tests, bringing the expected total to 103.
- Kept automated extraction disabled until a populated truth manifest passes a zero-false-Complete gate.
- Added no phone input action.

## 0.12.0

- Added the `PogoInventory.CropAtlas` project.
- Added deterministic selection of strong, non-overlapping screen-state, dynamic-content and text-dense candidate regions.
- Added representative image selection for every discovered visual cluster.
- Added read-only candidate crops, per-region contact sheets and a cluster overview PNG.
- Added a readiness report that identifies underrepresented visual clusters and states exactly when more screenshots are useful.
- Added a package-free PNG encoder for derived local evidence.
- Added JSON, Markdown and CSV crop-atlas reports and GitHub Actions validation.
- Added six self-tests, bringing the expected total to 97.
- Added no phone input action and no OCR or IV claim.

## 0.11.1

- Fixed the CLI build by importing the RegionDiscovery Models and Services namespaces.
- Kept the existing project reference, algorithms, reports and 91-test expectation unchanged.
- Added static release validation for the required CLI imports and project reference.
- Added no phone input action and made no change to image-analysis thresholds.

## 0.11.0

- Added deterministic visual-region discovery over the committed iPhone screenshots.
- Added a normalised grid with luminance, edge, variation and cluster-separation metrics.
- Added provisional stable-chrome, screen-state, dynamic-content and text-dense candidate regions.
- Added JSON, Markdown and CSV reports plus real-image GitHub Actions validation.
- Added five regression tests, bringing the expected total to 91.
- Added no phone input actions and no semantic OCR claims.

## 0.10.1

- Changed the iPhone pretest gate to require at least the configured number of successfully decoded images instead of rejecting the entire batch because one extra file failed.
- Added a default 90 percent minimum decode-rate gate, so widespread decoder failures still stop CI.
- Added the rejected file name, error type and error detail to console and Markdown diagnostics.
- Added regression tests for one isolated decode failure and for an excessive decode-failure rate.
- Preserved every phone-navigation and Calcy safety boundary.

## 0.10.0

- Added the `PogoInventory.ImagePretest` project.
- Added deterministic indexing of committed iPhone PNG screenshots.
- Added package-free decode, geometry, orientation, file SHA-256 and visual fingerprint checks.
- Added all-pairs similarity, exact duplicate, near-duplicate and deterministic clustering reports.
- Added JSON, Markdown and CSV output without copying source screenshots.
- Added the `image-pretest` CLI command and PowerShell runner.
- Added a conditional GitHub Actions gate over `data/iphone-images/*.png`.
- Added six self-tests, bringing the expected total to 84.
- Added no new phone input action and made no Android or Calcy claim from iPhone data.

## 0.9.0

- Added the local Calcy provider verification workspace and evidence-ingestion runner.
- Added expected-versus-observed comparison for identity, CP and all three IV values.
- Added explicit WrongComplete and IncorrectIncomplete outcomes.
- Added the minimum 20-case, zero-false-Complete long-scan gate.
- Added provider selection locked to verification-report and parser-profile SHA-256 hashes.
- Added JSON, Markdown and CSV reports, scripts, CI coverage and ten self-tests.
- Added no new phone input methods and no production provider claim without real evidence.

## 0.8.0

- Added `IAndroidAppInspectionTransport` with fixed read-only package, process, logcat, accessibility, app-ops and service operations.
- Added the `PogoInventory.CalcyProbe` project.
- Added Calcy package metadata parsing, installed-version discovery and capability reporting.
- Added local raw evidence files and SHA-256 locking.
- Added `calcy-probe` and `calcy-live-check` CLI commands.
- Added automatic one-Pokémon navigation before live Calcy inspection.
- Added a profile-driven regex parser for proven raw text output.
- Added Complete, Partial, Conflicting and Failed parser outcomes without guessed values.
- Added synthetic Calcy package, log and parser fixtures.
- Added ten self-tests, bringing the expected total to 68.
- Added CI probe, live-check and parser verification.
- Preserved the four-action input boundary and all destructive-operation prohibitions.

## 0.7.0

- Added automatic core screen-profile bootstrap from a known InventoryList start.
- Added the `PogoInventory.Bootstrap` project and local profile acceptance workflow.
- Added the `PogoInventory.Observations` project and `ICalcyObservationProvider` boundary.
- Added Complete, Partial, Conflicting, Failed and Unavailable observation states.
- Attached structured nullable observations to every inventory scan item.
- Added raw provider output SHA-256 validation.
- Added fake, scripted and unavailable providers.
- Upgraded inventory checkpoints to schema 2.0 with schema 1.0 migration.
- Added tests for partial, conflicting, failed and migrated observations.
- Added CI checks for core bootstrap and three deterministic fake observations.

## 0.6.2

- Fixed the only failing 0.6.1 self-test.
- Replaced the stale hard-coded expected harness version `0.2.0` with `DeviceHarnessOptions.CurrentVersion`.
- Bumped the runtime version and deterministic fake build fingerprints to 0.6.2.
- Preserved all automatic-navigation behavior and safety boundaries.

## 0.6.1

- Fixed the GitHub Actions compile failure in `InventoryAutomationRunner.FinishAsync`.
- Declared the conditional completion timestamp as `DateTimeOffset?`, matching `InventoryScanCheckpoint.CompletedAtUtc`.
- Preserved all 0.6.0 automatic-navigation behavior and guardrails.
- Added a regression release note and updated project handoff documents.

## 0.6.0

- Added the isolated `PogoInventory.Automation` project.
- Added `IAndroidAutomationTransport` with only validated tap and swipe operations.
- Added ADB input implementations using fixed `shell input tap` and `shell input swipe` forms.
- Added normalised, validated control points and swipe configuration.
- Added automatic navigation from inventory list to appraisal.
- Added screen-state verification after every input action.
- Added independent identity-region fingerprinting to verify that each swipe reached a different Pokémon.
- Added repeated-swipe end-of-inventory detection.
- Added automatic PNG evidence capture, SHA-256 hashes and atomic checkpointing after every Pokémon.
- Locked checkpoints to exact automation-profile and screen-profile SHA-256 values.
- Added strict resume matching against device, geometry, profile and last identity fingerprint.
- Added battery temperature and unpowered low-battery checks.
- Added a deterministic scripted Android transport and three distinct appraisal fixtures.
- Added the `inventory-scan` CLI command and real/fake PowerShell scripts.
- Added five self-tests, bringing the expected total to 52.
- Added a CI automatic inventory-navigation run.
- Removed per-image privacy approval from the target automatic scan workflow.
- Preserved the prohibition on transfer, tagging, text input, resource use, gameplay, location changes and anti-detection behaviour.

## 0.5.0

- Added a versioned guided real-screen capture plan.
- Added private `incoming/<ScreenState>/` screenshot staging.
- Added capture-session plan fingerprint, device serial and exact geometry locks.
- Added SHA-256 verification for every recorded incoming screenshot.
- Added pixel-identical duplicate detection and exclusion from variation coverage.
- Added required-state progress, next-state recommendation and capture reports.
- Added interactive manual-navigation capture and single-state capture commands.
- Added explicit privacy-review confirmation before fixture promotion.
- Added safe, idempotent promotion into the approved local fixture manifest without overwriting untracked files.
- Added PowerShell scripts for capture, status and approval.
- Added thirteen capture-workflow self-tests, bringing the expected total to 47.
- Preserved the read-only boundary with no taps, swipes, text input, app launching, tagging or transfer.

## 0.4.0

- Added the isolated `PogoInventory.Calibration` project.
- Added a private calibration workspace with a mandatory marker and ignored local layout.
- Added state-folder fixture indexing and stable fixture identifiers.
- Added SHA-256 locking, approval preservation for unchanged files and approval reset after changes.
- Added explicit account, location, notification, other-data and calibration approval fields.
- Added strict fixture path-containment checks.
- Added versioned fixture manifests and anchor plans.
- Added multiple approved fixture samples per anchor.
- Added local profile generation from approved PNG fixtures.
- Added per-state coverage and recall requirements.
- Added false-positive, false-negative and known-state misclassification accounting.
- Added confusion-matrix, weak-anchor and similarity-separation reports.
- Added JSON, Markdown and CSV acceptance outputs.
- Added synthetic calibration data and end-to-end CI profile generation and validation.
- Added eight calibration self-tests, bringing the expected total to 34.
- Added local PowerShell calibration scripts and detailed privacy and fixture-approval documentation.
- Preserved the read-only boundary with no taps, swipes, text input, tagging or transfer.

## 0.3.1

- Fixed the `PngDecoder` Paeth-filter compile failure reported by GitHub Actions.
- Changed `Paeth` to accept and return `int`, matching the integer values produced by PNG unfiltering before the final checked byte conversion.
- No detector behaviour, thresholds, phone access or safety boundary changed.
- Added a 3 x 2 RGBA PNG fixture whose second row uses PNG Paeth filter type 4.
- Added a regression self-test that verifies exact reconstructed RGBA pixels.
- Added a regression note and updated handoff documents.

## 0.3.0

- Added the isolated `PogoInventory.Vision` project.
- Added a package-free PNG decoder for 8-bit non-interlaced grayscale, RGB, grayscale-alpha and RGBA images.
- Added bounded PNG decompression, dimension limits and structured vision errors.
- Added normalised screen regions and deterministic Color, Grayscale and Edge fingerprints.
- Added Required, Optional and Forbidden screen anchors.
- Added self-contained JSON screen profiles with Base64 reference fingerprints.
- Added explicit orientation, minimum resolution and aspect-ratio validation.
- Added deterministic state scoring, confidence thresholds and winner-margin conflict handling.
- Added detailed per-state and per-anchor evidence models.
- Added `screen-detect` and `screen-fingerprint` CLI commands.
- Added synthetic fixtures for InventoryList, PokemonDetails, AppraisalOpen, PokemonMenuOpen, TagDialogOpen, SearchOpen, Loading, Popup and NetworkError.
- Added incomplete, conflicting, noisy and landscape fixtures for fail-closed tests.
- Added self-tests for PNG decoding, known states, Unknown handling, orientation and threshold determinism.
- Extended CI with synthetic screen detection and fingerprint extraction.
- Added Screen State Detector documentation and updated project handoff files.

## 0.2.0

- Added the isolated `PogoInventory.Device` project.
- Added read-only ADB process execution with `ArgumentList`, timeouts and cancellation.
- Added authorised-device discovery and explicit serial selection.
- Added parsers for ADB device lists, Android properties, screen size and battery state.
- Added read-only screenshot capture with PNG signature validation.
- Added atomic screenshot and metadata output with SHA-256 manifest.
- Added structured device error codes and CLI exit codes.
- Added console logging and a fake Android transport.
- Added the `device-snapshot` CLI command and PowerShell scripts.
- Expanded package-free self-tests for device parsing, selection, cancellation and output.
- Added GitHub Actions CI to build, test, run the analysis demo and run a fake capture.
- Added agent instructions and device/data-handling documentation.

## 0.1.0

- Created the initial C# solution.
- Added Pokémon observation and decision models.
- Added nullable protective status fields.
- Added JSON-configured policy.
- Added conservative KEEP / REVIEW / DELETE analysis.
- Added duplicate preservation and a preliminary PvP heuristic.
- Added JSON and Markdown reports.
- Added package-free self-tests.
- Added architecture, guardrails, roadmap, project state and continuation prompt.
## 0.14.3 - 2026-07-20

- Added shared read-only Pokemon GO game-state detection for Inventory,
  PokemonDetails, PokemonMenu, Appraisal and Unknown.
- Added state-evidenced `device-detect-game-state` output with screenshot hash.
- Added fail-closed `device-recover-inventory` with a maximum of two validated
  Back actions and post-action screenshots.
- Real detection identified PokemonDetails at confidence 1.000; recovery did
  not reach Inventory and stopped after the first Back attempt.
## 0.14.3 - 2026-07-20

- Added explicit `GameplayMap` state detection using the existing main-menu
  Poké Ball locator.
- Tightened PokemonDetails detection to require independent page topology.
- Added offline `game-state-detect-image` regression command.
- No phone input was sent; live acceptance remained open because the phone was
  in PokemonMenu during the read-only capture window.

## Android sequence real-phone acceptance - 2026-07-21

- Task 7 passed with three real records and two guarded observed transitions.
- Task 8 passed controlled stop/resume with overlap comparison and no duplicate
  recording of the overlap item.
- Task 9 stopped fail-closed at a real no-effect swipe after three records;
  no blind retry was sent. The final phone state was recovered to GameplayMap.
- Evidence and failure notes are retained under the ignored Android sequence
  validation root; no local phone data was committed.

## Cursor changed-identity fallback - 2026-07-21

- A missed transient animation now falls back to three independent stable
  Details frames and accepts a changed stable fingerprint as
  `SUCCESS_CHANGED_IDENTITY`.
- Unchanged identity returns explicit `NoEffectOrEndOfFilter` without a blind
  second swipe.
- Guarded recovery accepts only bounded visual Details topology as a fallback
  for detector Unknown; Back still requires an Inventory postcondition.
- Offline validation remains green at 157/157. The requested age0-1825 phone
  run was stopped at an unsafe Power Up confirmation screen and is not claimed
  complete.
