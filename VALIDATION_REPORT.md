# Validation report

## Version

0.4.0

## Validation performed in the assistant environment

The assistant environment does not contain the .NET SDK, so the final C# build and test execution must run in GitHub Actions.

The following checks were completed locally:

- all 87 C# files parsed without syntax errors using the tree-sitter C# grammar
- solution and project references were checked
- all JSON files parsed successfully
- GitHub Actions YAML was inspected for the new build and acceptance commands
- synthetic fixture SHA-256 values were generated from the committed PNG bytes
- the calibration anchor plan was generated from the accepted synthetic profile geometry
- the profile-generation and detector calculations were independently simulated against all 14 synthetic fixtures
- the simulation produced 14 correct classifications
- the simulation produced zero false positives
- the simulation produced zero false negatives
- the simulation produced zero known-state misclassifications
- the simulation produced zero weak anchors after composite-negative handling
- release ZIP structure, required files and archive integrity were checked during packaging

## Expected GitHub Actions checks

- restore the .NET 8 solution
- compile all six projects with warnings treated as errors
- run 34 package-free self-tests
- run the inventory analysis demo
- run a fake read-only device snapshot
- run synthetic screen detection
- extract a synthetic fingerprint
- build a profile from the synthetic fixture manifest and anchor plan
- validate all synthetic fixtures and require an accepted report
- upload all validation output

## New regression and acceptance coverage

- private workspace creation
- fixture path containment
- fixture indexing
- approval preservation for unchanged SHA-256
- approval reset after PNG change
- changed approved fixture rejection
- profile generation from approved fixtures
- synthetic acceptance success
- explicit false-positive rejection
- JSON, Markdown and CSV report output

## Remaining validation

- GitHub Actions compilation and all self-tests
- real Windows local-workspace execution
- real Android screenshots
- real Pokémon GO anchor calibration
- real false-positive acceptance

No real phone, account or inventory data is included in this release.
