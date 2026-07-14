# iPhone visual region discovery

Version 0.12.0 turns the accepted iPhone screenshot pretest into a deterministic visual-region analysis.

The command does not perform OCR and does not claim that any area contains a particular Pokémon field. It measures how normalised screen regions behave across the decoded screenshots and their automatically discovered visual clusters.

## Run

```powershell
.\scripts\run-iphone-region-discovery.ps1
```

Equivalent CLI command:

```powershell
dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
  image-region-discovery `
  --input .\data\iphone-images `
  --out .\out\iphone-region-discovery `
  --min-images 20 `
  --min-decode-rate 0.90 `
  --grid-columns 12 `
  --grid-rows 24
```

## Measurements

Each cell in the normalised grid records:

- mean luminance
- mean edge density
- cross-image variation
- consecutive-image variation
- variation inside visual clusters
- separation between visual clusters
- provisional stable-chrome score
- provisional screen-state score
- provisional dynamic-content score
- provisional text-density score

The candidate labels describe visual behaviour only:

- `StableChrome`: visually stable with persistent edge structure
- `ScreenStateDiscriminator`: differs between clusters but remains stable inside a cluster
- `DynamicContent`: changes substantially between screenshots
- `TextDense`: edge-dense and changing, making it a possible text or number region

None of these labels are a species, CP, IV or status reading.

## Outputs

```text
out/iphone-region-discovery/iphone-region-discovery.json
out/iphone-region-discovery/iphone-region-discovery.md
out/iphone-region-discovery/iphone-region-cells.csv
out/iphone-region-discovery/iphone-region-candidates.csv
out/iphone-region-discovery/iphone-region-image-clusters.csv
```

Source screenshots are read only and are never copied to the report directory.

## Acceptance gate

The discovery report requires:

- the image pretest to pass
- the configured minimum number of decoded images
- exactly one screenshot geometry group
- at least two visual clusters
- a complete normalised grid

A single rejected PNG remains visible in the earlier pretest diagnostics and is excluded from metric calculations.

## What this proves

A green real-image run proves that the application can locate stable, changing and cluster-discriminating areas consistently across the available iPhone screenshots.

It does not prove:

- correct OCR
- correct IV-bar interpretation
- Android coordinates or timing
- Calcy overlay geometry
- complete Pokémon observations

The report is the evidence used to select the next visual extraction regions. Additional screenshots are only needed when the real report shows weak cluster coverage or missing screen-state variation.
