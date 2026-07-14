# iPhone image pretest

Version 0.10.0 adds a deterministic pretest for uncropped iPhone Pokémon GO screenshots.

The pretest is intentionally cross-platform. It validates the screenshot pipeline before the fixed Android phone is available, but it does not prove that Android navigation or Calcy integration works.

## Input

The command reads PNG files from one directory:

```powershell
.\scripts\run-iphone-image-pretest.ps1
```

Equivalent CLI command:

```powershell
dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
  image-pretest `
  --input .\data\iphone-images `
  --out .\out\iphone-image-pretest `
  --min-images 20
```

The command never changes the source screenshots.

## Automatic checks

For every PNG the pretest records:

- file name and relative path
- byte length
- SHA-256
- decoded width and height
- aspect ratio and orientation
- geometry group
- a combined grayscale and edge fingerprint hash

Across the complete set it calculates:

- corrupt or unsupported files
- pixel-identical duplicate pairs
- visually near-identical pairs
- pairwise similarity
- visual clusters using normalised whole-screen fingerprints
- consecutive-image similarity

## Acceptance gate

The default gate requires:

- at least 20 PNG screenshots
- every PNG decodes successfully
- every screenshot is portrait
- at least two distinct file hashes

Exact and near duplicates are reported but do not fail the gate unless the complete set contains only one distinct screenshot.

## Output

The output directory contains:

```text
iphone-image-pretest.json
iphone-image-pretest.md
iphone-images.csv
iphone-similarity.csv
```

No source screenshot is copied to the output directory.

## What this proves

A green pretest proves that the real iPhone images can pass through the package-free PNG decoder, normalised fingerprint extraction, hashing, clustering and reporting pipeline.

## What this does not prove

The pretest does not validate:

- ADB connectivity
- Android tap or swipe coordinates
- Android animation timing
- Calcy IV package behaviour
- Calcy overlay extraction
- Android end-of-inventory detection

Those still require the fixed Android phone.
