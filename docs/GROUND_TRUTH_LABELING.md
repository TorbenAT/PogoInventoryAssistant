# Ground-truth labeling for Task-K

This is an offline measurement workflow. It does not change OCR, crops,
capture, carousel navigation, matching thresholds or identity rules, and it
never connects to a phone.

## Prepare the labeling pack

From the repository root:

```powershell
dotnet run --project src/PogoInventory.Cli -- `
  prepare-ground-truth `
  --evidence local-data/validation/reid-pilot-2x50/task-k `
  --out local-data/validation/ground-truth-task-k
```

The command reads only `run1/captured-observations.json` and
`run2/captured-observations.json`. It produces:

- `ground-truth.csv`: one row per observation, with scanner fields in separate
  `Scanner*` columns and blank manual fields;
- `labeling.html`: details evidence, appraisal evidence and scanner output
  side-by-side for manual review;
- `manifest.json`: schema version, source root and row count.

The initial status is `Unverifiable`, not `Verified`. A reviewer must fill the
manual fields from readable evidence, set `GroundTruthStatus=Verified`, assign
the same `GroundTruthEntityId` to the two observations only when the carousel
evidence proves they are the same individual, and retain a concrete image path
in `GroundTruthSource`. Unclear evidence stays `Unverifiable`. Use
`NotApplicable` only when a field genuinely does not apply.

## Analyze a labeled pack

```powershell
dotnet run --project src/PogoInventory.Cli -- `
  analyze-field-completeness `
  --ground-truth local-data/validation/ground-truth-task-k/ground-truth.csv `
  --run1 local-data/validation/reid-pilot-2x50/task-k/run1/cleanup-proof.sqlite `
  --run2 local-data/validation/reid-pilot-2x50/task-k/run2/cleanup-proof.sqlite `
  --out local-data/validation/ground-truth-task-k/report
```

The report contains overall and per-run metrics for Species, CP, all three IVs
and Nickname, plus `review-cases.csv`, JSON, Markdown and five counterfactual
gain scenarios. Accuracy excludes Unknown scanner output; completeness counts
known scanner values among manually Verified rows. No gain estimate is emitted
until identity labels are sufficiently verified.

## Reproduced baseline (2026-07-26)

Inputs:

- `local-data/validation/reid-pilot-2x50/task-k/run1/cleanup-proof.sqlite`
- `local-data/validation/reid-pilot-2x50/task-k/run2/cleanup-proof.sqlite`
- their `captured-observations.json` files;
- evidence paths carried by each captured row, including Details and Appraisal
  PNGs;
- `local-data/validation/reid-pilot-2x50/task-k/reidentification/reidentification-report.json`.

The generated pack contains 100 rows, all currently `Unverifiable`. The
diagnostic report has 15 NoMatch cases: 8 CP-not-extracted cases, 3 IV-not-
extracted cases, 2 species-not-extracted cases and 2 CP conflicts. These are
scanner-output diagnoses only; they are not ground-truth claims. The report
therefore marks same-Pokémon identity and counterfactual match safety as
Unverifiable, and all gain scenarios as not calculable.

## Safety properties

The analyzer rejects duplicate `(RunId, Ordinal)` rows, missing evidence
sources and Verified rows without an entity ID. It reads databases through the
existing persistence service and does not write to them. Scanner values are
never copied into manual ground-truth columns.
