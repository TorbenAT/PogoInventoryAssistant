# Header OCR (species / CP / nickname extraction)

Status: wired into the cleanup flow (`device-run-cleanup-proof` and
`analyze-cleanup-evidence`) as the header OCR engine, backed by Tesseract
(see "Tesseract is now the cleanup-flow engine" below). `ocr-header-spike`
remains the offline measurement tool, with `--engine winrt` kept available
for comparison. Read-only: none of this taps, swipes or changes any device
or game state.

## Why

Screen-state detection, identity fingerprinting and appraisal bar reading do
not currently read *text*. Species, CP and nickname have so far only come
from Calcy (`PogoInventory.CalcyProbe` / `PogoInventory.Observations`). This
infrastructure adds a second, independent read path based on on-device OCR of
the Pokemon Details / Appraisal header, so species/CP/nickname can eventually
be cross-checked against (or substituted for) the Calcy read.

## Layering

```
PogoInventory.HeaderText   (net8.0, plain)
  - ITextRecognizer, RecognizedTextLine      (dependency-light OCR abstraction)
  - HeaderScreenType, HeaderAnalysisProfile  (ROI configuration)
  - ISpeciesReference, StaticSpeciesReference
  - PokemonHeaderAnalyzer                    (parsing + multi-frame consensus)
  - SearchQueryClassifier

PogoInventory.HeaderOcr    (net8.0-windows10.0.19041.0)
  - WindowsMediaTextRecognizer : ITextRecognizer
    (Windows.Media.Ocr via CsWinRT projections pulled in automatically by the
    net8.0-windows TFM; no extra NuGet package required)

PogoInventory.TesseractOcr (net8.0, plain)
  - TesseractTextRecognizer : ITextRecognizer
    (TesseractOCR NuGet package; native tesseract/leptonica DLLs, no WinRT)

PogoInventory.Cli          (net8.0-windows10.0.19041.0, references all three)
  - `ocr-header-spike --engine <winrt|tesseract>` command
```

`PogoInventory.HeaderText` intentionally has no dependency on Windows-only
APIs (only on `PogoInventory.Vision` for `NormalizedRegion`), so
`PogoInventory.SelfTest` can exercise `PokemonHeaderAnalyzer` and
`SearchQueryClassifier` with a scripted `FakeTextRecognizer` without ever
touching a real OCR engine. `PogoInventory.SelfTest` does **not** reference
`PogoInventory.HeaderOcr`.

Because `PogoInventory.HeaderOcr` requires the Windows-only TFM,
`PogoInventory.Cli` had to move from `net8.0` to
`net8.0-windows10.0.19041.0` as well (it is still an ordinary console app;
nothing in the guardrails changes).

## Species reference data

The OCR spike validates species text against the real reference data already
maintained by `PogoInventory.Core.Reference` /
`data/reference/species-reference.json` (loaded with
`PogoInventory.Core.Reference.SpeciesReferenceLoader`, ~1025 named species).
If that file is missing, the command falls back to an empty
`StaticSpeciesReference`, species validation is skipped, a
`SPECIES_REFERENCE_MISSING` warning is recorded, and the raw header text is
still reported per-file under `SpeciesCandidate` (rather than being silently
dropped) so a human can eyeball what OCR actually read.

`StaticSpeciesReference` does its own case-insensitive, diacritic-insensitive
matching plus a tolerant edit-distance-<=1 fallback, so OCR noise such as a
missing Nidoran gender glyph (♀/♂) or a single misread character can still
resolve to the correct species.

## PokemonHeaderAnalyzer

- Reads two ROIs per frame: a CP region and a name/species region, both
  screen-type-specific (`HeaderScreenType.PokemonDetails` /
  `AppraisalBars`), via `HeaderAnalysisProfile`.
- CP: accepts `CP`/`cp` prefix variants (or no legible prefix if the glyph OCR
  as noise), requires the remaining text to be digits, and range-validates
  10..6000. Anything else, or a bare "CP" with no digits, fails
  `CP_NOT_READ`.
