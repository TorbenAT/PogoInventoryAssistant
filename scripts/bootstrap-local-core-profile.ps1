param(
    [Parameter(Mandatory = $true)]
    [string]$AdbPath,

    [Parameter(Mandatory = $true)]
    [string]$AutomationProfile,

    [string]$AnchorPlan = ".\data\calibration\core-anchor-plan.template.json",
    [string]$OutputDirectory = ".\local-data\core-profile",
    [string]$Serial
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo

$args = @(
    "run",
    "--project", ".\src\PogoInventory.Cli",
    "--configuration", "Release",
    "--",
    "profile-bootstrap",
    "--adb", $AdbPath,
    "--profile", $AutomationProfile,
    "--anchors", $AnchorPlan,
    "--out", $OutputDirectory
)

if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $args += @("--serial", $Serial)
}

& dotnet @args
if ($LASTEXITCODE -ne 0) {
    throw "Core profile bootstrap failed with exit code $LASTEXITCODE."
}
