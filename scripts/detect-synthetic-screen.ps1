$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

New-Item -ItemType Directory -Force -Path .\out\screen-detection | Out-Null

dotnet run --project .\src\PogoInventory.Cli -- screen-detect `
  --image .\data\screen-fixtures\InventoryList.png `
  --profile .\data\screen-profile.synthetic.json `
  --out .\out\screen-detection\inventory-list.json
