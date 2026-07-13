# Continuation prompt

Copy this entire text into a new ChatGPT or coding-agent task together with the latest repository and a private local folder containing the approved redacted screenshots.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth, verify it against the code, and preserve `docs/GUARDRAILS.md` and `AGENTS.md`.

Current version: 0.3.1.

Completed:

- conservative inventory decision engine
- read-only Android Device Harness
- generic package-free PNG decoder
- deterministic normalised-region anchor framework
- required, optional and forbidden anchors
- screen profile validation
- JSON evidence reports
- synthetic fixtures and tests for all initial screen states
- CLI commands for screen detection and fingerprint extraction

The 0.3.1 hotfix resolves the PNG Paeth-filter compile error found by CI. Verify CI is green before implementing the next milestone.

Build the next isolated milestone: M2b Real-screen calibration and detector acceptance. Keep it read-only.

Inputs expected outside the public repository:

- redacted PNG screenshots from Torben's fixed Android phone resolution
- multiple examples of InventoryList, PokemonDetails and AppraisalOpen
- examples of menus, dialogs, search, loading, popup and network error where available

Required output:

1. A complete versioned repository, not only changed files.
2. A private local profile-generation workflow that never copies real screenshots into Git-tracked folders.
3. Stable anchors based on UI elements, not Pokémon artwork, names, CP values or account-specific text.
4. Multiple reference samples per anchor where the UI varies.
5. A local profile file ignored by Git.
6. A redaction and fixture-approval checklist.
7. A validation command that runs every approved real fixture and writes a confusion report.
8. Explicit false-positive tests using unrelated and partial screenshots.
9. Acceptance thresholds for each state and a global minimum winner margin.
10. A report identifying weak anchors, ambiguous states and missing coverage.
11. Tests proving that unsupported layout, orientation, dialogs and mixed states return Unknown.
12. Updated README, architecture, roadmap, project state, continuation prompt and validation report.

Do not add taps, swipes, text input, tagging, automatic transfer, gameplay, location changes, anti-detection, randomised timing or human-like input.

Do not begin the Calcy spike until the real-screen detector has zero false positive classifications in the accepted fixture set. False negatives may return Unknown and must be documented.

---
