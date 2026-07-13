# Read-only Device Harness

## Purpose

The Device Harness creates a known, testable boundary between the application and an Android phone. Version 0.2.0 reads information only.

## Supported operations

- discover devices with `adb devices -l`
- select one authorised device
- read Android properties
- read screen size
- read battery information
- capture one screenshot as PNG

The interface has no tap, swipe, keyboard, app-launch or arbitrary shell operation.

## Set up Android Platform Tools

Download Android Platform Tools on the Windows computer and extract them to a stable folder, for example:

```text
C:\Android\platform-tools
```

Enable Developer options and USB debugging on the Android phone. Connect it by USB, unlock it and approve the computer's RSA prompt.

Check the connection:

```powershell
C:\Android\platform-tools\adb.exe devices -l
```

A working device line contains the state `device`. `unauthorized` means the prompt has not been approved. `offline` normally requires reconnecting the cable or restarting ADB.

## Run a fake capture first

```powershell
.\scripts\run-fake-device.ps1
```

This proves the CLI, JSON output and file handling without accessing a phone.

## Run a real capture

```powershell
.\scripts\capture-device.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

Output:

```text
out\device\screen.png
out\device\device-metadata.json
out\device\device-snapshot.json
```

## Multiple connected devices

The harness fails if more than one authorised device is connected. This is deliberate.

List serial numbers:

```powershell
C:\Android\platform-tools\adb.exe devices -l
```

Select one explicitly:

```powershell
.\scripts\capture-device.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe" `
  -Serial "SERIAL_FROM_ADB"
```

## Output validation

`device-snapshot.json` includes:

- harness and schema versions
- capture time
- device metadata
- screenshot filename
- byte length
- SHA-256 hash

A later pipeline should verify the hash before using the screenshot as evidence.

## Error handling

The CLI prints a code such as:

```text
[NoAuthorizedDevice] Exactly one authorised Android device is required...
```

It also returns a non-zero process exit code. Scripts must treat any non-zero exit as failure.

## Known limitations

- Battery temperature is not exposed consistently by every Android vendor.
- `wm size` may show both physical and override sizes. The override is treated as effective.
- The harness validates the PNG signature, not the semantic contents of the screenshot.
- No real phone was available in the assistant build environment.
