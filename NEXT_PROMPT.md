# Continuation prompt

Use this text with the latest repository after version 0.13.0 is green.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat
`PROJECT_STATE.md` as the source of truth. Read `docs/GUARDRAILS.md`,
`docs/IPHONE_IMAGE_PRETEST.md`, `docs/IPHONE_VISUAL_REGION_DISCOVERY.md`,
`docs/IPHONE_CROP_ATLAS.md`, `docs/IPHONE_SEMANTIC_EVIDENCE.md`,
`docs/CALCY_PROVIDER_VERIFICATION.md` and `docs/AUTOMATIC_NAVIGATION.md`.

Current version: 0.13.0.

Accepted before this task:

- 24 committed iPhone screenshots
- 23 decoded screenshots
- four visual clusters
- deterministic visual-region discovery
- crop atlas with representative cluster evidence
- semantic evidence pack with one case per decoded screenshot
- intentionally empty truth template
- automated extraction disabled
- 103 self-tests

First verify that version 0.13.0 builds and all 103 self-tests pass.

Next milestone: inspect the real
`out/iphone-semantic-evidence/semantic-review-pack.zip` artifact and choose
exactly one first semantic extraction experiment supported by the derived
images.

Required work:

1. Record `readiness.needsMoreImages` and any named underrepresented clusters.
2. Inspect the cluster overview, candidate contact sheets and per-case crops.
3. Assign provisional screen-state labels only where visually unambiguous.
4. Choose either name/CP extraction or appraisal-bar measurement as the first
   semantic experiment.
5. Populate a truth manifest for at least twenty cases for the chosen field.
6. Implement a zero-false-Complete acceptance gate for that field.
7. Keep unsupported fields null and do not guess.
8. Do not enable long inventory scanning from the semantic provider until the
   verification gate passes.
9. Add no phone input action.
10. Update project state, continuation prompt, release notes and validation
    report.

When the fixed Android phone becomes available, run the real Calcy probe
before selecting a production provider.
