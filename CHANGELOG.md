# Changelog

## Unreleased Android navigation increment - 2026-07-20

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
- No transfer, tag, delete, Calcy, OCR, location or arbitrary-shell behavior
  was added.

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
