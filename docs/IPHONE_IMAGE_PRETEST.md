# iPhone image pretest

Version 0.11.0 provides a deterministic pretest for uncropped iPhone Pokémon GO screenshots.

The pretest is cross-platform. It validates the screenshot-processing pipeline before the fixed Android phone is available, but it does not prove Android navigation or Calcy integration.

## Run

```powershell
.\scripts\run-iphone-image-pretest.ps1
```

Equivalent CLI command:

```powershell
dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
  image-pretest `
  --input .\data\iphone-images `
  --out .\out\iphone-image-pretest `
  --min-images 20 `
  --min-decode-rate 0.90
```

The command never changes the source screenshots.

## Recorded data

For every PNG the pretest records:

- file name and relative path
- byte length
- SHA-256
- decoded width and height when available
- aspect ratio and orientation
- geometry group
- visual fingerprint hash
- decoder error type and detail when rejected

Across the decoded set it calculates:

- exact duplicates
- near duplicates
- pairwise similarity
- visual clusters
- consecutive-image similarity

## Acceptance gate

The default gate requires:

- at least 20 successfully decoded PNG screenshots
- at least 90 percent of discovered PNG files to decode
- every decoded screenshot to be portrait
- at least two distinct decoded screenshots

An isolated rejected file does not fail the batch when enough valid evidence remains. Rejected files are never silently ignored: they remain in all diagnostic reports and are printed to the console.

Widespread decoding failure still rejects the pretest through the minimum decode-rate gate.

## Output

```text
iphone-image-pretest.json
iphone-image-pretest.md
iphone-images.csv
iphone-similarity.csv
```

The Markdown report includes a dedicated rejected-images table. No source screenshot is copied to the output directory.

## Reported real set

The first real run found:

- 24 PNG files
- 23 successfully decoded
- 95.8 percent decode rate
- one geometry group
- four visual clusters
- zero exact duplicates
- zero near duplicates

Version 0.11.0 accepts this as a useful pretest set while preserving the one rejected file for decoder investigation.

## Limits

The pretest does not validate:

- ADB connectivity
- Android tap or swipe coordinates
- Android animation timing
- Calcy IV package behaviour
- Calcy overlay extraction
- Android end-of-inventory detection
