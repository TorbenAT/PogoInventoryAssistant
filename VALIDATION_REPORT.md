# Validation report

## Version

0.9.0

## Accepted input

Torben reported that version 0.8.0 is fully green in GitHub Actions.

## Environment limitation

The packaging environment does not contain the .NET SDK or ADB. GitHub Actions remains the authoritative compile and runtime validation.

## Static validation completed

- complete 0.8.0 repository unpacked
- new Verification project added to the solution
- CLI and self-test project references added
- all JSON files parsed successfully
- all project XML files parsed successfully
- all project references resolve
- all C# files parsed with the tree-sitter C# grammar
- no syntax-error nodes found
- 78 self-test declarations counted
- synthetic twenty-case verification manifest parsed
- ZIP contains no `bin`, `obj`, `.git` or local real evidence
- no new Android input interface or action added

## Expected CI validation

GitHub Actions must:

1. restore and build all eleven projects with warnings as errors
2. run 78 self-tests
3. retain all existing analysis, device, navigation, calibration and Calcy probe checks
4. parse twenty synthetic raw evidence files
5. report twenty `ExactComplete` results
6. report zero `WrongComplete` results
7. recommend the synthetic provider for long scan
8. create a provider selection with verification and parser SHA-256 hashes

## Release gate

Do not enable a real provider for a long scan until real phone evidence passes the same verification gate.
