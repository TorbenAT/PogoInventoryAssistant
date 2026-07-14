# Release 0.11.0

## Visual-region discovery from real iPhone screenshots

Version 0.11.0 adds `PogoInventory.RegionDiscovery` and the `image-region-discovery` command.

The accepted iPhone screenshots are divided into a normalised 12 by 24 grid. For every cell the system measures luminance, edge density, global variation, consecutive variation, within-cluster variation and between-cluster separation.

It then produces provisional candidate regions for:

- stable UI chrome
- screen-state discrimination
- changing Pokémon-specific content
- edge-dense areas that may contain text or numbers

The labels are deliberately provisional. No OCR, species, CP or IV claim is made.

## Real-image CI path

GitHub Actions now runs the visual-region discovery after the iPhone image pretest and verifies:

- at least twenty decoded images
- one geometry group
- at least two visual clusters
- all 288 grid cells
- at least one candidate of each visual behaviour type

## Outputs

- JSON report
- Markdown summary
- per-cell CSV
- candidate-region CSV
- image-to-cluster CSV

## Tests

Five deterministic self-tests were added:

- accepted grid and candidate generation
- deterministic metrics
- report writing
- mixed-geometry rejection
- isolated decode-failure retention

Expected total: 91 self-tests.

## Safety

No phone input method was added. The source screenshots are read only. Android navigation, Calcy integration and transfer boundaries are unchanged.
