$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "out\automatic-inventory-scan"
if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

dotnet run --project "$root\src\PogoInventory.Cli" --configuration Release -- `
    inventory-scan --fake `
    --profile "$root\data\automation-profile.synthetic.json" `
    --screen-profile "$root\data\screen-profile.synthetic.json" `
    --fixtures "$root\data\screen-fixtures" `
    --max-items 3 `
    --out $out
