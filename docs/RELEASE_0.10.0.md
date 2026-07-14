# Release 0.10.0

## iPhone screenshot pretest

This release adds a non-destructive, label-free pretest for the committed iPhone Pokémon GO screenshots in `data/iphone-images`.

The pretest:

- enumerates PNG files in deterministic filename order
- decodes every screenshot through the existing package-free PNG decoder
- records geometry, orientation, size and SHA-256
- creates normalised grayscale and edge fingerprints
- calculates all pairwise visual similarities
- detects exact and near duplicates
- groups visually related screenshots into deterministic clusters
- writes JSON, Markdown and CSV reports
- returns a failing exit code when the minimum screenshot gate is not met

## Real repository data

Torben committed 24 screenshots separately in the `iphone images` commit. The release ZIP contains the pretest code and a directory README. Unpacking the ZIP over the repository does not delete or replace the committed PNG files.

## CI gate

When `data/iphone-images/*.png` exists, GitHub Actions automatically requires:

- at least 20 screenshots
- zero PNG decode failures
- portrait orientation for every screenshot
- at least two distinct screenshots

The validation output artifact includes the generated pretest reports, not copies of the screenshots.

## Scope

The iPhone screenshots are useful for testing image decoding, geometry normalisation, fingerprint extraction, duplicate detection and clustering.

They are not evidence that Android ADB navigation or Calcy integration works.

## Safety boundary

No new phone action was added. The allowed input boundary remains:

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```
