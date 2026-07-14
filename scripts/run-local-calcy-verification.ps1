param(
    [string]$Workspace = ".\\local-data\\calcy-verification",
    [string]$ParserProfile = ".\\local-data\\calcy-parser-profile.local.json"
)
$ErrorActionPreference = "Stop"
dotnet run --project .\\src\\PogoInventory.Cli --configuration Release -- `
  calcy-verification-run `
  --manifest (Join-Path $Workspace "verification-manifest.json") `
  --evidence-root $Workspace `
  --parser-profile $ParserProfile `
  --out (Join-Path $Workspace "report")
