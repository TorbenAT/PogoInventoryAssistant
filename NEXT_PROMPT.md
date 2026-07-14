# Continuation prompt

Use this text with the latest repository after version 0.10.0 is green.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth. Read `docs/GUARDRAILS.md`, `docs/IPHONE_IMAGE_PRETEST.md`, `docs/CALCY_PROVIDER_VERIFICATION.md` and `docs/AUTOMATIC_NAVIGATION.md`.

Current version: 0.10.0.

Accepted before this task:

- automatic allow-listed Android navigation
- automatic local core-profile bootstrap
- structured nullable Calcy observations
- Calcy package and live-check probe
- twenty-case zero-false-Complete provider gate
- 24 committed iPhone screenshots under `data/iphone-images`
- deterministic iPhone image pretest with geometry, hashing, similarity and clustering
- 84 self-tests

First verify that the 0.10.0 GitHub Actions workflow is green. Inspect the generated `out/iphone-image-pretest/iphone-image-pretest.json` report and use its real geometry, duplicate and clustering results rather than assumptions.

Next milestone: build a visual-region discovery report from the committed iPhone screenshots without adding OCR guesses or semantic labels.

Required work:

1. Identify stable and changing normalised regions across consecutive images.
2. Produce per-region variance and transition heat-map data in JSON and CSV.
3. Suggest candidate regions for screen state, Pokémon identity, CP/name and appraisal content.
4. Keep suggestions explicitly unlabelled until supported by real evidence.
5. Add deterministic replay tests using the committed image set.
6. Do not claim iPhone results validate Android coordinates or Calcy behaviour.
7. Add no new phone input action.
8. Update `PROJECT_STATE.md`, `NEXT_PROMPT.md`, release notes and validation report.

When the fixed Android phone becomes available, run the real Calcy probe before implementing a production provider.
