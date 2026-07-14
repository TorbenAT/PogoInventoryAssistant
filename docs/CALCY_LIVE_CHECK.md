# Automatic Calcy live check

## Goal

Prove the current Calcy integration on one Pokémon before attempting a long inventory scan.

The command starts from the known Pokémon inventory-list screen and automatically performs:

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
```

It captures exactly one item, waits for a configurable settling period and then runs the Calcy device probe.

No manual phone navigation is required.

## Command

```powershell
dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
  calcy-live-check `
  --adb "C:\Android\platform-tools\adb.exe" `
  --profile .\local-data\automation-profile.local.json `
  --screen-profile .\local-data\screen-profile.local.json `
  --settle-ms 2000 `
  --out .\local-data\calcy-live-check
```

The first real run should not supply `--parser-profile`. The raw evidence must show the actual output format before a local parser profile is created.

## Optional parser

After a real output format is proven:

```powershell
--parser-profile .\local-data\calcy-parser-profile.local.json
```

The command then writes:

```text
parsed-observation.json
```

A successful process is not enough. The parsed observation is `Complete` only when species or Pokédex number, CP, Attack IV, Defense IV and HP IV are all present and valid.

## Failure behaviour

The live check stops if:

- the starting screen is wrong
- a screen transition is not verified
- the selected device changes
- the screen geometry changes
- the screen profile or automation profile is invalid
- the first appraisal cannot be reached
- the Calcy package is missing
- a supplied parser produces conflicting or incomplete core fields

It does not continue into a long scan automatically.
