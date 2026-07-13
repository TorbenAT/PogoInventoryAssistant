# Validation report

## Version

0.5.0

## Accepted previous checkpoint

Torben reported that the complete 0.4.0 GitHub Actions workflow was green.

## Validation performed in the assistant environment

The assistant environment does not contain the .NET SDK, so the final C# build and test execution must run in GitHub Actions.

The following checks were completed locally for 0.5.0:

- all 101 C# files parsed without syntax errors using the tree-sitter C# grammar
- all 6 project files parsed as valid XML
- all 5 committed JSON files parsed successfully
- solution and project-reference directions were checked for cycles
- `PogoInventory.Calibration` now references only the read-only device abstraction and vision layer
- the public Android transport interface was checked and still exposes only device listing, metadata reads and screenshot capture
- no tap, swipe, text input, app launch, arbitrary shell or transfer method was added
- all new local paths are contained under the initialised private workspace
- capture and promoted fixture paths use path-traversal protection
- capture files and promoted fixtures are linked by SHA-256
- duplicate screenshots are excluded from coverage and rejected during promotion
- capture-plan fingerprint, device-serial and exact-geometry locks are enforced before capture persistence
- capture status prefers incomplete required states before optional states
- interrupted promotion has a verified idempotent repair path
- promotion refuses to overwrite a fixture file that exists outside the manifest
- PowerShell scripts were inspected for explicit workspace, ADB and privacy-confirmation arguments
- release source tree contains 170 files and no build output, private workspace, capture session or real screenshot directories
- release ZIP structure, required files and archive integrity are checked during packaging

## Expected GitHub Actions checks

- restore the .NET 8 solution
- compile all six projects with warnings treated as errors
- run 45 package-free self-tests
- run the inventory analysis demo
- run a fake read-only device snapshot
- run synthetic screen detection
- extract a synthetic fingerprint
- build a profile from the synthetic fixture manifest and anchor plan
- validate all synthetic fixtures and require an accepted report
- upload all validation output

## New self-test coverage

Version 0.5.0 adds tests for:

- guided capture workspace structure
- incoming screenshot and session persistence
- duplicate screenshot exclusion from coverage
- device-change rejection
- geometry-change rejection
- changed capture-plan rejection
- changed incoming screenshot rejection
- mandatory privacy-review confirmation
- reviewed capture promotion
- untracked fixture-file overwrite rejection
- promotion idempotency
- duplicate promotion rejection
- required-state priority over optional states
- JSON and Markdown capture-status output

The expected total is 47 tests.

## Remaining validation

- GitHub Actions compilation and all 47 self-tests
- real Windows execution with Android Platform Tools
- real Android device serial and screenshot geometry locking
- real private screenshot capture
- visual privacy review and fixture promotion
- phone-specific anchor selection
- real screen-profile acceptance with zero false positives and zero wrong known-state classifications

No real phone, account, location or inventory data is included in this release.
