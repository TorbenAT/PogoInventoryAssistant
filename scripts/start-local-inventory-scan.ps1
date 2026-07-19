param(
    [Parameter(Mandatory = $true)]
    [string]$AdbPath,

    [string]$AutomationProfile = ".\local-data\automation-profile.local.json",

    [string]$ScreenProfile = ".\local-data\screen-profile.local.json",

    [string]$AppraisalProfile = ".\local-data\phone-preparation\appraisal-profile.device.generated.json",

    [string]$Serial,

    [int]$MaximumItems = 12000,

    [int]$MaximumRuntimeMinutes = 0,

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

if ($MaximumRuntimeMinutes -gt 0) {
    $arguments += @("--max-runtime-minutes", $MaximumRuntimeMinutes)
}

if (-not [string]::IsNullOrWhiteSpace($AppraisalProfile)) {
    $arguments += @("--observation-provider", "appraisal", "--appraisal-profile", $AppraisalProfile)
}

if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $arguments += @("--serial", $Serial)
}

& dotnet @arguments
