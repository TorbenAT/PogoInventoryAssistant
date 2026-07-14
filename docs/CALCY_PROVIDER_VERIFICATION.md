# Calcy provider verification gate

Version 0.9.0 adds the evidence-ingestion and verification layer required before any real Calcy provider can be used in a long inventory scan.

## Why the gate exists

A parser that merely returns data is not sufficient. A wrong result marked `Complete` can cause the wrong Pokémon to be classified later. The gate therefore requires a minimum of 20 expected-versus-observed cases and zero wrong `Complete` observations.

## Outcomes

- `ExactComplete`: all required core fields match.
- `SafeIncomplete`: the provider omitted one or more fields but did not return a wrong known value.
- `IncorrectIncomplete`: an incomplete result contains a wrong known value.
- `WrongComplete`: the provider claimed Complete and at least one core field is wrong.
- `Conflicting`, `Failed`, `Unavailable` and `InvalidEvidence` preserve their literal meaning.

## Gate rules

`SafeForLongScan` requires:

- at least the configured minimum number of cases
- zero `WrongComplete`
- zero invalid or missing evidence files

`RecommendedForLongScan` additionally requires:

- the configured exact Complete rate, default 95 percent
- zero incorrect incomplete observations
- zero conflicting observations
- zero provider failures

Provider selection requires `RecommendedForLongScan` and locks the verification report and parser profile with SHA-256 hashes.

## Local workflow

```powershell
.\\scripts\\init-local-calcy-verification.ps1
.\\scripts\\run-local-calcy-verification.ps1
.\\scripts\\select-local-calcy-provider.ps1 -Version "<installed Calcy version>"
```

All real evidence remains under `local-data` and is excluded from Git.
