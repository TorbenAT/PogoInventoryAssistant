$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "out\calcy-live-check"
if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

dotnet run --project "$root\src\PogoInventory.Cli" --configuration Release -- `
    calcy-live-check --fake `
    --profile "$root\data\automation-profile.synthetic.json" `
    --screen-profile "$root\data\screen-profile.synthetic.json" `
    --parser-profile "$root\data\calcy-parser-profile.synthetic.json" `
    --screen-fixtures "$root\data\screen-fixtures" `
    --probe-fixtures "$root\data\calcy-probe" `
    --settle-ms 0 `
    --out $out
