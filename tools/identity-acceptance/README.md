# Identity acceptance tool

Dette værktøj analyserer en CSV med forventede og faktiske matches. Det ændrer ikke databasen.

## Input

CSV med kolonnerne:

`source_observation_id,expected_record_id,actual_record_id,decision`

Gyldige beslutninger:

- ConfirmedMatch
- PossibleMatch
- ConfirmedNonMatch

Tom `actual_record_id` er tilladt ved PossibleMatch eller ConfirmedNonMatch.

## Kørsel

```powershell
powershell -ExecutionPolicy Bypass -File tools/identity-acceptance/Test-IdentityAcceptance.ps1 -CsvPath .\local-data\identity-acceptance.csv
```

Scriptet afslutter med fejlkode 1 ved false merge eller forkert automatisk match.
