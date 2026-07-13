$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet run --project .\src\PogoInventory.Cli -- analyze `
  --inventory .\data\sample-inventory.json `
  --policy .\data\policy.json `
  --out .\out
