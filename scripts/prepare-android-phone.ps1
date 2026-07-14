param(
    [string]$Profile = ".\profiles\appraisal-normalized-v1.json",
    [string]$OutputDirectory = ".\local-data\phone-preparation",
    [string]$Serial = "",
    [string]$Adb = "adb"
)

$ErrorActionPreference = "Stop"

$arguments = @(
    "run",
    "--project", ".\src\PogoInventory.Cli",
    "--configuration", "Release",
    "--",
    "phone-prepare",
    "--profile", $Profile,
    "--out", $OutputDirectory,
    "--adb", $Adb
)

if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $arguments += @("--serial", $Serial)
}

dotnet @arguments
