# Project state

## Current version

0.7.0

## Accepted checkpoint

Torben reported that version 0.6.2 was building and testing while development continued.

Version 0.7.0 begins M4. It adds automatic core-profile bootstrap and the structured observation pipeline. The actual real-device Calcy adapter is not yet implemented and must not be claimed as working.

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
- state check after every action
- identity-change verification
- end detection
- local PNG evidence
- atomic checkpoint and resume
- exact device, geometry and profile-hash locks
- only four named input actions

### M4 phase 1

#### Automatic core profile bootstrap

- starts from a known InventoryList screen
- captures InventoryList, PokemonDetails and PokemonMenuOpen
- captures three different AppraisalOpen screens
- uses only the existing three taps and swipe
- requires each transition to change the screenshot
- creates a local fixture manifest automatically
- builds `screen-profile.local.json`
- runs calibration acceptance automatically
- rejects false positives and misclassifications
- requires no per-image approval

#### Structured observation pipeline

New `PogoInventory.Observations` project:

- `ICalcyObservationProvider`
- complete, partial, conflicting, failed and unavailable states
- nullable species, Pokédex number, form, CP, HP, level and IV fields
- nullable gender and move fields
- provider name, version and confidence
- raw provider output plus SHA-256
- validation of ranges and complete-result requirements
- provider exceptions become recorded failed observations
- fake and scripted providers for CI
- honest unavailable provider for real runs without an adapter

#### Checkpoint schema 2.0

- every inventory item contains an observation
- schema 1.0 is migrated in memory
- migrated old items are marked Unavailable
- observations are validated before checkpoint save

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

No other phone input is exposed.

## Not completed

- verified real Calcy IV integration
- real species, CP and IV extraction
- status and move extraction beyond the provider contract
- SQLite inventory database
- exact Pokémon identity across independent runs
- PvPoke and Ohbem integration
- full decision plan from the real inventory
- automatic tagging
- transfer remains manual and is not implemented

## Required checkpoint after push

1. Confirm version 0.6.2 finishes green if that run is still active.
2. Build version 0.7.0.
3. Confirm 58 of 58 self-tests pass.
4. Confirm fake core bootstrap captures six screens.
5. Confirm generated fake core profile is accepted with zero false positives and zero misclassifications.
6. Confirm fake inventory scan records three Complete observations in this order: Pikachu, Machop, Eevee.
7. Confirm the checkpoint schema is 2.0.
8. Confirm no new phone input methods exist.

## Next recommended milestone

M4 phase 2: real Calcy provider investigation and implementation.

The next release must:

- connect the fixed Android phone
- determine the installed Calcy IV version
- test current supported output mechanisms
- implement only the mechanism proven on the real phone
- preserve raw output
- produce Complete, Partial, Conflicting or Failed honestly
- add a small real-device verification command before a long scan
- refuse to label a real provider Complete unless the core fields are present and validated

## Design decisions preserved

- C# and .NET 8
- no hidden game API
- no transfer automation
- no anti-detection logic
- no random timing or random coordinates
- deterministic state-aware waits
- Unknown means stop
- unknown observation fields remain null
- all real data stays local and ignored by Git
- every release updates this file and `NEXT_PROMPT.md`
