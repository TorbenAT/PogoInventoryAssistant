# Task-K review-case ground truth

This report covers exactly the 15 NoMatch cases from the two fresh 50-item
scans. The labels were entered from the paired Details/Appraisal screenshots,
not from the scanner database or OCR output. Each pair has the same ordinal,
matching visible species/CP/IV bars and carousel evidence; the assigned entity
ID represents that evidence-based pairing. No independent Pokémon ID is
visible in the screenshots, so this remains an evidence-backed measurement,
not a claim of a cryptographic individual identifier.

## Case results

| Ordinal | Ground-truth Pokémon | CP | IV (A/D/HP) | Same Pokémon | Primary cause | Corrected extraction would match |
|---:|---|---:|---|---|---|---|
| 1 | Froakie | 204 | 12/13/0 | Yes | CpNotExtracted | Yes |
| 2 | Pikachu | 402 | 0/5/13 | Yes | IvNotExtracted | Yes |
| 14 | Illumise | 164 | 4/9/5 | Yes | SpeciesNotExtracted | Yes |
| 18 | Shellder | 802 | 14/0/12 | Yes | IvNotExtracted | Yes |
| 24 | Plusle | 545 | 8/1/8 | Yes | IvNotExtracted | Yes |
| 25 | Hoopa | 22 | 11/11/14 | Yes | CpNotExtracted | Yes |
| 27 | Karrablast | 476 | 12/10/10 | Yes | CpIncorrect | Yes |
| 30 | Eevee | 463 | 15/6/9 | Yes | CpIncorrect | Yes |
| 33 | Natu | 978 | 10/15/12 | Yes | CpNotExtracted | Yes |
| 35 | Enamorus | 240 | 11/15/13 | Yes | CpNotExtracted | Yes |
| 40 | Snover (nickname: `Snover 100`) | 16 | 15/15/15 | Yes | SpeciesNotExtracted | Yes |
| 42 | Sableye | 608 | 12/12/12 | Yes | CpNotExtracted | Yes |
| 44 | Archen | 749 | 10/12/10 | Yes | CpNotExtracted | Yes |
| 47 | Weedle | 27 | 5/13/4 | Yes | CpNotExtracted | Yes |
| 49 | Tyranitar | 2176 | 15/13/14 | Yes | CpNotExtracted | Yes |

All 15 were correctly sent to review by the current matcher. No true
identity collision and no false merge was observed.

## Counterfactual result

| Scenario | Extra automatic matches | Total re-match rate | Ambiguous | False merges |
|---|---:|---:|---:|---:|
| CP alone | 9 | 44/50 = 88% | 0 | 0 |
| IV alone | 3 | 38/50 = 76% | 0 | 0 |
| Species alone | 0 | 35/50 = 70% | 0 | 0 |
| CP and IV | 13 | 48/50 = 96% | 0 | 0 |
| All documented extractor errors | 15 | 50/50 = 100% | 0 | 0 |

The unchanged baseline is 35/50 = 70%. These are counterfactuals over the
verified labels; no matching threshold or identity rule was changed.

## Recommended next scanner change

**Recommended change:** improve CP extraction on the appraisal/details header,
retaining `Unknown` when the CP is not confidently readable.

**Documented cause:** 9 of 15 review cases are CP-related (7 missing CP and 2
incorrect CP). CP-only correction yields 9 additional matches, the largest
single-field gain: 70% → 88%.

**Risk:** no increased false-merge risk if uncertain CP remains `Unknown` and
the existing comparable-key guard remains unchanged. The two CP conflicts are
also visually resolved by the same evidence source.

**Fail-closed requirement:** never substitute a filter/query value or a low-
confidence OCR value for CP; preserve `Unknown` and route the observation to
review when the header is not readable.

The scanner change is intentionally not implemented in this checkpoint.
