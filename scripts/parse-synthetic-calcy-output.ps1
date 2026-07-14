$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "out\calcy-parser\observation.json"
$directory = Split-Path -Parent $out
if (Test-Path $directory) {
    Remove-Item $directory -Recurse -Force
}

dotnet run --project "$root\src\PogoInventory.Cli" --configuration Release -- `
    calcy-parse `
    --input "$root\data\calcy-output.synthetic.txt" `
    --profile "$root\data\calcy-parser-profile.synthetic.json" `
    --source-name logcat `
    --out $out
