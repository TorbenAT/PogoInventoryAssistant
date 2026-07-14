# Validation report

## Version

0.12.0

## Accepted prior result

Torben reported version 0.10.1 fully green in GitHub Actions.

The real iPhone fixture set previously produced:

```text
23/24 decoded
one geometry group
four visual clusters
zero exact duplicates
zero near duplicates
```

## New implementation

Version 0.12.0 adds a deterministic visual-region discovery layer.

For each cell in a configurable normalised grid it calculates:

- luminance
- edge density
- global variation
- consecutive variation
- within-cluster variation
- between-cluster separation
- four provisional candidate scores

Adjacent high-scoring cells are grouped into normalised candidate rectangles.

## Static validation completed

- new `PogoInventory.RegionDiscovery` project added
- project references added to CLI and self-test projects
- project added to the solution and all configurations
- CLI command and PowerShell script added
- GitHub Actions real-image step added
- JSON, Markdown and CSV report writers added
- no source screenshot copying added
- no new Android input interface or action added
- all JSON files parsed successfully
- all project XML files parsed successfully
- every project reference resolves
- all C# files parsed for syntax before packaging
- expected self-test declaration count: 91

## Expected GitHub Actions validation

GitHub Actions must:

1. restore and build all 13 projects
2. run 91 self-tests
3. keep the existing iPhone image pretest green
4. run `image-region-discovery` against `data/iphone-images`
5. accept at least twenty decoded screenshots
6. confirm one geometry group
7. confirm at least two visual clusters
8. produce exactly 288 cells for the 12 by 24 grid
9. produce at least one candidate of each provisional kind
10. upload all reports in `validation-output`

## Interpretation boundary

A green result proves deterministic localisation of stable, changing and cluster-discriminating image areas.

It does not prove OCR, IV-bar interpretation, Android coordinates, Calcy overlay extraction or complete Pokémon observations.

No additional images should be requested until the real 0.12.0 region and cluster reports have been inspected.

## 0.12.0 compile correction

The GitHub build proved that the new RegionDiscovery library itself compiled.
The four failures were isolated to unresolved symbols in the CLI integration.

Static checks for this patch confirm:

- the CLI project references `PogoInventory.RegionDiscovery`
- `Program.cs` imports `PogoInventory.RegionDiscovery.Models`
- `Program.cs` imports `PogoInventory.RegionDiscovery.Services`
- all four previously unresolved types exist and are public
- the declared self-test count remains 91

The preparation environment does not contain the .NET SDK, so GitHub Actions
remains the authoritative compilation and execution check.

## Version 0.12.0 static validation

The preparation environment does not contain the .NET SDK. GitHub Actions
therefore remains the authoritative compiler and test runner.

Static validation completed:

- 14 project files parsed as XML
- every project reference resolves
- every project is present in the solution
- CLI contains both CropAtlas namespace imports
- CLI contains the `image-crop-atlas` command
- self-test project references CropAtlas
- 97 self-tests are declared
- all JSON files parse
- GitHub Actions contains crop-atlas execution and verification
- generated ZIP integrity passes
- no committed source screenshot is modified or duplicated in the handoff
