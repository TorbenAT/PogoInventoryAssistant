# Continuation prompt

Use this text with the latest repository after version 0.9.0 is green.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth. Read `docs/GUARDRAILS.md`, `docs/CALCY_DEVICE_PROBE.md`, `docs/CALCY_LIVE_CHECK.md`, `docs/CALCY_TEXT_PARSER.md` and `docs/CALCY_PROVIDER_VERIFICATION.md`.

Current version: 0.9.0.

Accepted before this task:

- automatic inventory navigation with only four named actions
- automatic core profile bootstrap
- structured observation and checkpoint schema 2.0
- Calcy device probe and automatic one-Pokémon live check
- profile-driven parser
- local evidence-ingestion and twenty-case verification harness
- explicit wrong Complete detection
- provider selection locked to report and parser hashes
- 78 self-tests

First verify that the 0.9.0 GitHub Actions workflow is green.

Next milestone: M4 phase 4, real provider implementation and automated twenty-case collection.

Do not assume any Calcy output mechanism works. Use only evidence from the installed `tesmath.calcy` version on the fixed Android phone.

Required work:

1. Run the real `calcy-probe` and `calcy-live-check` commands.
2. Select one mechanism only from actual local evidence: PID-windowed logcat, proven local text, or visual overlay extraction.
3. Implement exactly that source behind `ICalcyRawOutputSource` or `ICalcyObservationProvider`.
4. Add automatic collection of 20 consecutive verification cases without manual phone navigation.
5. Keep expected ground truth separate from provider output.
6. Run the 0.9.0 verification gate.
7. Require zero `WrongComplete` observations and the configured exact Complete rate.
8. Refuse long-scan provider activation unless the selection hash locks still match.
9. Preserve all raw evidence and hashes locally.
10. Add no transfer, tagging, gameplay, location changes, arbitrary shell access, random timing or anti-detection behavior.
11. Update project state, continuation prompt, changelog, README, validation report and release notes.

If real phone evidence is unavailable, do not fabricate a production provider. Improve only the local acquisition and evidence-replay harness.
