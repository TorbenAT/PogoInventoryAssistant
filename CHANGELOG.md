# Changelog

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
