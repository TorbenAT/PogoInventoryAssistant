# Continuation prompt

Use this text with the latest repository after version 0.6.1 is green.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth. Read `docs/GUARDRAILS.md`, `docs/ARCHITECTURE.md` and `docs/AUTOMATIC_NAVIGATION.md`.

Current version: 0.6.1.

Accepted before this task:

- conservative inventory decision engine
- Android discovery, health and screenshots
- fail-closed screen-state detector
- synthetic calibration framework
- strict tap/swipe Android input interface
- automatic inventory-to-appraisal navigation
- automatic swipe-through and identity-change verification
- end-of-inventory detection
- automatic local evidence capture
- atomic checkpoint and safe resume
- deterministic fake phone and CI traversal
- 52 self-tests

First verify that the 0.6.1 GitHub Actions workflow is green and that the fake automatic scan captures exactly three items.

Next milestone: M4 automatic core-profile bootstrap and Calcy observation extraction.

User requirements:

- no per-Pokémon interaction
- no manual image approval
- no manual phone navigation during the 10,000+ item run
- one-time local phone/profile adjustment is acceptable
- after setup, the scan must run unattended over one or two nights

Required work:

1. Add an automatic bootstrap command that starts from a known InventoryList screen.
2. Use only the four already approved input actions.
3. Capture labelled InventoryList, PokemonDetails, PokemonMenuOpen and multiple AppraisalOpen examples automatically.
4. Keep all real captures local and ignored by Git, but do not require per-image privacy approval.
5. Build a local core screen profile automatically from configured stable anchor regions.
6. Validate the generated profile against the automatically captured samples and negative screens.
7. Refuse to start a long inventory scan unless the core profile passes zero-false-positive checks for the required states.
8. Introduce `ICalcyObservationProvider` behind a separate adapter boundary.
9. Investigate the current Calcy IV version on the fixed Android phone. Do not assume the old PGo-CalcaBotaBotaCalca logcat or clipboard method still works.
10. Add a fake Calcy provider for CI.
11. Attach at minimum these nullable fields to each ordered item:
    - species or Pokédex number
    - form where available
    - CP
    - HP
    - level
    - Attack IV
    - Defense IV
    - HP IV
    - gender where available
    - fast move and charged moves where available
12. Store raw provider output and confidence with every observation.
13. Unknown or conflicting data must not be guessed.
14. Add checkpoint migration from schema 1.0 to the new observation schema.
15. Add deterministic tests for complete, partial, conflicting and failed Calcy observations.
16. Update README, architecture, roadmap, project state, continuation prompt, changelog and validation report.

Hard boundaries:

- no transfer
- no evolve, power-up, purify, TM or resource use
- no catch, spin, battle, raid or location change
- no text input or tagging in this milestone
- no arbitrary ADB shell exposure
- no random timing, random taps, human imitation or detection avoidance
- screen Unknown, popup, network error or profile mismatch means stop

Acceptance criteria:

- automatic bootstrap requires no per-image approval
- fake bootstrap and fake Calcy path run in CI
- no input beyond the existing four named actions
- complete observation data is attached to all fake scan items
- partial data remains nullable and clearly reported
- checkpoints remain atomic and resumable
- all tests and CI steps are green

---
