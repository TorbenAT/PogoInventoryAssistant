# Guardrails

## Non-negotiable restrictions

The software must not include functions for:

- transferring Pokémon
- evolving Pokémon
- powering up Pokémon
- purifying Pokémon
- using TMs
- buying anything
- spending Stardust or Candy
- changing location
- catching Pokémon
- spinning PokéStops
- raids or battles

The final transfer remains manual.

## No anti-detection behaviour

Do not add:

- random timing intended to mimic a human
- random tap positions intended to hide automation
- detection avoidance
- account-behaviour camouflage

Adaptive waiting is allowed only for correctness, such as waiting for a recognised screen state or stopping on timeout.

## Unknown is not false

Special-status fields use nullable booleans.

```text
true    = positively detected
false   = positively confirmed absent
unknown = not reliably determined
```

Unknown critical values force REVIEW.

## Exact identity before delete tagging

A future tag executor may only apply a delete tag to an Exact match. High-confidence, ambiguous and mismatched observations must never receive a delete tag.

## Action whitelist

When input control is eventually added, all actions must be named, validated operations. Arbitrary coordinates must not be exposed outside the device module.

## Auditability

Every future tag action must record:

- expected identity
- observed identity
- match result
- decision and reasons
- screenshot before and after
- action performed
- result
- timestamp
