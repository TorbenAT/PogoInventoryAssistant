# observe-gates

`PogoInventory.Streaming.Observe.Gates` er et read-only diagnostikprogram.

## Eksempel

```powershell
dotnet run --project .\src\PogoInventory.Streaming.Observe.Gates -c Release -- `
  --device 192.168.1.185:5555 `
  --adb C:\Android\platform-tools\adb.exe `
  --server C:\scrcpy\scrcpy-server-v4.0 `
  --ffmpeg C:\ffmpeg\bin\ffmpeg.exe `
  --width 1080 `
  --height 2400 `
  --duration 30 `
  --buffer-seconds 2 `
  --max-fps 30 `
  --profile StableHeaderAndPanel `
  --output .\evidence\phase3-stable
```

Transitionprofil:

```powershell
dotnet run --project .\src\PogoInventory.Streaming.Observe.Gates -c Release -- `
  --device 192.168.1.185:5555 `
  --server C:\scrcpy\scrcpy-server-v4.0 `
  --ffmpeg C:\ffmpeg\bin\ffmpeg.exe `
  --width 1080 `
  --height 2400 `
  --duration 30 `
  --profile GenericScreenTransition `
  --output .\evidence\phase3-transition
```

Brugeren må ændre skærmen manuelt under transitiontesten. Programmet sender ingen input.

## Output

```text
gate-observation.json
gate-result.json
gate-timeline.json
frames/*.png
```

Evidence er bounded af profilens `MaximumEvidenceFrames`. Hele videostrømmen gemmes ikke.

Rapporten indeholder blandt andet:

- frames observed og rejected
- stable frames
- transition frames
- freeze events
- resolution changes
- gate transitions
- final state og reason code
- evidence frame-id'er og stier
- observationspercentiler
- parallel analyse
- frame drops
- history evictions
- outstanding leases
- `InputCommandsSent = 0`

Exit code er `0` kun ved gate-PASS, nul input og nul outstanding leases. Andre resultater returnerer `2`. Argumentfejl returnerer `64`.
