$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet run --project .\src\PogoInventory.Cli -- device-snapshot `
  --fake `
  --out .\out\fake-device