- Name/species: the first non-blank recognized line is checked against a
  UI-label blacklist (`CP`, `Appraise`, `Attack`, `Defense`, `HP`, `Cancel`,
  `Done`, `Power Up`, `Evolve`, `Transfer`) — those can never be treated as a
  species (`HEADER_TEXT_IS_UI_LABEL`). Otherwise it is validated against
  `ISpeciesReference`; a match sets `Species` (nickname stays null); no match
  sets `Nickname` to the raw text and `Species` stays null (Unknown), with
  reason `HEADER_TEXT_NOT_SPECIES`.
- `PokemonHeaderAnalyzer.Consensus(frames)` mirrors the existing
  `PokemonDetailsIdentityAnalyzer.Consensus` pattern
  (`src/PogoInventory.Vision/Imaging/PokemonDetailsIdentityAnalyzer.cs`):
  species is only accepted when >= 2 of the supplied frames agree on the
  normalized species; CP is only accepted when >= 2 frames agree on the same
  integer. Anything else is Unknown, with `SPECIES_CONSENSUS_NOT_REACHED` /
  `CP_CONSENSUS_NOT_REACHED`.

### Default ROIs

Spike-tuned against the real 20-item appraisal/details evidence set
(`local-data/validation/ocr-spike/frames-appraisal` +
`frames-details`, 60 frames total) -- the CP region is now the Tesseract
iteration-4 tuned ROI (tightened height, see "Measured iterations" below),
committed as `HeaderAnalysisProfile`'s defaults since Tesseract is now the
cleanup-flow OCR engine. The WinRT `ocr-header-spike` path can pass
`--profile` with the old `Y=0.07,Height=0.07` CP region to reproduce its
own prior 60/60 species measurement.

| Region | X | Y | Width | Height |
|---|---|---|---|---|
| Details CP | 0.28 | 0.08 | 0.44 | 0.05 |
| Details name/species | 0.15 | 0.41 | 0.70 | 0.08 |
| Appraisal CP | 0.28 | 0.08 | 0.44 | 0.05 |
| Appraisal name/species | 0.15 | 0.41 | 0.70 | 0.08 |

The name/species Y is still close to the original identity header region
(`PokemonIdentityFingerprintProfile.HeaderRegion`, Y=0.42 h=0.045 in
`src/PogoInventory.Vision/Models/PokemonIdentityModels.cs`), widened on X
and Height to reliably capture the full species/nickname line across both
screen types.

## SearchQueryClassifier

Classifies an inventory search query as:
- `ExactSpecies` — a single species token (validated against
  `ISpeciesReference`), optionally combined with non-species filters via `&`
  (e.g. `pidgey&age0-365` -> ExactSpecies("Pidgey")).
- `BroadFilter` — anything with a `!`/`#`/`@` operator prefix, a comma or `*`
  wildcard list (`0*,1*,2*`), a numeric range (`age0-1825`,
  `distance200-`), or a bare word that does not validate against
  `ISpeciesReference`.

