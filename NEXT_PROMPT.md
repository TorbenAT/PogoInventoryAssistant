# Continuation prompt

Use this text for the next development task together with the latest repository and the private local calibration report. Do not upload or commit real screenshots unless they have been separately redacted and explicitly approved for public use.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth and preserve `docs/GUARDRAILS.md`, `AGENTS.md` and the read-only boundary.

Current version: 0.5.0.

Accepted before this task:

- conservative inventory decision engine
- read-only Android Device Harness
- generic Screen State Detector
- private calibration workspace
- fixture indexing and SHA-256 approval locking
- anchor-plan based profile generation
- calibration acceptance reports
- guided private screenshot capture session
- device and geometry locking
- duplicate capture detection
- explicit privacy review before fixture promotion
- synthetic end-to-end calibration in CI

First verify that the 0.5.0 GitHub Actions workflow is green and that all 47 self-tests pass.

Next milestone: M2c-b Real-screen calibration and acceptance.

Inputs expected outside Git:

- the private `capture-status.json` or Markdown report
- approved local fixture manifest
- approved redacted PNG screenshots from Torben's fixed Android configuration
- at least 3 InventoryList examples
- at least 3 PokemonDetails examples
- at least 3 AppraisalOpen examples
- menu, tag dialog, search and popup examples
- at least 1 genuine Loading example and 1 genuine NetworkError example; these may be collected last but are required before acceptance
- at least 6 Unknown negatives, including unrelated, partial, conflicting and unsupported-layout cases

Required work:

1. Inspect capture coverage and reject pixel-identical or weakly varied samples.
2. Verify every promoted fixture hash and safety review.
3. Select stable anchors that do not depend on artwork, Pokémon names, CP, account data or dynamic values.
4. Use multiple samples for every variable UI anchor.
5. Prefer several small independent anchors over full-screen fingerprints.
6. Build the local phone-specific profile.
7. Run the complete acceptance set.
8. Investigate every false positive, wrong known-state classification and weak anchor.
9. Prefer returning Unknown over weakening thresholds.
10. Produce the final private acceptance report.
11. Change source code only when a defect is proven by real calibration evidence.
12. Update README, architecture, roadmap, project state, continuation prompt and validation report for any code release.

Hard acceptance rules:

- zero false positives
- zero wrong known-state classifications
- zero weak anchors
- minimum winner margin at least 0.05
- required fixture coverage
- accepted per-state recall
- no unapproved fixture used by an anchor
- no changed file accepted without renewed review

Do not add taps, swipes, text input, tagging, transfer, gameplay, location changes, anti-detection, randomised timing or human-like input.

Do not begin the Calcy spike until the real-screen report is accepted.

---
