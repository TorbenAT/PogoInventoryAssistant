$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "out\calcy-probe"
if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

dotnet run --project "$root\src\PogoInventory.Cli" --configuration Release -- `
    calcy-probe --fake `
    --fixtures "$root\data\calcy-probe" `
    --out $out
