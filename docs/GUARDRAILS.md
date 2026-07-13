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

## Current phone integration is read-only

Version 0.3.1 permits only:

- device discovery
- device metadata reads
- screen-size reads
- battery-state reads
- screenshot capture
- offline analysis of a PNG screenshot

The public device interface has no taps, swipes, text input, app launches or arbitrary shell commands. The vision project has no device-control dependency.

## Unknown is not false

Special-status fields use nullable booleans.

```text
true    = positively detected
false   = positively confirmed absent
unknown = not reliably determined
```

Unknown critical values force REVIEW.

## Unknown screen state is a hard stop

A later scanner or executor must not act when:

- screen state is `Unknown`
- required anchors are missing
- forbidden anchors are present
- orientation or layout is unsupported
- two states have conflicting evidence
- confidence is below threshold

False negatives are acceptable during calibration. False positive screen states are not.

## Exact identity before delete tagging

A future tag executor may only apply a delete tag to an Exact match. High-confidence, ambiguous and mismatched observations must never receive a delete tag.

## Action whitelist

When input control is eventually added, all actions must be named and validated. Arbitrary coordinates must not be exposed outside the device module.

Any input milestone requires:

- approved screen state before the action
- approved named action
- expected screen state after the action
- timeout and stop behaviour
- before and after evidence

## Fail closed

The program must stop, return Unknown or return REVIEW when:

- screen state is unknown
- a device is missing, unauthorised or ambiguous
- a command times out
- capture output is invalid
- identity is not exact
- critical data is unknown
- sequence or inventory counts do not reconcile

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

## Public repository data

Do not commit real screenshots, device serials, inventory exports, databases, logs or real screen profiles while the repository is public. Use ignored local folders and review every commit before pushing.