This exists so a future indexing/search workflow can decide whether a query
result set is expected to be a single species (and can therefore be
cross-checked against OCR'd species) versus an arbitrary filter.

## `ocr-header-spike` command

```
ocr-header-spike --input <directory of PNGs> --screen <details|appraisal> --out <directory>
                 [--profile <header-analysis-profile.json>] [--language <tag>]
                 [--engine <winrt|tesseract>] [--tessdata <directory>] [--binarize-cp]
```

- `--input`: a directory of PNG frames (one screen each).
- `--screen`: `details` or `appraisal` — selects the ROI set.
- `--out`: writes `ocr-spike-report.json` (per-file raw OCR lines with
  bounds, parsed species/CP/nickname, confidences, failure reasons) and
  `ocr-spike-report.md` (hit-rate summary + distinct species + per-file
  table).
- `--profile`: optional JSON override of `HeaderAnalysisProfile` (same shape
  as the record — `DetailsCpRegion`, `DetailsNameRegion`,
  `AppraisalCpRegion`, `AppraisalNameRegion`, each `{X,Y,Width,Height}`).
- `--language`: optional BCP-47 language tag. For `--engine winrt`, passed to
  `OcrEngine.TryCreateFromLanguage` (falls back to `en`, then the user's
  profile languages). For `--engine tesseract`, only `eng`/`en` is mapped
  today (`TesseractOCR.Enums.Language.English`).
- `--engine`: `winrt` (default) or `tesseract` — see "Tesseract alternative
  engine" below.
- `--tessdata`: tessdata directory for `--engine tesseract` (default
  `tools/tessdata`).
- `--binarize-cp`: `--engine tesseract` only. Applies the shared Otsu
  binarization to the CP crop before recognition. Off by default —
  measured worse, see below.

`--engine winrt` is Windows-only (it links `Windows.Media.Ocr`) and is meant to
be run on the machine that has the real evidence, e.g.:

```
local-data\validation\cleanup-value-proof\appraisal-carousel-20
```

(20 real Pokemon Details screenshots captured during the cleanup-value-proof
carousel run). That directory is local-only per `docs/GUARDRAILS.md` and is
never committed.

### Acceptance target

For the offline spike to be considered a success against the 20-screenshot
evidence set: **>= 19/20 species reads correct** and **>= 19/20 CP reads
correct** (single-frame, not consensus — the real scan pipeline can apply
`Consensus` across repeated frames of the same Pokemon on top of this).

### Profile tuning workflow

1. Run `ocr-header-spike` with the default profile against the 20-screenshot
   set.
2. Open `ocr-spike-report.md`. For any file where species or CP was not
   read, check `ocr-spike-report.json` for that file's `rawLines` — each
   recognized line carries `normalizedBounds` (X/Y/Width/Height on the full
   frame). Use those bounds to see whether the CP/name ROI in
   `HeaderAnalysisProfile` missed the text, clipped it, or captured extra
   noise (e.g. an evolve/power-up button label creeping into the ROI).
2. Adjust the ROI(s) in a copy of the profile JSON and re-run with
   `--profile <file>`.
3. Repeat until the acceptance target is met, or until failures are
   understood well enough to fix in `PokemonHeaderAnalyzer` itself (e.g. a
   new CP prefix noise pattern, or a species reference gap).
4. Only after the spike consistently meets the target should a follow-up
   change wire this into the live scan pipeline (not part of this change).

## Tesseract alternative engine

`PogoInventory.TesseractOcr.TesseractTextRecognizer` is a second
`ITextRecognizer` implementation, added specifically to test whether a
different OCR engine avoids the WinRT engine's deterministic thin-"1"-stroke
digit drop in CP text (see "CP digit-drop experiments" above). Selected via
`ocr-header-spike --engine tesseract`.

### Package choice

[`TesseractOCR`](https://www.nuget.org/packages/TesseractOCR) 5.5.2 (Kees van
Spelde's actively-maintained fork of the older `charlesw/tesseract` binding),
not the original `Tesseract` package (charlesw, last released 2022, bundles
the older tesseract50 engine). Both wrap the native tesseract/leptonica DLLs
via plain P/Invoke (x86/x64 DLLs copied to the output folder by a `.targets`
file) -- no WinRT involved -- so `PogoInventory.TesseractOcr` stays on the
ordinary `net8.0` TFM, not `net8.0-windows10.0.19041.0`. `TesseractOCR` ships
the newer tesseract55 engine and exposes `Pix.Image.LoadFromMemory(byte[])`
plus a `Page.Layout` object tree (`Blocks` -> `Paragraphs` -> `TextLines` ->
`Words`) with per-line `BoundingBox`/`Confidence`, which is exactly the shape
`ITextRecognizer` needs to map back to `NormalizedBounds` the same way
`WindowsMediaTextRecognizer` does for WinRT's `OcrResult.Lines`.

`TesseractTextRecognizer` decodes the frame PNG with the repo's own
`PngDecoder` (`PogoInventory.Vision`), crops and nearest-neighbor-upscales
with the new pure `HeaderOcrCropScaler` (shares crop geometry with
`HeaderOcrGeometry.ComputeUpscale`, so both engines get comparable input
sizes), then re-encodes the crop with `PngEncoder` for Tesseract's `Pix`
loader -- no `System.Drawing` anywhere. Per-region character whitelist and
segmentation mode come from the pure `TesseractOcrConfigSelector`
(`PogoInventory.HeaderText`): CP is whitelisted to `"CPcp0123456789"`,
species/nickname text is unrestricted; both force single-line segmentation.

### tessdata choice

`tessdata_fast` (`tools/tessdata/eng.traineddata`, ~4MB) is committed --
small enough that committing is simpler than a download step, and it is the
default `--tessdata` path. `tessdata_best` (~15.4MB) measurably improved
both species and CP reads (see iteration table below) but is over the
"commit if small" line, so it is **not** committed; `tools/tessdata-best/`
is gitignored. To use it:

```
curl -sL -o tools/tessdata-best/eng.traineddata https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata
ocr-header-spike ... --engine tesseract --tessdata tools/tessdata-best
```

### Measured iterations (real 60-frame set: 57 appraisal + 3 details)

Scored at the item level (>= 2 of 3 frames must agree on the same value; 20
items total -- item 1 from `frames-details`, items 2-20 from
`frames-appraisal` in consecutive same-Pokemon triplets), against the ground
truth in this change's task description (not committed -- it names the real
capture set).

| # | Change | Species | CP | Notes |
|---|---|---|---|---|
| 1 | `tessdata_fast`, whitelist, single-line PSM, nearest-neighbor upscale, default ROI | 18/20 | 14/20 | 1 **false positive**: item 15 (Volbeat, actual 961) agreed 761 |
| 2 | Switch to `tessdata_best`, otherwise identical | 19/20 | 16/20 | Same false positive persists (agreed 261 instead of 761) |
| 3 | + CP-only Otsu binarization (`--binarize-cp`, reusing `HeaderOcrBinarization`) | 19/20 | 15/20 | Regressed, like the WinRT experiment; false positive got *worse* (agreed 61, all 3 frames) and broke 2 previously-correct items |
| 4 | Revert binarization; tighten the CP ROI height (`Y=0.08,Height=0.05` vs default `Y=0.07,Height=0.07`) to cut the animated background bokeh out of the crop | 19/20 | 17/20 | **False positive fixed** (item 15 now reads 961 on all 3 frames); zero wrong agreed values across all 20 items; item 13 (Illumise, CP164) regressed from correct to unread |

Iteration 4 is the best-measured configuration: `--engine tesseract
--tessdata tools/tessdata-best --profile <tightened-CP-ROI-profile>` (no
`--binarize-cp`). This is now the shipped `HeaderAnalysisProfile` default and
the default tessdata for the cleanup flow (see below) -- Tesseract replaced
WinRT as the cleanup flow's OCR engine, so its best-measured configuration
took over the shared defaults. The WinRT `ocr-header-spike` path can pass
`--profile` with the old `Y=0.07,Height=0.07` CP region to reproduce its own
prior 60/60 species measurement.

## Tesseract is now the cleanup-flow engine

`device-run-cleanup-proof` and `analyze-cleanup-evidence` build their header
OCR analyzer via `TryCreateHeaderSemanticsAnalyzer`, which now creates a
`TesseractTextRecognizer` (previously `WindowsMediaTextRecognizer`/WinRT).
Tessdata directory resolution: `--tessdata <directory>` on either command,
default `tools/tessdata-best` (the iteration-4 winner above; gitignored, not
committed -- see "tessdata choice" below to fetch it). When the resolved
directory does not contain a usable `eng.traineddata`, the analyzer is not
created (a clear `[cleanup] No Tesseract engine could be created from ...`
message is written to stderr) and the command falls back to its pre-OCR
behaviour (species/CP simply stay at whatever the query/database already
had), the same safe degrade `TryCreateHeaderSemanticsAnalyzer` already used
for a missing WinRT language pack. `ocr-header-spike` is unchanged: it still
defaults `--tessdata` to `tools/tessdata` (`tessdata_fast`, committed) and
still supports `--engine winrt` for side-by-side comparison.

### Verdict vs the acceptance target

Species meets the >= 19/20 target on every iteration from #2 onward. CP does
**not** reach >= 19/20 in any iteration (best: 17/20) -- the remaining misses
are unread digits (2-of-3 frames don't agree), not wrong values. The
single hardest safety requirement -- **never emit a confidently wrong CP** --
is met at iteration 4: item 4 (Pikachu, CP 129) reads 129 or is otherwise
never misread as `29` in any iteration, and item 20 (Hoopa, CP occluded by
the model) never produces a false consensus. Reported honestly: Tesseract
(this configuration) is a safer engine than WinRT on this set (no false
positives) but has not been tuned to beat WinRT's CP hit rate outright; it
remains available as a second, independently-measured `ITextRecognizer` for
future tuning rather than a wholesale WinRT replacement.

## Tests

`tests/PogoInventory.SelfTest/HeaderOcrTests.cs` (registered in
`Program.cs`) covers, via a scripted `FakeTextRecognizer`
(`tests/PogoInventory.SelfTest/FakeTextRecognizer.cs`):

- species consensus accepts 2-of-3 agreement; conflicting frames -> Unknown
- UI labels are never treated as species
- unrecognized header text becomes Nickname with Species Unknown
- CP parsing: `CP1234` -> 1234, bare `CP` rejected, out-of-range rejected,
  2-of-3 CP consensus
- tolerant species normalization: case-insensitive, single-character OCR
  noise, missing gender glyph
- `SearchQueryClassifier`: exact species, species + filter combo, and the
  various broad-filter forms
- `HeaderOcrGeometry.ComputeUpscale`: crop-size thresholds, including the
  164px-tall CP region boundary
- `HeaderOcrBinarization`: luminance conversion, Otsu threshold selection
  (including the empty-input fallback and a two-cluster split), pixel
  binarization and alpha handling
- `TesseractOcrConfigSelector`: CP region gets the digit whitelist, name
  region gets none; both force single-line segmentation
- `HeaderOcrCropScaler`: crop+upscale is a pure nearest-neighbor block
  replication (no blending) and `upscale=1` is a plain crop

None of these tests touch `PogoInventory.HeaderOcr`, `PogoInventory.TesseractOcr`
or a real OCR engine. `PogoInventory.SelfTest` is deliberately package-free
(no third-party NuGet dependencies) so it stays fast and dependency-light;
`TesseractTextRecognizer` itself (the part that actually calls the
`TesseractOCR` package) is therefore exercised only by the manual
`ocr-header-spike --engine tesseract` runs above, not by the self-test
harness -- the same split `WindowsMediaTextRecognizer` already has with
`Windows.Media.Ocr`.

### CP digit-drop experiments (spike, not wired into production)

Real-frame spike numbers (60 frames: 57 appraisal + 3 details) with the
default ROIs above:

| Change | Species | CP | Notes |
|---|---|---|---|
| Baseline (old 60px upscale cutoff) | 60/60 | 50/60 | Starting point |
| Raise upscale cutoff to 220px (both regions) | 60/60 | 53/60 | Shipped default |
| + CP-only Otsu binarization | 60/60 | 44/60 | Regressed vs. upscale-only; reverted |
| CP-specific 3x upscale (no binarization) | 60/60 | 52/60 | Also regressed vs. upscale-only; reverted |

The 220px upscale cutoff is the only change kept in
`WindowsMediaTextRecognizer`. Two follow-up ideas -- CP-only binarization and
a stronger CP-specific upscale -- were each tried and measured worse on the
real set, so neither is wired in; `HeaderOcrBinarization` and the
`ITextRecognizer.RecognizeAsync` `regionKind` hint remain as tested,
available building blocks for a future attempt. Even after the upscale
change, digit-drop failures remain on two known items: Swampert
(0094-0096, actual CP 1659) reads `None`/`69`/`1659` -- no two frames agree,
so consensus is not reached -- and Minun (0110-0112, actual CP 412) reads
`None`/`412`/`41` -- same result. Hoopa (0190-0192) stays unread across all
experiments; its CP is physically occluded by the Pokemon model in every
captured frame and is not a software-fixable case.
