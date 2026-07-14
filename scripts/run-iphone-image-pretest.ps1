param(
    [string]$InputDirectory = ".\data\iphone-images",
    [string]$OutputDirectory = ".\out\iphone-image-pretest",
    [int]$MinimumImages = 20
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (Test-Path $OutputDirectory) {
    Remove-Item $OutputDirectory -Recurse -Force
}

dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
  image-pretest `
  --input $InputDirectory `
  --out $OutputDirectory `
  --min-images $MinimumImages
