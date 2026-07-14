# Continuation prompt

Use this text with the latest repository after version 0.8.0 is green.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth. Read `docs/GUARDRAILS.md`, `docs/CALCY_DEVICE_PROBE.md`, `docs/CALCY_LIVE_CHECK.md` and `docs/CALCY_TEXT_PARSER.md`.

Current version: 0.8.0.

Accepted before this task:

- automatic inventory navigation with only four named actions
- automatic core screen-profile bootstrap
- checkpoint schema 2.0
- structured observation contract
- named read-only Android app-inspection interface
- Calcy package, process, accessibility, app-ops, service and log probe
- automatic one-Pokémon live check
- profile-driven raw text parser
- synthetic CI evidence only
- 68 self-tests

First verify that the 0.8.0 GitHub Actions workflow is green.

Next milestone: M4 phase 3, real-device evidence and production Calcy provider selection.

Do not assume that the old Calcy intent, clipboard, logcat or overlay behaviour still works. Use only evidence from the installed `tesmath.calcy` version on the fixed Android phone.

Required work:

1. Run the local `calcy-probe` and `calcy-live-check` commands against the fixed phone.
2. Record the installed Calcy version and the probe decision.
3. Inspect only the local evidence. Do not commit real screenshots or full logs.
4. Select one provider mechanism only when it is proven:
   - PID/time-windowed logcat if it contains structured Pokémon fields
   - another documented local text mechanism if proven
   - visual overlay extraction if no structured text mechanism exists
5. Implement the selected mechanism behind `ICalcyRawOutputSource` or `ICalcyObservationProvider`.
6. Preserve raw evidence and hashes.
7. Produce Complete only with species or Pokédex number, CP and all three IV values.
8. Treat mismatches and ambiguity as Partial, Conflicting or Failed.
9. Add a 20-Pokémon verification mode with expected-versus-observed reporting.
10. Require zero false Complete observations before a long scan can select the provider.
11. Keep all ADB execution in `PogoInventory.Device`.
12. Do not add transfer, tagging, gameplay, location changes, arbitrary shell access, random timing or anti-detection behaviour.
13. Update `PROJECT_STATE.md`, `NEXT_PROMPT.md`, `CHANGELOG.md`, README, validation report and release notes.

If the real evidence is unavailable in the development environment, implement only the evidence ingestion and verification harness. Do not fabricate a working adapter.
