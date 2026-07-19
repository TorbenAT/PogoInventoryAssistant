# Automatic inventory navigation

## Purpose

The navigation engine removes all per-Pokémon user work. The user performs only the one-time phone/profile setup and starts the run.

## State flow

```text
InventoryList
    |
    | TapFirstInventoryCard
    v
PokemonDetails
    |
    | TapDetailsMenu
    v
PokemonMenuOpen
    |
    | TapAppraise
    v
AppraisalOpen
    |
    | Capture evidence and checkpoint
    | SwipeNextPokemon
    v
AppraisalOpen on next Pokémon
```

Every transition is checked with the Screen State Detector. A tap or swipe is never followed by a blind chain of further actions.

## End detection

The screen-state remains `AppraisalOpen` when swiping between Pokémon, so state detection alone cannot prove that the item changed.

The automation profile therefore defines a separate identity region. The runner extracts a deterministic fingerprint from that region before and after each swipe.

- similarity below the configured threshold means a new Pokémon is visible
- similarity above the threshold means the same Pokémon is still visible
- repeated verified swipes with no identity change mean the end of the current ordered inventory was reached

The identity fingerprint is only a navigation key. It is not yet the final exact Pokémon identity model.

The real-scan evidence export preserves that distinction. It stores a
schema-versioned `PokemonInstanceEvidence` record for the run fingerprint and a
separate `PokemonVariantIdentity` record for semantic collection identity.
Unknown semantic fields remain null and force REVIEW.

## Checkpointing

After every captured Pokémon the program atomically writes:

- screenshot file
- screenshot SHA-256
- identity fingerprint and SHA-256
- sequence number
- screen-state confidence
- complete action audit
- current run status

An interrupted run can resume only when:

- device serial matches
- screen geometry matches
- automation profile name and SHA-256 match
- screen-detection profile SHA-256 matches
- appraisal is open
- current identity fingerprint matches the last checkpoint item

The runner then swipes to the next Pokémon before continuing, so the last item is not duplicated.

## Stop conditions

The runner stops without further input on:

- `Unknown`
- popup
- network error
- unexpected known state
- state timeout
- device change
- geometry change
- resume mismatch
- low unpowered battery
- excessive battery temperature
- cancellation

## Timing

Timing is deterministic and correctness-driven:

- configured settle delay after an action
- screen-state polling until success or timeout
- no random pauses
- no random coordinates
- no behaviour intended to look human

## Current data output

Version 0.12.0 records ordered evidence and attaches a structured provider observation to every item. It also provides an automatic one-Pokémon Calcy live check, but no real provider is selected until phone evidence proves one mechanism.

## Structured observations in version 0.12.0

After each appraisal screenshot is saved, the configured `ICalcyObservationProvider` is called. Its result is attached to the same sequence item before the atomic checkpoint is written.

A provider exception is recorded as a Failed observation. It does not silently create Pokémon data.
