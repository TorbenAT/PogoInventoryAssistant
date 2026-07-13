# Pogo Inventory Assistant

Version 0.2.0

Pogo Inventory Assistant is being built as a conservative, local inventory and decision assistant for Pokémon GO. The final transfer remains manual.

Version 0.2.0 completes the first isolated Android milestone: a read-only Device Harness.

## What works now

### Inventory analysis from 0.1.0

- domain model for scanned Pokémon
- configurable decision policy
- conservative KEEP / REVIEW / DELETE rule engine
- duplicate grouping
- preliminary PvP preservation heuristic
- JSON and Markdown decision reports

### Read-only Android Device Harness in 0.2.0

- discovers Android devices through ADB
- requires exactly one authorised device unless `--serial` is supplied
- reads manufacturer, model, Android version, API level and build fingerprint
- reads physical and overridden screen size
- reads battery percentage, power state and temperature where Android exposes them
- captures one PNG screenshot through `adb exec-out screencap -p`
- validates the PNG signature
- writes metadata and a SHA-256 capture manifest
- supports command timeouts and cancellation
- uses structured error codes and explicit logging
- includes a fake Android transport for tests and development without a phone

The Device Harness contains no methods for taps, swipes, text input, tags or other device changes.

## Requirements

- Windows 10 or 11
- Visual Studio 2022 or .NET 8 SDK
- Android Platform Tools for a real phone capture
- USB debugging enabled on the Android phone

## First validation

From PowerShell in the repository folder:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-demo.ps1
.\scripts\run-fake-device.ps1
```

The fake-device command needs no Android phone and writes:

```text
out\fake-device\screen.png
out\fake-device\device-metadata.json
out\fake-device\device-snapshot.json
```

## Capture from a real Android phone

Connect the phone by USB, unlock it and approve the USB debugging prompt.

If `adb` is available on PATH:

```powershell
.\scripts\capture-device.ps1
```

With an explicit Platform Tools path:

```powershell
.\scripts\capture-device.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

If more than one authorised Android device is connected:

```powershell
adb devices -l

.\scripts\capture-device.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe" `
  -Serial "YOUR_DEVICE_SERIAL"
```

The capture command is equivalent to:

```powershell
dotnet run --project .\src\PogoInventory.Cli -- device-snapshot `
  --out .\out\device `
  --adb "C:\Android\platform-tools\adb.exe"
```

## Analysis demo

```powershell
.\scripts\run-demo.ps1
```

Expected sample result:

```text
KEEP: 6
REVIEW: 3
DELETE: 1
```

## Safety boundary

The repository deliberately does not contain automatic transfer or gameplay functions. Later input control must use a named action whitelist and verified screen states. Random timing or tap positions intended to disguise automation are prohibited by the project guardrails.

## Public repository warning

Do not commit real inventory exports, screenshots, device serials, SQLite databases or capture output while the repository is public. The relevant local-data folders and database extensions are ignored by `.gitignore`, but review every commit before pushing.

Read next:

- `PROJECT_STATE.md`
- `NEXT_PROMPT.md`
- `docs/DEVICE_HARNESS.md`
- `docs/GUARDRAILS.md`
- `docs/ARCHITECTURE.md`
- `VALIDATION_REPORT.md`
