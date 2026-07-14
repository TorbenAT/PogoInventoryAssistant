# Continuation prompt

Use this text with the latest repository after version 0.12.0 is green.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth. Read `docs/GUARDRAILS.md`, `docs/IPHONE_IMAGE_PRETEST.md`, `docs/IPHONE_VISUAL_REGION_DISCOVERY.md`, `docs/CALCY_PROVIDER_VERIFICATION.md` and `docs/AUTOMATIC_NAVIGATION.md`.

Current version: 0.12.0.12.0.

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

Next milestone: inspect the real crop-atlas artifact and implement only the first semantic extraction experiment supported by its contact sheets.

Required work:

1. Read `out/iphone-crop-atlas/iphone-crop-atlas.json`.
2. Record `readiness.needsMoreImages` and any named underrepresented clusters.
3. Inspect the cluster overview and every selected-region contact sheet.
4. Assign provisional screen-state labels only where the evidence is visually unambiguous.
5. Select either name/CP extraction or appraisal-bar measurement as the first semantic experiment, not both unless both are strongly supported.
6. Add a truth manifest and zero-false-Complete acceptance test for that field.
7. Keep unsupported fields null and do not guess.
8. Add no phone input action.
9. Update project state, continuation prompt, release notes and validation report.

When the fixed Android phone becomes available, run the real Calcy probe before selecting a production provider.
