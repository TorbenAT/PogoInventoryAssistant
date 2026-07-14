# Continuation prompt

Use this text with the latest repository after version 0.7.0 is green.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth. Read `docs/GUARDRAILS.md`, `docs/AUTOMATIC_CORE_BOOTSTRAP.md` and `docs/CALCY_OBSERVATION_PIPELINE.md`.

Current version: 0.7.0.

Accepted before this task:

- automatic core profile bootstrap from a known InventoryList screen
- no per-image approval
- four-action input whitelist
- automatic inventory traversal and resume
- checkpoint schema 2.0
- `ICalcyObservationProvider`
- nullable structured observation fields
- fake, scripted and unavailable providers
- raw provider output hashing
- 58 self-tests

First verify that GitHub Actions is green, all 58 tests pass, the fake bootstrap is accepted, and the fake inventory scan contains three Complete observations in order: Pikachu, Machop, Eevee.

Next milestone: M4 phase 2, real Calcy IV integration.

Required work:

1. Add a diagnostic command that reports Android package presence and version for Calcy IV without changing Pokémon GO data.
2. Keep all ADB execution inside `PogoInventory.Device`.
3. Test the current installed Calcy version on the fixed phone.
4. Do not assume the old PGo-CalcaBotaBotaCalca logcat, clipboard or intent method still works.
5. Determine which output method is actually available.
6. Implement the verified method behind `ICalcyObservationProvider`.
7. Store the exact raw output and parser version.
8. Add a one-Pokémon verification command.
9. The verification must compare screenshot hash, provider output and parsed values.
10. Mark missing fields null.
11. Mark inconsistent results Conflicting.
12. Convert adapter exceptions to Failed without aborting the full evidence capture.
13. Add parser fixtures that contain no personal inventory data.
14. Add tests for supported output, malformed output, stale output, wrong-Pokémon output and provider timeout.
15. Add a provider freshness check so output from the previous Pokémon cannot be attached to the next one.
16. Update all handoff and validation documents.

Hard boundaries:

- no transfer
- no evolve, power-up, purify, TM or resource use
- no catch, spin, battle, raid or location change
- no tagging or text input yet
- no arbitrary ADB shell exposed above the device layer
- no random human imitation or detection avoidance
- screen Unknown, popup, network error or profile mismatch means stop

Acceptance criteria:

- the real provider is proven on the fixed phone
- a single Pokémon can be observed repeatedly without stale data
- complete results require species, CP and all three IV values
- partial and conflicting results remain explicit
- fake CI path remains deterministic
- all tests are green

---
