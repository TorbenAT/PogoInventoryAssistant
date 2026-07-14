# Validation report

## Version

0.10.0

## Accepted prior checkpoint

Torben reported version 0.9.0 fully green in GitHub Actions with 78 self-tests and the synthetic provider verification gate passing.

## Real screenshot input

The latest repository commit lists 24 PNG files under `data/iphone-images`, from `IMG_7681.png` through `IMG_7705.png` with `IMG_7695.png` absent.

The preparation environment cannot download the binary GitHub files directly into the local build tree. The release therefore adds deterministic processing code and a CI step that runs against the screenshots already committed in Torben's repository.

## Static validation completed

- new `PogoInventory.ImagePretest` project created
- project added to the solution
- CLI and self-test project references added
- `image-pretest` command added
- PowerShell runner added
- conditional GitHub Actions pretest added
- output reports contain metadata and hashes, not copied screenshots
- all 180 C# files parsed without syntax errors using the tree-sitter C# grammar
- all 11 JSON files parsed successfully
- all 12 project XML files parsed successfully
- every project reference resolves and every project is present in the solution
- GitHub Actions YAML parsed successfully
- six new self-tests declared
- expected self-test total is 84
- no new ADB write operation or phone input action added
- no screenshot source file is modified

## Expected GitHub Actions validation

GitHub Actions must:

1. restore and build all 12 projects
2. run all 84 self-tests
3. complete all existing synthetic navigation, calibration, Calcy and verification checks
4. find at least 20 committed iPhone PNG files
5. decode every committed iPhone PNG
6. confirm every committed screenshot is portrait
7. create `iphone-image-pretest.json`
8. create `iphone-image-pretest.md`
9. create `iphone-images.csv`
10. create `iphone-similarity.csv`
11. upload all reports in the existing validation artifact

## Release gate

Do not treat the iPhone images as proof of Android navigation or Calcy extraction. Use them only as cross-platform visual fixtures until the fixed Android phone is available.
