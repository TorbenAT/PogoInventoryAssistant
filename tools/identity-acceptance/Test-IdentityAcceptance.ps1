param(
    [Parameter(Mandatory = $true)]
    [string]$CsvPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $CsvPath)) {
    throw "CSV findes ikke: $CsvPath"
}

$rows = Import-Csv -LiteralPath $CsvPath
if (-not $rows) {
    throw 'CSV indeholder ingen rækker.'
}

$required = @('source_observation_id','expected_record_id','actual_record_id','decision')
foreach ($name in $required) {
    if (-not ($rows[0].PSObject.Properties.Name -contains $name)) {
        throw "Manglende kolonne: $name"
    }
}

$falseMerges = @($rows | Where-Object {
    $_.decision -eq 'ConfirmedMatch' -and
    -not [string]::IsNullOrWhiteSpace($_.actual_record_id) -and
    $_.actual_record_id -ne $_.expected_record_id
})

$missedMatches = @($rows | Where-Object {
    $_.decision -ne 'ConfirmedMatch' -and
    -not [string]::IsNullOrWhiteSpace($_.expected_record_id)
})

$correctMatches = @($rows | Where-Object {
    $_.decision -eq 'ConfirmedMatch' -and
    $_.actual_record_id -eq $_.expected_record_id
})

$reviewed = @($rows | Where-Object { $_.decision -eq 'PossibleMatch' })
$accountedFor = $correctMatches.Count + $reviewed.Count
$coverage = if ($rows.Count -eq 0) { 0 } else { [math]::Round(100 * $accountedFor / $rows.Count, 2) }

[pscustomobject]@{
    Total = $rows.Count
    CorrectMatches = $correctMatches.Count
    PossibleMatchReview = $reviewed.Count
    MissedMatches = $missedMatches.Count
    FalseMerges = $falseMerges.Count
    AccountedForPercent = $coverage
} | Format-List

if ($falseMerges.Count -gt 0) {
    Write-Error "Acceptance fejlede: $($falseMerges.Count) false merge(s)."
    exit 1
}

if ($coverage -lt 99) {
    Write-Error "Acceptance fejlede: kun $coverage % korrekt matchet eller sendt til review."
    exit 1
}

Write-Host 'Fase 1 acceptance bestået.'
