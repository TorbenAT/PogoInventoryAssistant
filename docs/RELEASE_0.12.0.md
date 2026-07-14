# Release 0.12.0

## Purpose

Turn the real iPhone region-discovery result into a compact crop atlas that
can show whether the current screenshot set is sufficient for semantic
experiments.

## New project

`PogoInventory.CropAtlas`

## Outputs

- cluster overview PNG
- selected candidate crop PNG files
- one contact sheet per selected candidate
- JSON report
- Markdown report with embedded derived images
- region, crop and cluster CSV files

## Image sufficiency decision

The report does not guess which Pokémon GO screen a visual cluster
represents. It does determine whether every visual cluster has enough
representative screenshots for the next experiment.

`readiness.needsMoreImages` is true only when a specific visual cluster has
fewer images than the requested representative count. The report names those
clusters explicitly.

## Safety

- source screenshots are read only
- no phone action was added
- no OCR result is claimed
- no IV result is claimed
- no hidden game API is used

Expected total: 97 self-tests.
