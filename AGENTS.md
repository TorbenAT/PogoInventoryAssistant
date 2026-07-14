# Agent instructions

Read these files before changing code:

1. `PROJECT_STATE.md`
2. `docs/GUARDRAILS.md`
3. `docs/ARCHITECTURE.md`
4. `NEXT_PROMPT.md`

## Non-negotiable rules

- Do not add transfer, evolve, power-up, purify, TM, purchase, catch, spin, battle, raid or location-changing functions.
- Do not add anti-detection behaviour, human imitation, randomised timing or randomised taps.
- Unknown data is never equivalent to `false`.
- A future delete tag requires Exact identity and a documented reason.
- All ADB process execution belongs inside `PogoInventory.Device`.
- Do not expose arbitrary shell execution to any higher layer.
- Version 0.6.1 permits only the named tap and swipe actions defined in `docs/GUARDRAILS.md`.
- Every input action requires a validated profile, expected state, post-action state check, timeout and audit record.
- No manual screenshot approval is required in the automatic local scan path.
- Preserve fake transports, synthetic fixtures and package-free self-tests.
- Real screenshots, device serials, profiles, checkpoints and inventory data stay under ignored local paths.
- Every milestone updates `CHANGELOG.md`, `PROJECT_STATE.md`, `NEXT_PROMPT.md`, architecture and validation documentation.

## Required validation

Run:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-demo.ps1
.\scripts\run-fake-device.ps1
.\scripts\run-fake-inventory-scan.ps1
.\scripts\detect-synthetic-screen.ps1
.\scripts\build-synthetic-calibration-profile.ps1
.\scripts\validate-synthetic-calibration.ps1
```

Do not claim a build or test passed unless it was actually executed. Record limitations in `VALIDATION_REPORT.md`.

## Versioning

Use semantic versions. Every handoff ZIP contains the complete repository directly at the ZIP root.
