# Agent instructions

Read these files before changing code:

1. `PROJECT_STATE.md`
2. `docs/GUARDRAILS.md`
3. `docs/ARCHITECTURE.md`
4. `NEXT_PROMPT.md`

## Non-negotiable rules

- Do not add Pokémon transfer, evolve, power-up, purify, TM, purchase, catch, spin, battle, raid or location-changing functions.
- Do not add anti-detection behaviour, human imitation, randomised timing or randomised taps.
- Unknown data is never equivalent to `false`.
- A future delete tag requires an Exact identity match and a documented reason.
- All ADB process execution belongs inside `PogoInventory.Device`.
- Do not expose an arbitrary shell or arbitrary coordinate API to higher layers.
- The current Device Harness and Screen State Detector are read-only. Do not add taps, swipes or text input until a later explicitly approved milestone.
- Preserve the fake transport, synthetic screen fixtures and package-free self-tests.
- Every completed milestone must update `CHANGELOG.md`, `PROJECT_STATE.md`, `NEXT_PROMPT.md`, architecture and validation documentation.

## Required validation

Run:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-demo.ps1
.\scripts\run-fake-device.ps1
.\scripts\detect-synthetic-screen.ps1
.\scripts\extract-synthetic-fingerprint.ps1
```

Do not claim a build or test passed unless it was actually executed. Record limitations in `VALIDATION_REPORT.md`.

## Versioning

Use semantic versions. Each handoff ZIP must contain the complete repository at its root, not only changed files.
