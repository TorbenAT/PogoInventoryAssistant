# Continuation prompt

Copy this entire text into a new ChatGPT or coding-agent task together with the latest repository.

---

I am building Pogo Inventory Assistant in C# and .NET 8.

Open and inspect the repository before changing anything. Treat `PROJECT_STATE.md` as the source of truth, verify it against the code, and preserve `docs/GUARDRAILS.md` and `AGENTS.md`.

Current version: 0.2.0.

Completed:

- conservative inventory decision engine
- read-only Android Device Harness
- ADB and fake transports
- exact-one-authorised-device selection
- metadata, screen and battery capture
- validated PNG screenshot output
- structured errors, timeouts, cancellation and logs
- package-free self-tests and GitHub Actions CI

Before implementing the next milestone, fix any CI or real-device validation issue found in 0.2.0.

Build the next isolated milestone: M2 Screen State Detector, read-only only.

Required output:

1. A complete versioned repository, not only changed files.
2. A screen-state abstraction independent of ADB and file storage.
3. Initial states:
   - InventoryList
   - PokemonDetails
   - AppraisalOpen
   - PokemonMenuOpen
   - TagDialogOpen
   - Loading
   - Popup
   - NetworkError
   - Unknown
4. A deterministic image-anchor framework with:
   - named anchors
   - normalised regions rather than fixed pixels
   - confidence values
   - required and forbidden anchors
   - explicit resolution/orientation validation
5. No OCR dependency yet unless it is isolated and demonstrably necessary.
6. Recorded or synthetic screenshot fixtures that contain no personal account data.
7. Tests proving:
   - known fixtures classify correctly
   - incomplete or conflicting fixtures return Unknown
   - unsupported orientation returns Unknown or a structured error
   - confidence thresholds are deterministic
8. A CLI command that classifies one PNG file and writes a JSON evidence report.
9. Updated README, architecture, roadmap, project state, continuation prompt and validation report.
10. Preserve the Device Harness as read-only.

Do not add taps, swipes, text input, tagging, automatic transfer, gameplay, location changes, anti-detection, randomised timing or human-like input.

Do not move on to Calcy integration until screen-state classification is independently testable and accepted.

---
