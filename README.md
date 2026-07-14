# Pogo Inventory Assistant

Version 0.11.1

Pogo Inventory Assistant is a local tool for building a complete Pokémon GO inventory, analysing it and later applying safe batch tags. Final transfer remains manual.

Version 0.11.1 uses the accepted real iPhone screenshot set to discover stable, changing and cluster-discriminating normalised screen regions. The output is evidence for the next extraction step; it does not yet perform OCR or claim species, CP or IV values.


## iPhone screenshot pretest

The committed iPhone screenshots can be processed without labels or manual approval:

```powershell
.\scripts\run-iphone-image-pretest.ps1
```

The pretest writes:

```text
out/iphone-image-pretest/iphone-image-pretest.json
out/iphone-image-pretest/iphone-image-pretest.md
out/iphone-image-pretest/iphone-images.csv
out/iphone-image-pretest/iphone-similarity.csv
```

It checks that at least 20 PNG screenshots decode, remain portrait and contain at least two distinct images. It also reports exact duplicates, near duplicates and visual clusters. It never modifies or copies the source screenshots.

This is a cross-platform image-pipeline test. It does not validate Android ADB navigation or Calcy output.

## iPhone visual-region discovery

After the pretest passes, run:

```powershell
.\scripts\run-iphone-region-discovery.ps1
```

The system divides every decoded screenshot into a normalised 12 by 24 grid and measures:

- stable edge-bearing regions
- areas that separate the visual clusters
- areas that change between consecutive images
- edge-dense areas that may later support text or number extraction

Reports are written to:

```text
out/iphone-region-discovery/iphone-region-discovery.json
out/iphone-region-discovery/iphone-region-discovery.md
out/iphone-region-discovery/iphone-region-cells.csv
out/iphone-region-discovery/iphone-region-candidates.csv
out/iphone-region-discovery/iphone-region-image-clusters.csv
```

All candidate labels are provisional visual descriptions. No OCR or IV interpretation is performed. Source screenshots are read only.

## What works now

### Automatic phone navigation

The program can:

- start from the Pokémon inventory list
- open the first Pokémon
- open the menu and Appraise
- swipe through the inventory automatically
- verify the screen state and identity region after every action
- stop at the end, on an unsafe state or on a mismatch
- checkpoint every Pokémon and resume safely

There is no image-by-image approval and no manual navigation during the long scan.

### Automatic core screen profile

From one known inventory-list screen, the bootstrap command automatically captures and validates:

```text
InventoryList
PokemonDetails
PokemonMenuOpen
AppraisalOpen, three different Pokémon
```

The generated local profile is rejected on false positives or misclassification.

### Calcy installation and capability probe

The new `calcy-probe` command inspects the fixed Android phone through named read-only ADB operations. It records:

- installed package and package path
- installed version name and version code
- target and minimum Android SDK where available
- declared activities, services, receivers and permissions
- current process id
- accessibility state
- app-ops, including overlay evidence
- currently running services
- recent logcat and a locally filtered Calcy subset
- a current screenshot
- SHA-256 for every evidence file

The configured package is `tesmath.calcy`, matching the official Google Play listing.

All output stays under `local-data/` and is ignored by Git.

### Automatic one-Pokémon live check

`calcy-live-check` performs the complete one-time verification path automatically:

```text
InventoryList
→ first Pokémon
→ menu
→ Appraise
→ wait for Calcy
→ inspect package, process, services and logs
→ optionally parse a structured observation
```

It uses only the existing allow-listed taps. It does not require manual navigation.

### Profile-driven raw-output parser

A new parser can convert proven text output into the existing observation model. The parser is configured by JSON regular expressions rather than hard-coded assumptions.

Supported fields include:

- species and Pokédex number
- form
- CP, HP and level
- Attack, Defense and HP IV
- gender
- fast and charged moves

The parser:

- preserves the complete raw source and SHA-256
- returns `Complete` only when species or Pokédex number, CP and all three IV values exist
- returns `Partial` when only some fields are proven
- returns `Conflicting` when sources disagree
- returns `Failed` when no configured fields are recognized
- never guesses missing values

The committed parser profile and raw output are synthetic test data. They are not claimed to match the installed Calcy version.

### Structured inventory observations

Every scanned item can store:

```text
Complete
Partial
Conflicting
Failed
Unavailable
```

along with the nullable Pokémon fields, provider information, warnings, raw output and hash.

### Inventory analysis

- configurable KEEP, REVIEW and DELETE policy
- duplicate grouping and strictly-better duplicate requirement
- preliminary PvP candidate protection
- JSON and Markdown decision reports

### Calcy provider verification gate

- imports local raw evidence or parsed observations
- compares 20 or more cases with expected identity, CP and IV values
- counts wrong Complete observations separately
- requires zero wrong Complete observations
- writes JSON, Markdown and CSV reports
- locks a selected provider to report and parser-profile SHA-256 hashes
- refuses provider selection when the gate fails

## Validate the repository

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-fake-core-profile-bootstrap.ps1
.\scripts\run-fake-inventory-scan.ps1
.\scripts\run-fake-calcy-probe.ps1
.\scripts\run-fake-calcy-live-check.ps1
.\scripts\parse-synthetic-calcy-output.ps1
```

The expected test count is 91.

## Real-device Calcy probe

```powershell
.\scripts\run-local-calcy-probe.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

The automatic live check is run with the local profiles:

```powershell
dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
  calcy-live-check `
  --adb "C:\Android\platform-tools\adb.exe" `
  --profile .\local-data\automation-profile.local.json `
  --screen-profile .\local-data\screen-profile.local.json `
  --out .\local-data\calcy-live-check
```

Do not provide a parser profile on the first real run. First inspect which output mechanism actually exists on the phone.

## Current real-device limitation

Version 0.11.1 can verify a proven candidate mechanism across at least 20 local cases and can locate provisional visual regions in the accepted iPhone screenshots. It still does not claim that a real mechanism works until evidence from the fixed Android phone passes the gate.

The next implementation must be selected from the real local evidence:

1. structured log output, if proven
2. another documented text surface, if proven
3. visual reading of the Calcy overlay, if text output is not available

Until then, a long real scan keeps observations `Unavailable` rather than inventing data.

## Safety boundary

Phone input remains limited to:

- tap first inventory card
- tap details menu
- tap Appraise
- swipe to next Pokémon

The repository contains no functions for transfer, evolve, power-up, purify, TM use, purchases, catching, spinning, battles, location changes or anti-detection behaviour.

Read next:

- `PROJECT_STATE.md`
- `NEXT_PROMPT.md`
- `docs/IPHONE_VISUAL_REGION_DISCOVERY.md`
- `docs/CALCY_DEVICE_PROBE.md`
- `docs/CALCY_LIVE_CHECK.md`
- `docs/CALCY_TEXT_PARSER.md`
- `docs/GUARDRAILS.md`
- `VALIDATION_REPORT.md`
