param(
    [string]$Output = ".\\local-data\\calcy-verification",
    [int]$Cases = 20
)
$ErrorActionPreference = "Stop"
dotnet run --project .\\src\\PogoInventory.Cli --configuration Release -- `
  calcy-verification-init --out $Output --cases $Cases
