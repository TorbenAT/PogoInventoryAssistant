param(
    [string]$Workspace = ".\\local-data\\calcy-verification",
    [string]$Mechanism = "PidWindowedLogcat",
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$ParserProfile = ".\\local-data\\calcy-parser-profile.local.json"
)
$ErrorActionPreference = "Stop"
dotnet run --project .\\src\\PogoInventory.Cli --configuration Release -- `
  calcy-provider-select `
  --report (Join-Path $Workspace "report\\verification-report.json") `
  --mechanism $Mechanism `
  --version $Version `
  --parser-profile $ParserProfile `
  --out (Join-Path $Workspace "calcy-provider-selection.local.json")
