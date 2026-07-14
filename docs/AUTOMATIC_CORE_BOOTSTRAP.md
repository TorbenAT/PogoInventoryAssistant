# Automatic core profile bootstrap

Version 0.7.0 can create the core local screen detector profile from one known starting screen.

## Required start

Pokémon GO must show the Pokémon inventory list in portrait orientation. The configured first-card, menu, Appraise and swipe coordinates must already match the fixed phone.

## Automatic sequence

```text
Capture InventoryList
Tap first card
Capture PokemonDetails
Tap details menu
Capture PokemonMenuOpen
Tap Appraise
Capture AppraisalOpen item 1
Swipe next
Capture AppraisalOpen item 2
Swipe next
Capture AppraisalOpen item 3
Build profile
Validate profile
```

No per-image approval is required.

## Controls

- only the four existing named actions are used
- every transition must produce a changed screenshot
- the configured target-state anchor must be stable across consecutive captures
- each PNG is decoded before it is accepted
- the device and geometry are fixed for the run
- the profile is rejected on false positives or misclassifications
- all output remains local

## Files

```text
local-data\core-profile\
  captures\
  fixture-manifest.local.json
  screen-profile.local.json
  acceptance\
```

The anchor-plan template contains normalised regions. Those regions may require one-time adjustment for the actual phone and current Pokémon GO layout.
