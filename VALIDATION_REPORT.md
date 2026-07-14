# Validation report

## Version

0.7.0

## Environment limitation

The packaging environment does not contain the .NET SDK or ADB. GitHub Actions remains the authoritative compile and runtime validation.

## Static validation completed

- 130 C# files parsed successfully with the tree-sitter C# grammar
- no syntax-error or missing nodes found
- all JSON files parsed successfully
- all project XML files parsed successfully
- every project reference resolves
- solution contains nine projects
- expected self-test declaration count is 58
- synthetic core anchor regions were independently checked against all six bootstrap images
- each required state matched only its own expected fixtures at the configured threshold
- all required release files are present
- no `bin`, `obj`, `.git`, local captures or private inventory data are included

## Synthetic anchor check

At threshold 0.98:

- InventoryList anchor matched only InventoryList
- PokemonDetails anchor matched only PokemonDetails
- PokemonMenuOpen anchor matched only PokemonMenuOpen
- AppraisalOpen anchor matched all three appraisal examples and no other core state

## Expected GitHub Actions validation

1. restore and build nine projects
2. run 58 self-tests
3. run the fake device snapshot
4. run the fake inventory scan
5. verify checkpoint schema 2.0
6. verify all three fake observations are Complete
7. verify species order Pikachu, Machop, Eevee
8. run the automatic core profile bootstrap
9. verify bootstrap acceptance with zero false positives and misclassifications
10. run the existing screen and calibration checks
11. upload validation output

## Known limitation

No real Calcy IV output mechanism has been proven yet. The real provider remains Unavailable by design. Version 0.7.0 must not be described as ready to extract a real 10,000-item inventory until the real-device adapter is implemented and tested.

## Release gate

Do not run the full real inventory scan for data extraction until version 0.7.0 is green and the next real-Calcy milestone is complete.
