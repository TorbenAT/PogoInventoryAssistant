$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

Remove-Item .\out\core-profile-bootstrap -Recurse -Force -ErrorAction SilentlyContinue

dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
  profile-bootstrap --fake `
  --profile .\data\automation-profile.synthetic.json `
  --anchors .\data\calibration\core-anchor-plan.synthetic.json `
  --fixtures .\data\screen-fixtures `
  --out .\out\core-profile-bootstrap
