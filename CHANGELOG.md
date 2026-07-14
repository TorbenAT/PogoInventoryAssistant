# Changelog

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
