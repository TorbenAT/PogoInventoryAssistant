$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet restore .\PogoInventoryAssistant.sln
dotnet build .\PogoInventoryAssistant.sln --configuration Release --no-restore
