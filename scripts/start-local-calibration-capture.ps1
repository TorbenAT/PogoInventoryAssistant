param(
    [string]$Workspace = ".\local-data\screen-calibration",
    [string]$AdbPath = "adb",
    [string]$Serial = "",
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$arguments = @(
    "run",
    "--project", ".\src\PogoInventory.Cli",
    "--",
    "calibration-capture-session",
    "--workspace", $Workspace,
    "--adb", $AdbPath,
    "--timeout-seconds", $TimeoutSeconds
)

if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $arguments += @("--serial", $Serial)
}

& dotnet @arguments
