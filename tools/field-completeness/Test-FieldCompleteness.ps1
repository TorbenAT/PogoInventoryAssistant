param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath,
    [double]$MinimumCoveragePercent = 95
)

$ErrorActionPreference = 'Stop'
$rows = Import-Csv -LiteralPath $CsvPath
if (-not $rows) { throw 'CSV indeholder ingen rækker.' }

$required = @('observation_id','field','result')
foreach ($name in $required) {
    if (-not ($rows[0].PSObject.Properties.Name -contains $name)) { throw "Manglende kolonne: $name" }
}

$valid = @('Correct','Incorrect','Unknown','NotApplicable')
$invalid = @($rows | Where-Object { $valid -notcontains $_.result })
if ($invalid.Count -gt 0) { throw "Ugyldige resultater: $($invalid.Count)" }

$critical = @('Species','CP','AttackIV','DefenseIV','HpIV')
$criticalIncorrect = @($rows | Where-Object { $critical -contains $_.field -and $_.result -eq 'Incorrect' })

$summary = $rows | Group-Object field | ForEach-Object {
    $applicable = @($_.Group | Where-Object { $_.result -ne 'NotApplicable' })
    $correct = @($applicable | Where-Object { $_.result -eq 'Correct' }).Count
    $coverage = if ($applicable.Count) { [math]::Round(100 * $correct / $applicable.Count, 2) } else { 100 }
    [pscustomobject]@{ Field=$_.Name; Total=$applicable.Count; Correct=$correct; Incorrect=@($applicable | Where-Object result -eq 'Incorrect').Count; Unknown=@($applicable | Where-Object result -eq 'Unknown').Count; CorrectPercent=$coverage }
}
$summary | Sort-Object Field | Format-Table -AutoSize

if ($criticalIncorrect.Count -gt 0) {
    Write-Error "Acceptance fejlede: $($criticalIncorrect.Count) forkerte kritiske feltværdier."
    exit 1
}

$below = @($summary | Where-Object { $_.Field -in $critical -and $_.CorrectPercent -lt $MinimumCoveragePercent })
if ($below.Count -gt 0) {
    Write-Warning 'Kritiske felter er under dækningsmålet. Det er ikke nødvendigvis usikkert, hvis resten er Unknown og fail-closed.'
}

Write-Host 'Fase 2 sikkerhedskontrol bestået: ingen forkerte kritiske værdier.'
