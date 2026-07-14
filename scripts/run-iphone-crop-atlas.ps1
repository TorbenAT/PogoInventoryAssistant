param(
    [string]$InputDirectory = ".\data\iphone-images",
    [string]$RegionReport = ".\out\iphone-region-discovery\iphone-region-discovery.json",
    [string]$OutputDirectory = ".\out\iphone-crop-atlas"
)

$ErrorActionPreference = "Stop"

dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
    image-crop-atlas `
    --input $InputDirectory `
    --region-report $RegionReport `
    --out $OutputDirectory `
    --max-candidates 8 `
    --representatives-per-cluster 2 `
    --max-crop-width 640 `
    --max-crop-height 480
