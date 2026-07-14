param(
    [string]$Path = ".\data\iphone-images\IMG_7699.png"
)

$ErrorActionPreference = "Stop"
$expectedSha256 = "ef40abb395c0e17f87706731322ea492d7071b2bd9ee26c26ab97c7242551738"

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Host "The known unsupported fixture is already absent: $Path"
    exit 0
}

$actualSha256 = (
    Get-FileHash -LiteralPath $Path -Algorithm SHA256
).Hash.ToLowerInvariant()

if ($actualSha256 -ne $expectedSha256) {
    throw (
        "Refusing to delete $Path because its SHA-256 does not match " +
        "the known unsupported fixture. Expected $expectedSha256, " +
        "found $actualSha256."
    )
}

Remove-Item -LiteralPath $Path -Force
Write-Host "Removed known unsupported fixture: $Path"
