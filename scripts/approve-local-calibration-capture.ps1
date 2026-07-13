param(
    [Parameter(Mandatory = $true)]
    [string]$CaptureId,
    [string]$ReviewedBy = "Torben",
    [string]$Workspace = ".\local-data\screen-calibration"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "This command confirms that the screenshot has been reviewed for account identity, location, notifications and other personal data."
$confirmation = Read-Host "Type APPROVE to promote capture $CaptureId"
if ($confirmation -cne "APPROVE") {
    throw "Approval cancelled. Exact text APPROVE was not entered."
}

& dotnet run --project .\src\PogoInventory.Cli -- calibration-capture-approve `
  --workspace $Workspace `
  --id $CaptureId `
  --reviewed-by $ReviewedBy `
  --confirm-private-review
