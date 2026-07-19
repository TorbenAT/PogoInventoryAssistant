param(
    [Parameter(Mandatory = $true)]
    [string]$AdbPath,

    [string]$Serial,
    [int]$MaximumItems = 1000,
    [double]$MaximumRuntimeHours = 7,
    [double]$MinimumFreeDiskGb = 10,
    [int]$MinimumBatteryPercent = 25,
    [string]$OutputDirectory = "",
    [switch]$Resume
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$automationProfile = Join-Path $root "local-data\automation-profile.local.json"
$screenProfile = Join-Path $root "local-data\screen-profile.local.json"
$appraisalProfile = Join-Path $root "local-data\phone-preparation\appraisal-profile.device.generated.json"
$checkpointPath = ""
$child = $null
$stdoutStream = $null
$stderrStream = $null
$stdoutCopy = $null
$stderrCopy = $null
$safetyStop = $null

function Write-AtomicJson {
    param([string]$Path, [object]$Value)
    $temporary = "$Path.tmp-$PID"
    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}

function Get-FreeDiskGb {
    param([string]$Path)
    $resolved = [System.IO.Path]::GetFullPath($Path)
    $rootPath = [System.IO.Path]::GetPathRoot($resolved)
    return [Math]::Round((New-Object System.IO.DriveInfo($rootPath)).AvailableFreeSpace / 1GB, 3)
}

function Read-JsonFile {
    param([string]$Path)
    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete)
    try {
        $reader = New-Object System.IO.StreamReader($stream)
        try {
            return $reader.ReadToEnd() | ConvertFrom-Json
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function ConvertTo-ProcessArgument {
    param([string]$Value)
    if ($Value -notmatch '[\s"]') {
        return $Value
    }
    return '"' + (($Value -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}

function Invoke-DeviceSnapshot {
    param([string]$Directory)
    $arguments = @(
        "run", "--project", (Join-Path $root "src\PogoInventory.Cli"),
        "--configuration", "Release", "--no-build", "--",
        "device-snapshot", "--adb", $AdbPath, "--out", $Directory
    )
    if (-not [string]::IsNullOrWhiteSpace($Serial)) {
        $arguments += @("--serial", $Serial)
    }
    & dotnet @arguments | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Device snapshot failed with exit code $LASTEXITCODE."
    }
    return Get-Content -LiteralPath (Join-Path $Directory "device-snapshot.json") -Raw | ConvertFrom-Json
}

function Write-Heartbeat {
    param(
        [string]$Status,
        [object]$Checkpoint,
        [object]$Device,
        [string]$StopReason
    )
    $items = @($Checkpoint.items)
    $actions = @($Checkpoint.actions)
    $last = if ($items.Count -gt 0) { $items[-1] } else { $null }
    $heartbeat = [ordered]@{
        runId = if ($null -ne $Checkpoint.runId) { $Checkpoint.runId } else { $null }
        processId = $PID
        scanProcessId = if ($null -ne $child) { $child.Id } else { $null }
        lastUpdatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
        status = $Status
        completedItems = $items.Count
        uniqueScreenshots = @($items | Select-Object -ExpandProperty screenshotSha256 -Unique).Count
        uniqueFingerprints = @($items | Select-Object -ExpandProperty identityFingerprintSha256 -Unique).Count
        successfulSwipes = @($actions | Where-Object {
            $_.kind -eq "SwipeNextPokemon" -and
            $_.stateBefore -eq "AppraisalOpen" -and
            $_.stateAfter -eq "AppraisalOpen"
        }).Count
        lastSequence = if ($null -ne $last) { $last.sequenceNumber } else { 0 }
        lastScreenshotSha256 = if ($null -ne $last) { $last.screenshotSha256 } else { $null }
        lastIdentityFingerprintSha256 = if ($null -ne $last) { $last.identityFingerprintSha256 } else { $null }
        batteryPercent = $Device.device.battery.levelPercent
        charging = [bool]($Device.device.battery.usbPowered -or $Device.device.battery.acPowered -or $Device.device.battery.wirelessPowered)
        freeDiskGb = Get-FreeDiskGb $OutputDirectory
        currentStopReason = $StopReason
    }
    Write-AtomicJson -Path (Join-Path $OutputDirectory "heartbeat.json") -Value $heartbeat
}

if ($MaximumItems -lt 1 -or $MaximumItems -gt 50000) {
    throw "MaximumItems must be between 1 and 50000."
}
if ($MaximumRuntimeHours -le 0 -or $MaximumRuntimeHours -gt 24) {
    throw "MaximumRuntimeHours must be between 0 and 24."
}
if ($MinimumFreeDiskGb -lt 0 -or $MinimumBatteryPercent -lt 0 -or $MinimumBatteryPercent -gt 100) {
    throw "Disk and battery thresholds are invalid."
}

foreach ($profile in @($automationProfile, $screenProfile, $appraisalProfile)) {
    if (-not (Test-Path -LiteralPath $profile -PathType Leaf)) {
        throw "Required local profile is missing: $profile"
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDirectory = Join-Path $root "local-data\night-scans\$stamp"
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$checkpointPath = Join-Path $OutputDirectory "inventory-scan-checkpoint.json"

if ((Test-Path -LiteralPath $checkpointPath) -and -not $Resume) {
    throw "Output contains a checkpoint but Resume was not requested."
}
if ((Test-Path -LiteralPath $checkpointPath) -and $Resume) {
    $resumeCheckpoint = Read-JsonFile $checkpointPath
    if ($resumeCheckpoint.status -ne "Running") {
        throw "Only a structurally running checkpoint can be resumed."
    }
}

$activeRoot = Join-Path $root "local-data\night-scans"
if (Test-Path -LiteralPath $activeRoot) {
    foreach ($processFile in Get-ChildItem -LiteralPath $activeRoot -Filter "night-process.json" -Recurse -File) {
        $existingProcess = Get-Content -LiteralPath $processFile.FullName -Raw | ConvertFrom-Json
        if ($existingProcess.processId -ne $PID -and
            $null -ne (Get-Process -Id $existingProcess.processId -ErrorAction SilentlyContinue)) {
            throw "Another night scan wrapper is already running with PID $($existingProcess.processId)."
        }
    }
}

$profileLock = [ordered]@{
    schemaVersion = "1.0"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    automationProfile = [ordered]@{ path = $automationProfile; sha256 = (Get-FileHash -LiteralPath $automationProfile -Algorithm SHA256).Hash.ToLowerInvariant() }
    screenProfile = [ordered]@{ path = $screenProfile; sha256 = (Get-FileHash -LiteralPath $screenProfile -Algorithm SHA256).Hash.ToLowerInvariant() }
    appraisalProfile = [ordered]@{ path = $appraisalProfile; sha256 = (Get-FileHash -LiteralPath $appraisalProfile -Algorithm SHA256).Hash.ToLowerInvariant() }
}
Write-AtomicJson -Path (Join-Path $OutputDirectory "profile-lock.json") -Value $profileLock
Write-AtomicJson -Path (Join-Path $OutputDirectory "night-process.json") -Value ([ordered]@{
    schemaVersion = "1.0"
    processId = $PID
    startedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    outputDirectory = $OutputDirectory
})

$preflightDirectory = Join-Path $OutputDirectory "preflight"
$deviceSnapshot = Invoke-DeviceSnapshot -Directory $preflightDirectory
$appraisal = Get-Content -LiteralPath $appraisalProfile -Raw | ConvertFrom-Json
if ($deviceSnapshot.device.screen.effectiveWidth -ne $appraisal.sourceScreenWidth -or
    $deviceSnapshot.device.screen.effectiveHeight -ne $appraisal.sourceScreenHeight) {
    throw "Device geometry does not match the hash-locked appraisal profile."
}
$charging = [bool]($deviceSnapshot.device.battery.usbPowered -or $deviceSnapshot.device.battery.acPowered -or $deviceSnapshot.device.battery.wirelessPowered)
if (-not $charging -and $deviceSnapshot.device.battery.levelPercent -lt $MinimumBatteryPercent) {
    throw "Battery is below threshold and the device is not charging."
}
if ((Get-FreeDiskGb $OutputDirectory) -lt $MinimumFreeDiskGb) {
    throw "Free disk space is below threshold."
}

$stopCalcyArguments = @(
    "run", "--project", (Join-Path $root "src\PogoInventory.Cli"),
    "--configuration", "Release", "--no-build", "--",
    "device-stop-known-app",
    "--adb", $AdbPath,
    "--app", "calcy"
)
if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $stopCalcyArguments += @("--serial", $Serial)
}
& dotnet @stopCalcyArguments | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Could not stop the allow-listed Calcy package."
}

$screenEvidence = Join-Path $preflightDirectory "screen-detection.json"
& dotnet run --project (Join-Path $root "src\PogoInventory.Cli") --configuration Release --no-build -- `
    screen-detect --image (Join-Path $preflightDirectory "screen.png") --profile $screenProfile --out $screenEvidence | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Screen detection failed."
}
$detected = Get-Content -LiteralPath $screenEvidence -Raw | ConvertFrom-Json
if ($detected.state -ne "AppraisalOpen") {
    throw "Current screen is not AppraisalOpen."
}

$runtimeMinutes = [Math]::Max(1, [int][Math]::Ceiling($MaximumRuntimeHours * 60))
$scanArguments = @(
    "run", "--project", (Join-Path $root "src\PogoInventory.Cli"),
    "--configuration", "Release", "--no-build", "--",
    "inventory-scan",
    "--adb", $AdbPath,
    "--profile", $automationProfile,
    "--screen-profile", $screenProfile,
    "--appraisal-profile", $appraisalProfile,
    "--observation-provider", "appraisal",
    "--max-items", $MaximumItems,
    "--max-runtime-minutes", $runtimeMinutes,
    "--out", $OutputDirectory
)
if (-not [string]::IsNullOrWhiteSpace($Serial)) {
    $scanArguments += @("--serial", $Serial)
}

$dotnetPath = (Get-Command dotnet.exe -ErrorAction Stop).Source
$processStart = New-Object System.Diagnostics.ProcessStartInfo
$processStart.FileName = $dotnetPath
$processStart.Arguments = ($scanArguments | ForEach-Object { ConvertTo-ProcessArgument ([string]$_) }) -join ' '
$processStart.WorkingDirectory = $root
$processStart.UseShellExecute = $false
$processStart.CreateNoWindow = $true
$processStart.RedirectStandardOutput = $true
$processStart.RedirectStandardError = $true
$child = [System.Diagnostics.Process]::Start($processStart)
if ($null -eq $child) {
    throw "Could not start the inventory scan process."
}
$stdoutStream = [System.IO.File]::Create((Join-Path $OutputDirectory "scan.stdout.log"))
$stderrStream = [System.IO.File]::Create((Join-Path $OutputDirectory "scan.stderr.log"))
$stdoutCopy = $child.StandardOutput.BaseStream.CopyToAsync($stdoutStream)
$stderrCopy = $child.StandardError.BaseStream.CopyToAsync($stderrStream)

$emptyCheckpoint = [pscustomobject]@{ runId = $null; items = @(); actions = @() }
Write-Heartbeat -Status "Starting" -Checkpoint $emptyCheckpoint -Device $deviceSnapshot -StopReason "none"
$lastCount = -1
$lastDeviceCheck = [DateTimeOffset]::UtcNow

try {
    while (-not $child.HasExited) {
        Start-Sleep -Seconds 2
        $child.Refresh()
        $checkpoint = if (Test-Path -LiteralPath $checkpointPath) {
            try { Read-JsonFile $checkpointPath } catch { $null }
        } else { $null }
        if ($null -ne $checkpoint -and @($checkpoint.items).Count -ne $lastCount) {
            $lastCount = @($checkpoint.items).Count
            Write-Heartbeat -Status "Running" -Checkpoint $checkpoint -Device $deviceSnapshot -StopReason "none"
        }

        foreach ($locked in @(
            [pscustomobject]@{ Path = $automationProfile; Hash = $profileLock.automationProfile.sha256 },
            [pscustomobject]@{ Path = $screenProfile; Hash = $profileLock.screenProfile.sha256 },
            [pscustomobject]@{ Path = $appraisalProfile; Hash = $profileLock.appraisalProfile.sha256 })) {
            if ((Get-FileHash -LiteralPath $locked.Path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $locked.Hash) {
                $safetyStop = "ProfileHashChanged"
                break
            }
        }
        if ($null -ne $safetyStop) { break }

        if ((Get-FreeDiskGb $OutputDirectory) -lt $MinimumFreeDiskGb) {
            $safetyStop = "DiskThreshold"
            break
        }

        if (([DateTimeOffset]::UtcNow - $lastDeviceCheck).TotalMinutes -ge 5) {
            try {
                $deviceSnapshot = Invoke-DeviceSnapshot -Directory (Join-Path $OutputDirectory "device-status")
            } catch {
                $safetyStop = "DeviceDisconnected"
                break
            }
            $lastDeviceCheck = [DateTimeOffset]::UtcNow
            $charging = [bool]($deviceSnapshot.device.battery.usbPowered -or $deviceSnapshot.device.battery.acPowered -or $deviceSnapshot.device.battery.wirelessPowered)
            if (-not $charging -and $deviceSnapshot.device.battery.levelPercent -lt $MinimumBatteryPercent) {
                $safetyStop = "BatteryThreshold"
                break
            }
            if ($null -ne $checkpoint) {
                Write-Heartbeat -Status "Running" -Checkpoint $checkpoint -Device $deviceSnapshot -StopReason "none"
            }
        }
    }

    if ($null -ne $safetyStop -and -not $child.HasExited) {
        Stop-Process -Id $child.Id -Force
        $child.WaitForExit()
    } else {
        $child.WaitForExit()
    }
    if ($null -ne $stdoutCopy) { $stdoutCopy.GetAwaiter().GetResult() }
    if ($null -ne $stderrCopy) { $stderrCopy.GetAwaiter().GetResult() }
    if ($null -ne $stdoutStream) { $stdoutStream.Flush() }
    if ($null -ne $stderrStream) { $stderrStream.Flush() }

    if (-not (Test-Path -LiteralPath $checkpointPath)) {
        throw "Scan process ended without a checkpoint."
    }
    $finalCheckpoint = Read-JsonFile $checkpointPath
    if ($finalCheckpoint.status -eq "Running") {
        Write-Heartbeat -Status "Failed" -Checkpoint $finalCheckpoint -Device $deviceSnapshot -StopReason "ScanProcessExitedWithRunningCheckpoint"
        throw "Scan process exited while its checkpoint was still Running."
    }
    if ($null -ne $safetyStop) {
        Write-Heartbeat -Status "Failed" -Checkpoint $finalCheckpoint -Device $deviceSnapshot -StopReason $safetyStop
        throw "Night scan safety stop: $safetyStop"
    }

    $exportArguments = @(
        "run", "--project", (Join-Path $root "src\PogoInventory.Cli"),
        "--configuration", "Release", "--no-build", "--",
        "real-scan-export",
        "--checkpoint", $checkpointPath,
        "--appraisal-profile", $appraisalProfile,
        "--out", $OutputDirectory,
        "--calibration-out", (Join-Path $OutputDirectory "calibration"),
        "--minimum-items", "1",
        "--requested-maximum-items", $MaximumItems,
        "--generate-overlays", "false",
        "--copy-screenshots", "false",
        "--generate-checkpoint-evidence", "false"
    )
    & dotnet @exportArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Evidence export failed with exit code $LASTEXITCODE."
    }

    $normalStop = $finalCheckpoint.stopReason -in @(
        "MaximumItemsReached", "MaximumRuntimeReached", "Cancelled", "EndOfInventoryDetected")
    $finalStatus = if ($normalStop) { "Stopped" } else { "Failed" }
    Write-Heartbeat -Status $finalStatus -Checkpoint $finalCheckpoint -Device $deviceSnapshot -StopReason $finalCheckpoint.stopReason
    if (-not $normalStop) {
        throw "Night scan stopped for safety reason: $($finalCheckpoint.stopReason)"
    }
} finally {
    if ($null -ne $child -and -not $child.HasExited -and $null -ne $safetyStop) {
        Stop-Process -Id $child.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -ne $stdoutStream) { $stdoutStream.Dispose() }
    if ($null -ne $stderrStream) { $stderrStream.Dispose() }
}
