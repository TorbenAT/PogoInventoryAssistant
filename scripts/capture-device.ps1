param(
    [string]$AdbPath = "adb",
    [string]$Serial = "",
    [int]$TimeoutSeconds = 15,
    [string]$OutputDirectory = ".\out\device"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$arguments = @(
    "run",
    "--project", ".\src\PogoInventory.Cli",
    "--",
    "device-snapshot",
    "--out", $OutputDirectory,
    "--adb", $AdbPath,
    "--timeout-seconds", $TimeoutSeconds
)

if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $arguments += @("--serial", $Serial)
}

& dotnet @arguments
