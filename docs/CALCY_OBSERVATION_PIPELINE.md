# Calcy observation pipeline

Version 0.7.0 introduces a separate adapter boundary:

```text
ICalcyObservationProvider
```

The automation runner does not know how Calcy IV exposes its result. It only sends a request containing:

- sequence number
- device serial
- capture time
- PNG bytes
- screenshot SHA-256

## Output

The provider returns a `CalcyObservation` with nullable fields and one status:

```text
Complete
Partial
Conflicting
Failed
Unavailable
```

A complete result requires species, CP and all three IV values.

## Fail-closed rules

- provider exceptions do not destroy the scan
- failures are recorded as `Failed`
- missing adapter is `Unavailable`
- conflicting values stay `Conflicting`
- partial values stay nullable
- raw provider output is hashed
- invalid ranges are rejected and recorded as provider failures

## Current providers

### FakeCalcyObservationProvider

Used only for tests and CI. It returns deterministic data for three synthetic Pokémon.

### UnavailableCalcyObservationProvider

Used when no real provider has been configured. It records the missing adapter honestly.

### ScriptedCalcyObservationProvider

Used for deterministic tests of complete, partial, conflicting and failed results.

## Next real-device work

The next release must inspect the current Calcy IV build on the fixed Android phone. It must verify which of these mechanisms, if any, still works:

- documented Android intent
- accessibility output
- notification or overlay state
- clipboard output
- logcat output

No old integration method may be assumed to work without a real-device test.
