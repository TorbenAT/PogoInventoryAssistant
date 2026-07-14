param(
    [string]$InputDirectory = ".\data\iphone-images",
    [string]$RegionReport = ".\out\iphone-region-discovery\iphone-region-discovery.json",
    [string]$Profile = ".\profiles\appraisal-normalized-v1.json",
    [string]$OutputDirectory = ".\out\appraisal-pretest"
)

$ErrorActionPreference = "Stop"

dotnet run --project .\src\PogoInventory.Cli --configuration Release -- `
    appraisal-pretest `
    --input $InputDirectory `
    --region-report $RegionReport `
    --profile $Profile `
    --out $OutputDirectory `
    --min-images 20 `
    --min-candidates 5 `
    --min-dominant-cluster-share 0.70
