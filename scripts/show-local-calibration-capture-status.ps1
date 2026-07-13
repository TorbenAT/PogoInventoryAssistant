param(
    [string]$Workspace = ".\local-data\screen-calibration"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

& dotnet run --project .\src\PogoInventory.Cli -- calibration-capture-status `
  --workspace $Workspace
