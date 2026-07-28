# observe-stream

Forudsætninger: .NET 8 SDK/runtime, `adb`, `ffmpeg`, en matching `scrcpy-server-v4.0` JAR og et eksplicit device serial.

```powershell
dotnet run --project .\src\PogoInventory.Streaming.Observe -- `
  --device 192.168.1.185:5555 `
  --adb C:\Android\platform-tools\adb.exe `
  --server C:\scrcpy\scrcpy-server-v4.0 `
  --ffmpeg C:\ffmpeg\bin\ffmpeg.exe `
  --duration 30 --buffer-seconds 2 --max-fps 30 `
  --width 1080 --height 2400 --output evidence
```

Kommandoen er bounded og sender nul input. Rapporten skrives som `stream-observation.json`. Felter, der endnu ikke kan måles præcist, er `null`, ikke estimater.
