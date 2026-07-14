# Agent instructions

Read these files before changing code:

1. `PROJECT_STATE.md`
2. `docs/GUARDRAILS.md`
3. `docs/ARCHITECTURE.md`
4. `docs/CALCY_DEVICE_PROBE.md`
5. `docs/CALCY_LIVE_CHECK.md`
6. `NEXT_PROMPT.md`

## Non-negotiable rules

- Do not add transfer, evolve, power-up, purify, TM, purchase, catch, spin, battle, raid or location-changing functions.
- Do not add anti-detection behaviour, human imitation, randomised timing or randomised taps.
- Unknown data is never equivalent to `false`.
- A future delete tag requires Exact identity and a documented reason.
- All ADB process execution belongs inside `PogoInventory.Device`.
- Do not expose arbitrary shell execution to any higher layer.
- Version 0.10.1 permits only the named tap and swipe actions defined in `docs/GUARDRAILS.md`.
- Every input action requires a validated profile, expected state, post-action state check, timeout and audit record.
- No manual screenshot approval is required in the automatic local scan path.
- Preserve fake transports, synthetic fixtures and package-free self-tests.
- Treat `data/iphone-images` as non-destructive cross-platform fixtures. Never rewrite or delete source screenshots.
- iPhone pretest results must not be represented as proof of Android coordinates, ADB timing or Calcy output.
- Real Android screenshots, device serials, profiles, checkpoints and inventory data stay under ignored local paths. Explicitly committed iPhone pretest fixtures are the only current exception.
- Every milestone updates `CHANGELOG.md`, `PROJECT_STATE.md`, `NEXT_PROMPT.md`, architecture and validation documentation.

## Required validation

Run:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-demo.ps1
.\scripts\run-fake-device.ps1
.\scripts\run-fake-core-profile-bootstrap.ps1
.\scripts\run-fake-inventory-scan.ps1
.\scripts\run-fake-calcy-probe.ps1
.\scripts\run-fake-calcy-live-check.ps1
.\scripts\run-iphone-image-pretest.ps1
.\scripts\parse-synthetic-calcy-output.ps1
.\scripts\detect-synthetic-screen.ps1
.\scripts\build-synthetic-calibration-profile.ps1
.\scripts\validate-synthetic-calibration.ps1
```

Do not claim a build or test passed unless it was actually executed. Record limitations in `VALIDATION_REPORT.md`.

## Versioning

Use semantic versions. Every handoff ZIP contains the complete repository directly at the ZIP root.

## Observation rules

- Real Calcy access must remain behind `ICalcyObservationProvider` or `ICalcyRawOutputSource`.
- Never mark an observation Complete without species, CP and all three IV values.
- Unknown values remain null.
- Provider failures are recorded and must not be guessed away.
- The fake provider is test-only and must never be selected silently for a real phone.


## Calcy evidence rules

- The default package name is `tesmath.calcy`, but the installed version is always read from the device.
- Do not claim logcat, clipboard, intent or overlay extraction works until real local evidence proves it.
- Full logcat and screenshots stay under ignored local paths.
- A synthetic parser profile is never a real provider configuration.
- A production provider requires a 20-Pokémon verification report with zero false Complete observations.
