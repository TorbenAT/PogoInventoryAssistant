# Validation report

## Version

0.8.0

## Environment limitation

The preparation environment does not contain the .NET SDK, MSBuild or ADB. GitHub Actions remains the authoritative compilation and runtime test.

## Static validation completed

- complete 0.7.0 repository copied as the base
- new `PogoInventory.CalcyProbe` project added to the solution
- all project XML files parsed successfully
- every project reference resolves
- all JSON fixtures and parser profiles parse successfully
- all C# files parse without syntax errors using tree-sitter C#
- expected self-test declaration count is 68
- ZIP root contains the solution directly
- no `bin`, `obj`, `.git`, `local-data`, real screenshots or inventory databases included

## Expected GitHub Actions validation

GitHub Actions must:

1. restore and build all ten projects with warnings as errors
2. run 68 self-tests
3. run the existing analysis demo
4. run the existing fake device and three-item automatic inventory scan
5. verify checkpoint schema 2.0 and Pikachu, Machop, Eevee observations
6. build and accept the automatic core profile
7. run the scripted Calcy package probe
8. verify package version 4.3.1 and two filtered lines
9. run the automatic scripted one-Pokémon live check
10. verify the live check reaches one item without manual navigation
11. parse the synthetic Calcy output as Pikachu, CP 501, IV 15/14/13
12. run all existing screen and calibration validation

## New test coverage

- exact ADB command allow-list for app inspection
- package metadata parser
- missing package detection
- PID and package log filtering
- probe evidence and report output
- automatic live-check navigation
- complete text parsing
- partial text parsing
- conflict detection
- raw output preservation and hashing

## Release gate

Do not enable a real Calcy provider for a long scan until real-phone evidence has been captured and a 20-Pokémon verification run has zero false Complete observations.
