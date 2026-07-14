param(
    [string]$InputDirectory = ".\data\iphone-images",
    [string]$RegionReport = ".\out\iphone-region-discovery\iphone-region-discovery.json",
    [string]$CropAtlasReport = ".\out\iphone-crop-atlas\iphone-crop-atlas.json",
    [string]$OutputDirectory = ".\out\iphone-semantic-evidence"
)

$ErrorActionPreference = "Stop"

dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
    image-semantic-evidence `
    --input $InputDirectory `
    --region-report $RegionReport `
    --crop-atlas-report $CropAtlasReport `
    --out $OutputDirectory `
    --min-cases 20 `
    --min-cases-per-cluster 2 `
    --max-crop-width 640 `
    --max-crop-height 480
