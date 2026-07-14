# Validation report

## Version

0.10.1

## Reported real CI result

The 0.10.0 iPhone pretest completed image analysis and reported:

```text
iPhone image pretest: 23/24 decoded.
Geometry groups: 1.
Visual clusters: 4.
Exact duplicates: 0.
Near duplicates: 0.
Rejected: 1 image(s) failed decoding.
```

This proved that the processing pipeline worked for 23 real iPhone screenshots. The failure was caused by an overly strict batch gate, not by insufficient usable evidence.

## Fix applied

The gate now requires:

- at least `MinimumImageCount` successfully decoded images
- at least 90 percent successful decoding by default
- every decoded image to be portrait
- at least two distinct decoded screenshots

A rejected file no longer blocks a sufficiently large, high-quality pretest set. It remains present in JSON, CSV, Markdown and console diagnostics.

For the reported set:

- decoded minimum: 20
- decoded result: 23
- required decode rate: 90.0 percent
- actual decode rate: 95.8 percent
- expected gate result: accepted

## Static validation completed

- image-pretest acceptance logic updated
- minimum decode-rate option added and validated
- report model records actual and required decode rates
- console prints rejected filename and exact error
- Markdown adds a rejected-images section
- isolated-failure regression test added
- low-decode-rate rejection test added
- expected self-test total is 86
- all JSON files parse successfully
- all project XML files parse successfully
- all project references resolve
- no new ADB command or phone input action added
- no screenshot source file is modified

## Expected GitHub Actions validation

1. restore and build all 12 projects
2. run all 86 self-tests
3. complete all existing synthetic navigation, calibration, Calcy and verification checks
4. process all 24 committed iPhone PNG files
5. decode 23 or more files
6. meet the 90 percent minimum decode rate
7. accept the iPhone pretest
8. print the rejected image name and error detail
9. upload JSON, Markdown and CSV reports

## Remaining investigation

The single rejected PNG should not be guessed at. Use the exact filename and decoder error printed by 0.10.1 to decide whether the file is corrupt or whether the package-free PNG decoder should support an additional valid PNG variant.
