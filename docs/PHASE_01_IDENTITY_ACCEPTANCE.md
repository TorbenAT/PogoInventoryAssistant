# Fase 1: Identitets-acceptance

## Formål

Bevis at samme individuelle Pokémon kan genfindes på tværs af to scanninger uden falske sammenlægninger.

## Stopregel

Der må ikke udvikles scrcpy-, MediaProjection- eller databaseflytning, før denne fase er målt.

## Datasæt

- Minimum 100 Pokémon.
- Samme afgrænsede inventory-segment scannes to gange.
- Medtag bevidste kollisionsgrupper: samme art, samme CP, samme IV, almindelige dubletter, shiny, shadow, costume og nickname.
- Ingen tagging, sletning eller andre ændringer under målingen.

## Obligatoriske resultater

- `ConfirmedMatch`
- `PossibleMatch`
- `ConfirmedNonMatch`
- `FalseMerge`
- `MissedMatch`

## Acceptance

- FalseMerge = 0.
- Forkert automatisk match = 0.
- Mindst 99 % af observationerne skal enten være korrekt matchet eller eksplicit sendt til review.
- Tvetydige kandidater må aldrig automatisk flettes.

## Krævet evidens pr. sammenligning

- Begge observationers id.
- Art, CP, IV og øvrige anvendte felter.
- Hard conflicts.
- Samlet score.
- Bedste og næstbedste kandidat.
- Margin mellem kandidaterne.
- Endelig beslutning og begrundelse.

## Næste kodeændring efter måling

Implementér kun den mindst mulige ændring, der fjerner den største dokumenterede fejlkategori.
