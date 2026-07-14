param(
    [Parameter(Mandatory = $true)]
    [string]$AdbPath,

    [string]$Serial,

    [string]$OutputDirectory = ".\local-data\calcy-probe"
)

$ErrorActionPreference = "Stop"

$arguments = @(
    "run", "--project", ".\src\PogoInventory.Cli",
    "--configuration", "Release", "--",
    "calcy-probe",
    "--adb", $AdbPath,
    "--out", $OutputDirectory
)

if ($Serial) {
    $arguments += @("--serial", $Serial)
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Local probe complete. Keep the entire output directory local."
Write-Host "Share calcy-probe-report.md or selected redacted text, not the full logcat file, unless needed."
