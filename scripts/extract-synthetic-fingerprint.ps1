$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

New-Item -ItemType Directory -Force -Path .\out\screen-fingerprint | Out-Null

dotnet run --project .\src\PogoInventory.Cli -- screen-fingerprint `
  --image .\data\screen-fixtures\InventoryList.png `
  --region "0.05,0.70,0.25,0.20" `
  --mode Color `
  --width 8 `
  --height 8 `
  --out .\out\screen-fingerprint\inventory-grid.json
