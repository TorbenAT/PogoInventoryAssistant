# Continuation prompt

Use this text with the latest repository after version 0.11.1 is green.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth. Read `docs/GUARDRAILS.md`, `docs/IPHONE_IMAGE_PRETEST.md`, `docs/IPHONE_VISUAL_REGION_DISCOVERY.md`, `docs/CALCY_PROVIDER_VERIFICATION.md` and `docs/AUTOMATIC_NAVIGATION.md`.

Current version: 0.11.1.

Accepted before this task:

- automatic state-aware Android inventory navigation
- structured nullable observations and checkpoint schema 2.0
- Calcy probe, text parser and twenty-case zero-wrong-Complete gate
- 24 committed iPhone screenshots
- real result of 23 decoded images, one geometry group and four visual clusters
- visual-region discovery over a 12 by 24 normalised grid
- provisional stable, state-discriminating, dynamic and text-dense candidate regions
- 91 self-tests

First verify GitHub Actions is green. Inspect these real reports from `validation-output`:

```text
out/iphone-image-pretest/iphone-image-pretest.json
out/iphone-region-discovery/iphone-region-discovery.json
out/iphone-region-discovery/iphone-region-candidates.csv
out/iphone-region-discovery/iphone-region-image-clusters.csv
```

Next milestone: build a deterministic crop-atlas and cluster-explanation report from the strongest real candidate regions.

Required work:

1. Select non-overlapping high-confidence screen-state and text-dense candidate regions.
2. Produce a manifest linking every decoded screenshot, its visual cluster and selected region fingerprints.
3. Generate local diagnostic crops or contact sheets only under ignored output directories.
4. Keep every semantic label provisional unless supported by measured evidence.
5. Determine whether the current images are sufficient for name, CP and appraisal-bar extraction experiments.
6. Request more screenshots only for a precise missing state or coverage gap.
7. Do not claim iPhone results validate Android coordinates, timing or Calcy geometry.
8. Add no new phone input action.
9. Update project state, continuation prompt, release notes and validation report.

When the fixed Android phone becomes available, run the real Calcy probe before selecting a production provider.
