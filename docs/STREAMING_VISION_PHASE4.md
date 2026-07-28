# Streaming Vision Phase 4

## Status

Offline Fase 4 implementation is integrated and tested. Real-phone stream
acceptance is not claimed: the authorized OnePlus A6013 is visible to ADB, but
FFmpeg and the scrcpy server JAR are not installed in the workspace.

The read-only preflight nevertheless verified:

- serial `192.168.1.185:5555`, state `device`, model `ONEPLUS_A6013`
- ADB 1.0.41 / platform-tools 37.0.0-14910828
- physical display `1080x2340`, portrait
- automatic bounded stream resolution `886x1920` at `max-size=1920`
- `InputCommandsSent = 0`

Preflight result was `FfmpegUnavailable`; scrcpy was also
`ScrcpyServerMissing`. No stream, gate run, screenshot evidence or phone input
was produced.

## Offline acceptance

`PogoInventory.Streaming.Phase4.SelfTest` passes 7/7. It covers `wm size`
parsing, automatic aspect-ratio resolution, explicit-dimension validation,
invalid-output fail-closed behavior, required failure reason codes and the
zero-input contract. Fase 3 replay/self-test remains 15/15, including volatile
model/background behavior, stable required regions, transition rejection and
bounded frame selection.

The device candidate profile is
`profiles/pokemon-go-oneplus6t-portrait.json`. Its ROIs are normalized and
derived from the existing conservative Details/Appraisal profile; they are a
device-specific candidate, not a real-phone calibration claim until a bounded
stream provides multi-second ROI measurements.

Run the preflight with:

```powershell
.\scripts\streaming-preflight.ps1 `
  -Device 192.168.1.185:5555 `
  -Adb .\tools\platform-tools\adb.exe `
  -Server .\tools\scrcpy-server-v4.0.jar `
  -Ffmpeg ffmpeg
```

The command writes bounded `streaming-preflight.json` and
`streaming-preflight.md`. It never sends input.
