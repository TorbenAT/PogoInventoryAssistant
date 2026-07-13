param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "InventoryList",
        "PokemonDetails",
        "AppraisalOpen",
        "PokemonMenuOpen",
        "TagDialogOpen",
        "SearchOpen",
        "Loading",
        "Popup",
        "NetworkError",
        "Unknown")]
    [string]$State,
    [string]$Workspace = ".\local-data\screen-calibration",
    [string]$AdbPath = "adb",
    [string]$Serial = "",
    [string]$Notes = "",
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$arguments = @(
    "run",
    "--project", ".\src\PogoInventory.Cli",
    "--",
    "calibration-capture",
    "--workspace", $Workspace,
    "--state", $State,
    "--adb", $AdbPath,
    "--timeout-seconds", $TimeoutSeconds
)

if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $arguments += @("--serial", $Serial)
}

if (-not [string]::IsNullOrWhiteSpace($Notes)) {
    $arguments += @("--notes", $Notes)
}

& dotnet @arguments
