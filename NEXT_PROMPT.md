# Continuation prompt

Copy this entire text into a new ChatGPT conversation together with the latest project ZIP.

---

I am building Pogo Inventory Assistant in C#.

Open and inspect the uploaded ZIP before changing anything. Treat `PROJECT_STATE.md` as the source of truth, but verify it against the code. Preserve all existing guardrails.

Current version: 0.1.0.

The existing package contains:

- domain models
- JSON policy
- conservative KEEP / REVIEW / DELETE rule engine
- duplicate analysis
- preliminary PvP protection heuristic
- CLI report generation
- self-tests
- architecture and project-state documents

Build the next isolated milestone: M1 Device Harness, read-only only.

Required output:

1. A new versioned ZIP containing the complete repository, not only changed files.
2. A C# device abstraction with both:
   - an ADB implementation
   - a fake/simulated implementation for tests
3. Detection of exactly one authorised device.
4. Commands for:
   - device information
   - screen size
   - battery state
   - screenshot capture
5. No taps, swipes, text input or other device mutations yet.
6. Timeouts, cancellation tokens, structured errors and clear logging.
7. Unit-style self-tests that do not require a connected phone.
8. A CLI command that captures one screenshot and writes device metadata to JSON.
9. Updated README, architecture, roadmap, `PROJECT_STATE.md` and this continuation prompt.
10. A concise validation report stating what was and was not executed in the build environment.

Do not add automatic transfer, gameplay, location changes, anti-detection, randomised timing or human-like input.

Do not move on to Calcy integration until the Device Harness is complete and independently testable.

---
