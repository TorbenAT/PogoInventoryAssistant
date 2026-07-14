# Automation profile

## Purpose

The automation profile contains the only phone coordinates the runner may use.

All coordinates are normalised:

```text
0.0 = left or top edge
1.0 = right or bottom edge
```

The template intentionally contains `-1.0` for every input point. It cannot pass validation or control a phone until a fixed-device profile is deliberately created.

## Required controls

```text
firstInventoryCard
```

A safe point near the centre of the first visible Pokémon card when the inventory is sorted in the required order.

```text
detailsMenuButton
```

The menu button on the Pokémon details screen.

```text
appraiseMenuItem
```

The Appraise row in the open Pokémon menu.

```text
nextPokemonSwipe
```

A horizontal swipe within the appraisal screen that moves to the next Pokémon while keeping appraisal open.

## Identity region

`identityRegion` must contain enough Pokémon-specific visual content to change between adjacent Pokémon, while avoiding animation-heavy areas when possible.

It must not be the appraisal bars alone because those can be identical across many Pokémon.

The threshold means:

```text
similarity >= threshold  same item
similarity < threshold   changed item
```

A conservative threshold near `0.995` is used initially. It must be validated against real adjacent Pokémon and animation frames before a full run.

## Timing

Timing values are fixed and state-aware. They are not randomised.

- `statePollMilliseconds`: delay between screen checks
- `stateTimeoutSeconds`: maximum wait for a required state or changed item
- `postActionSettleMilliseconds`: small delay before polling after input
- `maxSwipeAttemptsAtEnd`: repeated verified swipes before declaring inventory end

## Safety checks before a real run

- fixed phone and display configuration
- portrait orientation
- stable Pokémon GO language and UI scale
- accepted local screen profile
- test with maximum 20 items
- test with maximum 200 items
- verify no action outside tap/tap/tap/swipe
- verify checkpoints and screenshot sequence
- only then use 12,000 as the maximum
