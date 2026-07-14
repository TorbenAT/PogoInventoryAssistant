# Release 0.14.2

## Purpose

Prevent one unsupported PNG fixture from terminating the appraisal pretest.

## Root cause

`PngDecoder` throws `ScreenVisionException` with error code
`UnsupportedPng`. The appraisal pretest retained several framework exception
types but did not include this domain exception.

## Fix

`AppraisalPretestRunner` now catches `ScreenVisionException` and records:

- file name
- SHA-256
- error type
- error detail
- decoded status false

The acceptance gate still uses successfully decoded images.

## Known fixture removal

Run:

```powershell
.\scripts\remove-known-unsupported-iphone-fixture.ps1
```

The script removes `data/iphone-images/IMG_7699.png` only when its SHA-256 is:

```text
ef40abb395c0e17f87706731322ea492d7071b2bd9ee26c26ab97c7242551738
```

It refuses to delete any different file.

## Scope

No appraisal geometry, color threshold, IV calculation, report schema or phone
action changed.

Expected total: 113 self-tests.
