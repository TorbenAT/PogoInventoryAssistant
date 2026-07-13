# Continuation prompt

Copy this text into the next development task together with the latest repository. Real screenshots must be supplied only through a private local folder and must not be committed.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth and preserve `docs/GUARDRAILS.md`, `AGENTS.md` and the read-only boundary.

Current version: 0.4.0.

Accepted before this task:

- conservative inventory decision engine
- read-only Android Device Harness
- generic Screen State Detector
- private calibration workspace
- fixture indexing and SHA-256 approval locking
- anchor-plan based profile generation
- calibration acceptance reports
- synthetic end-to-end calibration in CI

First verify that the 0.4.0 GitHub Actions workflow is green.

Build or complete the next isolated milestone: M2c Real-screen calibration and acceptance.

Inputs expected outside Git:

- approved redacted PNG screenshots from Torben's fixed Android phone configuration
- at least 3 InventoryList examples
- at least 3 PokemonDetails examples
- at least 3 AppraisalOpen examples
- menu, tag dialog, search, popup, loading and network-error examples where available
- at least 4 Unknown negatives including unrelated, partial, conflicting and unsupported-layout cases

Required work:

1. Initialise or inspect the private calibration workspace.
2. Index fixtures and verify every approved hash.
3. Reject unreviewed or changed fixtures.
4. Select stable anchors that do not depend on artwork, Pokémon names, CP or account-specific text.
5. Use multiple samples for variable UI elements.
6. Build the local profile.
7. Run the full acceptance set.
8. Investigate every false positive, misclassification and weak anchor.
9. Prefer returning Unknown over weakening thresholds.
10. Produce the final private acceptance report.
11. Update only source or tooling defects that are proven by the real calibration run.
12. Update README, architecture, roadmap, project state, continuation prompt and validation report for any code release.

Hard acceptance rules:

- zero false positives
- zero wrong known-state classifications
- zero weak anchors
- minimum winner margin at least 0.05
- required fixture coverage
- accepted per-state recall

Do not add taps, swipes, text input, tagging, transfer, gameplay, location changes, anti-detection, randomised timing or human-like input.

Do not begin the Calcy spike until the real-screen report is accepted.

---
