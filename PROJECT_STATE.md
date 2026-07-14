# Project state

## Current version

0.8.0

## Accepted checkpoint

Torben reported that version 0.7.0 is fully green in GitHub Actions.

Version 0.8.0 implements M4 phase 2 infrastructure: real-device Calcy inspection, an automatic one-Pokémon live check and a profile-driven raw-text parser. It does not claim that a production Calcy output mechanism has been proven on the real phone.

## Completed

### M0 Foundation

- .NET 8 solution
- conservative KEEP, REVIEW and DELETE engine
- duplicate and preliminary PvP protection
- JSON and Markdown reports

### M1 Device harness

- authorised-device selection
- metadata, geometry, battery and PNG screenshots
- fake device transport
- atomic evidence writes

### M2 Vision and calibration

- package-free PNG decoder
- normalised fingerprints
- deterministic screen-state detector
- fail-closed Unknown
- synthetic calibration generation and acceptance

### M3 Automatic inventory navigation

- automatic path from InventoryList to AppraisalOpen
- automatic swipe-through
- state and identity checks
- end detection
- local evidence
- atomic checkpoint and resume
- exact device, geometry and profile-hash locks
- only four named input actions

### M4 phase 1

- automatic core-profile bootstrap
- nullable structured observation model
- `ICalcyObservationProvider`
- checkpoint schema 2.0
- fake observation provider for CI

### M4 phase 2

#### Named Android app inspection

New `IAndroidAppInspectionTransport` methods:

```text
ReadPackageDumpAsync
ReadPackagePathAsync
ReadProcessIdAsync
ReadRecentLogcatAsync
ReadAccessibilityStateAsync
ReadAppOpsAsync
ReadActivityServicesAsync
```

The real ADB transport implements only fixed command shapes. The project still exposes no arbitrary shell method above the device layer.

#### Calcy probe

New `PogoInventory.CalcyProbe` project:

- defaults to package `tesmath.calcy`
- selects one authorised device
- records package metadata and installed version
- parses declared activities, services, receivers and permissions
- records current process id
- records accessibility state, app-ops and running services
- records recent full and filtered logcat
- captures the current screen
- hashes every evidence file
- writes JSON and Markdown reports
- keeps all real output local and Git-ignored

Probe decisions:

```text
PackageMissing
InstalledNeedsLiveEvidence
CandidateEvidenceFound
InspectionFailed
```

A candidate is evidence for further investigation, not proof of a working production provider.

#### Automatic one-Pokémon live check

`calcy-live-check`:

- starts from InventoryList
- navigates automatically to AppraisalOpen
- captures exactly one Pokémon
- waits for Calcy to settle
- runs the package and output probe
- optionally applies a local parser profile
- refuses to call an incomplete observation Complete

It requires no manual navigation.

#### Profile-driven parser

- JSON regex profile with named capture groups
- source-specific patterns
- regex timeout
- species, dex, form, CP, HP, level and IV fields
- gender and move fields
- Complete, Partial, Conflicting and Failed outcomes
- conflicting values remain null
- raw output and SHA-256 retained
- synthetic parser profile and output for CI only

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

No new phone input action was added in 0.8.0.

## Not completed

- real-phone Calcy probe execution
- proof that current Calcy exposes structured log output
- proof of clipboard or other text output
- visual Calcy overlay extraction
- production real `ICalcyObservationProvider`
- move, date, size, nickname and special-status extraction
- SQLite inventory database
- exact identity across independent runs
- PvPoke and Ohbem integration
- real KEEP, REVIEW and DELETE plan
- automatic tagging
- transfer remains manual and is not implemented

## Required checkpoint after push

1. Build version 0.8.0.
2. Confirm 68 of 68 self-tests pass.
3. Confirm the scripted Calcy probe reports package version 4.3.1 and two filtered log lines.
4. Confirm the scripted live check automatically reaches one appraisal and parses Pikachu as Complete.
5. Confirm the synthetic parser produces CP 501 and IV 15/14/13.
6. Confirm the existing three-item inventory scan and core bootstrap remain green.
7. Confirm no new phone input action exists.

## Next recommended milestone

M4 phase 3: execute the live check on the fixed Android phone and implement exactly one production raw-output source based on evidence.

Required sequence:

1. Run `calcy-probe` locally and capture the installed version.
2. Run `calcy-live-check` without a parser profile.
3. Inspect the local report and filtered evidence.
4. If structured log output exists, implement a PID/time-windowed log source.
5. If no structured text exists, do not force a logcat adapter. Build visual overlay extraction instead.
6. Add a local parser profile only for the exact proven output.
7. Verify at least 20 Pokémon before enabling the provider for a long scan.
8. Require zero wrong Complete observations.

## Design decisions preserved

- C# and .NET 8
- no hidden game API
- no transfer automation
- no anti-detection logic
- no random timing or coordinates
- deterministic state-aware waits
- Unknown means stop
- unknown observation values remain null
- all real data stays local and ignored by Git
- every release updates this file and `NEXT_PROMPT.md`
