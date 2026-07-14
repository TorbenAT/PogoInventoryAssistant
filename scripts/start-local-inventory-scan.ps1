param(
    [Parameter(Mandatory = $true)]
    [string]$AdbPath,

    [Parameter(Mandatory = $true)]
    [string]$AutomationProfile,

    [Parameter(Mandatory = $true)]
    [string]$ScreenProfile,

    [string]$Serial,

    [int]$MaximumItems = 12000,

    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDirectory = Join-Path $root "local-data\inventory-scans\$stamp"
}

$arguments = @(
    "run",
    "--project", "$root\src\PogoInventory.Cli",
    "--configuration", "Release",
    "--",
    "inventory-scan",
    "--adb", $AdbPath,
    "--profile", $AutomationProfile,
    "--screen-profile", $ScreenProfile,
    "--max-items", $MaximumItems,
    "--out", $OutputDirectory
)

if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $arguments += @("--serial", $Serial)
}

& dotnet @arguments
