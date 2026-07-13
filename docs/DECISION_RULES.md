# Decision rules

## Principle

A Pokémon is not marked DELETE because it looks weak. DELETE is only allowed when the system can explain why the Pokémon is redundant and document a better retained alternative.

## Decision categories

### KEEP

Strong, deterministic protection or a required retained copy.

### REVIEW

Potentially valuable, incomplete, ambiguous or not yet covered by a sufficiently strong rule.

### DELETE

Eligible duplicate with exact identity, no protection and a documented better alternative.

## Hard KEEP rules in version 0.1.0

- Perfect IV, 15/15/15
- Shiny
- Mythical
- Background or location card
- Favorite
- Catch date on or before configured old-Pokémon cutoff
- A configured Trade tag
- Nickname containing a configured Trade fragment such as `Trade distan`

## REVIEW-protected rules in version 0.1.0

- Legendary
- Ultra Beast
- Shadow
- Purified
- Lucky
- Costume
- Dynamax
- Gigantamax
- Special or legacy move
- XXL
- XXS
- Any critical unknown value
- Identity confidence below Exact

## Living-dex preservation

For ordinary unprotected Pokémon grouped by species, form and costume, the policy preserves at least the configured minimum number of copies.

The first retained ordinary copy is selected by:

1. total IV
2. CP
3. earliest sequence number as deterministic tie-breaker

This is a temporary general-purpose rule. Later versions will add species metadata, raid relevance and evolution-family planning.

## Preliminary PvP preservation

Version 0.1.0 does not claim to calculate true PvP value.

It protects the best preliminary candidate in a duplicate group when:

- Attack IV is at or below the configured maximum
- Defense IV is at or above the configured minimum
- HP IV is at or above the configured minimum

The candidate is marked REVIEW, not KEEP, because species relevance, league caps, level, evolution and movesets are not yet evaluated.

## DELETE requirements

All of the following must be true:

- no hard KEEP rule applies
- no REVIEW protection applies
- identity confidence is Exact
- the ordinary minimum-copy quota is already satisfied
- the Pokémon is not the selected preliminary PvP candidate
- a strictly better ordinary duplicate exists
- critical values are known

Equal-quality duplicates are REVIEW in this version. This is intentional because visually identical duplicates can be difficult to distinguish during later execution.
