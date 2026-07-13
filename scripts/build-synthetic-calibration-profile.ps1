$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

New-Item -ItemType Directory -Force -Path .\out\calibration | Out-Null

dotnet run --project .\src\PogoInventory.Cli -- calibration-build-profile `
  --synthetic `
  --manifest .\data\calibration\fixture-manifest.synthetic.json `
  --anchors .\data\calibration\anchor-plan.synthetic.json `
  --fixtures .\data\screen-fixtures `
  --out .\out\calibration\screen-profile.generated.json
