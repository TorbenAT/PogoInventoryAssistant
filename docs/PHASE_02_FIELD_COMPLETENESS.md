# Fase 2: Feltkomplethed og korrekthed

## Formål

Mål præcist hvilke felter den nuværende scanner kan aflæse korrekt. Udvid kun de felter, der er nødvendige for beslutningsregler eller sikker identifikation.

## Felter

Prioritet A:

- Species
- CP
- Attack IV
- Defense IV
- HP IV
- Shiny
- Shadow/Purified
- Favorite
- Costume ja/nej
- Background ja/nej

Prioritet B:

- Køn
- Catch date
- Moves
- XXL/XXS
- Tags

Prioritet C:

- Catch location
- Eksakt costume-variant
- Eksakt background-variant
- Weight/height

## Måleformat

Hvert felt registreres som:

- Correct
- Incorrect
- Unknown
- NotApplicable

Unknown er acceptabelt. Incorrect er ikke acceptabelt for felter, der indgår i automatisk DELETE eller automatisk merge.

## Acceptance

- 0 forkerte værdier for Species, CP og IV i automatiske beslutninger.
- Ukendte værdier skal føre til Partial/Review, ikke opdigtede defaults.
- Alle extractor-resultater skal kunne spores til frame, crop og extractor-version.
