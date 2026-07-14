# Calcy text parser

## Purpose

The parser converts a proven local text output into `CalcyObservation`. It is intentionally independent of the mechanism that produced the text.

Potential sources may later include:

```text
logcat
clipboard
exported text
another verified local Android surface
```

No source is considered supported until it is observed on the real phone.

## Parser profile

A profile contains named regular expressions. Every expression must include a named capture group, normally `value`.

Example:

```json
{
  "field": "Cp",
  "pattern": "(?:^|\\s)cp=(?<value>\\d+)",
  "sourceName": "logcat"
}
```

Profiles are versioned and validated before use. Regex execution has a timeout.

## Status rules

### Complete

Requires:

- species or Pokédex number
- CP
- Attack IV
- Defense IV
- HP IV

### Partial

At least one field was recognized, but the complete core set was not present.

### Conflicting

Two different values were found for the same field. The conflicting field remains null.

### Failed

No configured Pokémon field was recognized or the raw-output source failed.

## Evidence

The combined raw source is preserved in the observation and hashed with SHA-256. Missing values remain null.

The synthetic profile in `data/calcy-parser-profile.synthetic.json` is for CI only. It must never be selected for a real scan unless the real evidence exactly matches that format.
