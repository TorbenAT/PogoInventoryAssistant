param(
    [string]$InputDirectory = ".\data\iphone-images",
    [string]$OutputDirectory = ".\out\iphone-region-discovery",
    [int]$MinimumImages = 20,
    [double]$MinimumDecodeRate = 0.90,
    [int]$GridColumns = 12,
    [int]$GridRows = 24
)

$ErrorActionPreference = "Stop"

dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
    image-region-discovery `
    --input $InputDirectory `
    --out $OutputDirectory `
    --min-images $MinimumImages `
    --min-decode-rate $MinimumDecodeRate `
    --grid-columns $GridColumns `
    --grid-rows $GridRows
