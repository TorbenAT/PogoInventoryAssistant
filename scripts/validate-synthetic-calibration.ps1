$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

New-Item -ItemType Directory -Force -Path .\out\calibration\acceptance | Out-Null

if (-not (Test-Path .\out\calibration\screen-profile.generated.json)) {
    & .\scripts\build-synthetic-calibration-profile.ps1
}

dotnet run --project .\src\PogoInventory.Cli -- calibration-validate `
  --synthetic `
  --manifest .\data\calibration\fixture-manifest.synthetic.json `
  --fixtures .\data\screen-fixtures `
  --profile .\out\calibration\screen-profile.generated.json `
  --out .\out\calibration\acceptance
