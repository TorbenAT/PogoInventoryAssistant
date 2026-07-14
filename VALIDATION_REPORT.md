# Validation report

## Version

0.6.0

## Environment limitation

The build environment used to prepare this handoff does not contain the .NET SDK, MSBuild or ADB. A real C# compile and test run could therefore not be executed here.

GitHub Actions is the authoritative compile and runtime validation for this release.

## Static validation completed

- complete repository copied from accepted 0.5.0 source
- new project and project references added to the solution
- all JSON files parsed successfully
- all seven project XML files parsed successfully and every project reference resolves
- PowerShell and YAML files inspected
- all 121 C# files parsed without syntax errors using the tree-sitter C# grammar
- C# brace counts checked across the source tree
- all required source and documentation files present across 200 repository files
- synthetic appraisal variants preserve the existing appraisal and network-state anchor regions
- ZIP root contains the solution directly
- no `bin`, `obj`, `.git`, `local-data`, private captures or real inventory data included

## Expected CI validation

GitHub Actions must:

1. restore .NET 8 projects
2. build the full solution with warnings as errors
3. run 52 self-tests
4. run the existing analysis demo
5. run the fake device snapshot
6. run the deterministic automatic inventory scan
7. capture exactly three fake inventory items
8. run synthetic screen detection
9. build and validate the synthetic calibration profile
10. upload validation output

## New automated checks

- ADB tap command is exactly allow-listed
- ADB swipe command is exactly allow-listed
- automation profile loads and validates
- automatic setup performs three named taps
- three distinct appraisal items are captured
- repeated unchanged swipes detect the end
- maximum item count stops cleanly
- completed checkpoint does not issue new input on rerun
- checkpoint stores exact SHA-256 locks for both profiles

## Manual review performed

- no text-input method added
- no arbitrary shell method exposed
- no transfer or destructive action added
- no random timing or random coordinates added
- real data remains excluded by `.gitignore`
- automatic scan path has no per-image approval requirement

## Release gate

Do not begin the next milestone until GitHub Actions is green and reports all 52 tests passing.
