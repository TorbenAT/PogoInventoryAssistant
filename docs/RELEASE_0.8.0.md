# Release 0.8.0

## Calcy investigation and verification layer

This release adds the machinery needed to determine how the currently installed Calcy IV version can provide structured observations.

### Added

- named read-only Android app-inspection transport
- package metadata and version parser
- process, accessibility, overlay app-ops and service inspection
- full and filtered local logcat evidence
- current-screen evidence and SHA-256 manifest
- JSON and Markdown Calcy probe reports
- automatic one-Pokémon live check using the existing navigation actions
- profile-driven raw text parser
- source-specific regex patterns with timeout
- Complete, Partial, Conflicting and Failed parser outcomes
- synthetic package, log and parser fixtures
- CI checks for probe, live check and parser

### Important limitation

No production Calcy provider is claimed as working yet. The real phone must prove whether structured log output, another text surface or visual overlay extraction is the correct implementation.

### Safety

No new phone input method was added. The probe uses read-only ADB inspection. Final transfer remains manual and is not implemented.
