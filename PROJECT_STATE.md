# Project state

## Current version

0.9.0

## Accepted checkpoint

Torben reported that version 0.8.0 is fully green in GitHub Actions.

Version 0.9.0 implements the evidence-ingestion and verification gate for M4 phase 3. Real phone evidence is still required before a production Calcy provider can be selected.

## Completed

### M0 to M3

- .NET 8 foundation and conservative KEEP, REVIEW and DELETE engine
- read-only Android device harness
- deterministic screen detection and calibration
- automatic inventory navigation with only four named actions
- local evidence, checkpoints and safe resume

### M4 phase 1 and 2

- automatic core-profile bootstrap
- structured nullable Calcy observations
- checkpoint schema 2.0
- package, process, accessibility, app-ops, service and log probe
- automatic one-Pokémon live check
- profile-driven raw-text parser

### M4 phase 3 verification harness

New `PogoInventory.Verification` project:

- local verification workspace with 20 or more cases
- raw evidence or parsed observation ingestion
- strict evidence-root path containment
- SHA-256 hashes for every evidence file
- expected-versus-observed identity, CP and IV comparison
- `ExactComplete`, `SafeIncomplete`, `IncorrectIncomplete`, `WrongComplete`, `Conflicting`, `Failed`, `Unavailable` and `InvalidEvidence`
- separate zero-false-Complete safety gate
- configurable exact Complete rate, default 95 percent
- provider selection refused unless the long-scan gate passes
- provider selection locked to report and parser hashes
- JSON, Markdown and CSV reports
- synthetic twenty-case CI verification
- 78 self-tests

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

No new phone input action was added in 0.9.0.

## Not completed

- real-phone Calcy probe and live-check evidence
- proof of PID-windowed logcat, local text or visual overlay extraction
- production real `ICalcyObservationProvider`
- automated twenty-case evidence collection from the real phone
- move, date, size, nickname and special-status extraction
- SQLite inventory database
- exact identity across independent runs
- PvPoke and Ohbem integration
- real decision plan and automatic tagging
- transfer remains manual and is not implemented

## Required checkpoint after push

1. Build version 0.9.0.
2. Confirm 78 of 78 self-tests pass.
3. Confirm twenty synthetic cases are `ExactComplete`.
4. Confirm zero `WrongComplete` observations.
5. Confirm the synthetic provider is recommended for long scan.
6. Confirm provider selection contains verification and parser hashes.
7. Confirm all existing navigation, probe and calibration workflows remain green.
8. Confirm no new phone input action exists.

## Next recommended milestone

M4 phase 4: run the real phone probe and select the actual extraction mechanism.

Required sequence:

1. Run `calcy-probe` and `calcy-live-check` on the fixed Android phone.
2. Inspect local evidence only.
3. Choose PID-windowed logcat, another proven local text source or visual overlay extraction.
4. Implement exactly that mechanism behind the existing provider boundary.
5. Automate collection of 20 verification cases.
6. Pass the 0.9.0 verification gate with zero wrong Complete observations.
7. Only then enable the provider in a long inventory scan.

## Design decisions preserved

- no hidden game API
- no transfer automation
- no anti-detection or human imitation
- no random timing or coordinates
- deterministic state-aware waiting
- Unknown means stop
- incomplete data stays incomplete
- real data remains local and ignored by Git
- every release updates this file and `NEXT_PROMPT.md`
