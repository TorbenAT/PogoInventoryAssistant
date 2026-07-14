# Release 0.7.0

## Automatic core profile bootstrap

A new bootstrap runner starts from a known Pokémon inventory list and automatically captures the four core screen states using only the existing input whitelist.

It creates and validates a local screen profile without per-image approval.

## Structured Calcy observation pipeline

A new `PogoInventory.Observations` project defines the adapter contract and structured result model.

Each inventory item can now hold species, CP, HP, level, IVs, gender, moves, confidence, raw output, warnings and errors.

The fake provider is deterministic. The real provider remains unavailable until it is tested on the actual phone.

## Checkpoint schema

The schema is upgraded from 1.0 to 2.0. Old checkpoints migrate in memory and receive an Unavailable observation on earlier items.

## CI

CI now verifies:

- 58 self-tests
- fake bootstrap acceptance
- zero fake bootstrap false positives
- zero fake bootstrap misclassifications
- schema 2.0
- three Complete fake observations
- Pikachu, Machop and Eevee in order

## Safety

No transfer, tagging, text input, purchases, gameplay or location functions were added.
