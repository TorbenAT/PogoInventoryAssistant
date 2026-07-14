# Project state

## Current version

0.6.2

## Accepted checkpoint

Torben reported that the complete 0.5.0 GitHub Actions run was green. All 47 tests and the existing synthetic calibration workflow are accepted at CI level.

Torben clarified the operational requirement:

- no manual navigation through the inventory
- no image-by-image approval
- no user involvement across 10,000+ Pokémon
- once the phone profile is adjusted, traversal must run unattended

Version 0.6.2 preserves that plan and fixes the stale harness-version assertion found after the 0.6.1 build succeeded.

GitHub Actions for 0.6.1 built successfully and ran all 52 self-tests. Exactly one test failed because it still expected the historical harness version `0.2.0`, while the manifest correctly reported `0.6.1`. Version 0.6.2 makes that assertion use `DeviceHarnessOptions.CurrentVersion` and bumps the current runtime version to `0.6.2`.

## Completed

### M0 Foundation

- .NET 8 solution
- conservative inventory rule engine
- KEEP / REVIEW / DELETE reports
- duplicate and preliminary PvP preservation

### M1 Device Harness

- authorised-device selection
- metadata, screen and battery reads
- PNG screenshots
- fake transport
- atomic evidence and structured errors

### M2 Vision and calibration foundation

- package-free PNG decoder
- normalised fingerprint anchors
- fail-closed screen states
- synthetic calibration generation and acceptance
- local fixture tooling

### M3 Automatic inventory navigation

- new `PogoInventory.Automation` project
- `IAndroidAutomationTransport`
- ADB tap and swipe implementations only
- no arbitrary shell access above the device layer
- validated normalised control points
- automatic state path:
  - InventoryList
  - PokemonDetails
  - PokemonMenuOpen
  - AppraisalOpen
- screen-state check after every action
- independent identity fingerprint per Pokémon
- changed-item verification after every swipe
- repeated-swipe end detection
- automatic local evidence capture
- screenshot and fingerprint SHA-256
- atomic checkpoint after every item
- action audit
- resume only from matching last item
- device serial, geometry and exact profile-hash lock
- battery safety checks
- deterministic scripted phone for tests
- fake full traversal in CI
- no manual image approval in the automatic scan path

## Input boundary now allowed

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

No other phone input is allowed.

## Not completed

- automatic real-phone bootstrap profile generation
- accepted real `screen-profile.local.json`
- accepted real `automation-profile.local.json`
- Calcy invocation and result adapter
- species, CP, level, HP and IV extraction
- move, date, size, nickname and status extraction
- SQLite inventory database
- exact Pokémon identity
- full PvPoke / Ohbem integration
- KEEP / REVIEW / DELETE plan based on the real complete inventory
- automatic tagging
- transfer remains manual and is not implemented

## Important limitation

Version 0.6.2 can automatically traverse and capture an ordered inventory once real local profiles are adjusted. The committed profiles are synthetic and must not be used against the real phone.

The current evidence items contain ordered screenshots and fingerprints, not yet complete Pokémon observations.

## Required checkpoint after push

1. Confirm GitHub Actions is green for 0.6.2.
2. Confirm all 52 self-tests pass.
3. Confirm the deterministic fake inventory scan captures exactly three items.
4. Confirm the checkpoint contains SHA-256 locks for both automation and screen profiles.
5. Confirm the fake run records only three taps and configured swipes.
6. Confirm `out/automatic-inventory-scan/inventory-scan-checkpoint.json` reports `Completed`.

## Next recommended milestone

M4: automatic core-profile bootstrap and Calcy observation extraction.

The next release should:

- capture core screen states automatically from one known starting screen
- build the local core detector profile without per-image approval
- introduce a Calcy adapter interface
- prove current Calcy invocation and output on the fixed Android phone
- add structured observation fields to each sequence item
- retain raw evidence and mark incomplete data Unknown rather than guessing

## Design decisions preserved

- C# and .NET 8
- no hidden game API
- no transfer automation
- no anti-detection or human imitation
- deterministic, state-aware waiting
- unknown state means stop
- unknown observation means REVIEW later
- all ADB execution remains inside `PogoInventory.Device`
- input is limited to named validated actions
- local real data stays out of the public repository
- every release updates the continuation prompt
