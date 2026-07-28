param(
    [Parameter(Mandatory=$true)][string]$Device,
    [string]$Adb = 'adb',
    [string]$Ffmpeg = 'ffmpeg',
    [string]$Server = 'scrcpy-server-v4.0',
    [string]$ServerVersion = '4.0',
    [string]$Output = 'evidence-preflight',
    [int]$MaxSize = 1920,
    [int]$Width = 0,
    [int]$Height = 0
)

$arguments = @('--device', $Device, '--adb', $Adb, '--ffmpeg', $Ffmpeg, '--server', $Server, '--server-version', $ServerVersion, '--output', $Output, '--max-size', $MaxSize.ToString())
if ($Width -gt 0 -or $Height -gt 0) { if ($Width -le 0 -or $Height -le 0) { throw 'Width and Height must be supplied together.' }; $arguments += @('--width', $Width.ToString(), '--height', $Height.ToString()) }
dotnet run --project "$PSScriptRoot\..\src\PogoInventory.Streaming.Preflight" -c Release -- @arguments
exit $LASTEXITCODE
