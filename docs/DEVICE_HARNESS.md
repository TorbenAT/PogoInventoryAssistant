# Android Device Harness

## Purpose

The Device Harness is the only project allowed to execute ADB commands.

Version 0.7.0 has two separate interfaces.

## Read interface

```text
IAndroidDeviceTransport
```

Supported operations:

- discover devices with `adb devices -l`
- select one authorised device
- read Android properties
- read screen size
- read battery information
- capture a screenshot as PNG

## Automation interface

```text
IAndroidAutomationTransport
```

This extends the read interface with only:

- tap at a validated pixel coordinate
- swipe between validated pixel coordinates with a bounded duration

The device layer does not expose:

- arbitrary shell commands
- text input
- key events
- app launching
- location commands
- transfer or any game-specific action

Higher layers use named actions and normalised profile points. They do not access raw ADB execution.

## Set up Android Platform Tools

Extract Android Platform Tools to a stable folder, for example:

```text
C:\Android\platform-tools
```

Enable Developer options and USB debugging on the Android phone. Connect by USB, unlock it and approve the computer.

Check the connection:

```powershell
C:\Android\platform-tools\adb.exe devices -l
```

A working line contains the state `device`.

## Fake checks

```powershell
.\scripts\run-fake-device.ps1
.\scripts\run-fake-inventory-scan.ps1
```

The second command proves automatic taps, swipes, screen-state checks, ordered captures and checkpoint output without a real phone.

## Real snapshot

```powershell
.\scripts\capture-device.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

## Real automatic scan

Use only accepted local profiles:

```powershell
.\scripts\start-local-inventory-scan.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe" `
  -AutomationProfile "C:\Path\automation-profile.local.json" `
  -ScreenProfile "C:\Path\screen-profile.local.json"
```

## Multiple devices

The harness fails when more than one authorised device is present unless a serial is selected explicitly.

## Input validation

- coordinates cannot be negative
- profile points must be within 0 to 1
- profile points are converted against locked screen geometry
- swipe duration is restricted to 50 to 5000 milliseconds
- the automation layer checks the screen state before and after actions

## Known limitations

- battery information varies by Android vendor
- `wm size` may report physical and override geometry
- the screenshot transport validates PNG structure before vision analysis
- real phone profiles are not committed
- the assistant build environment did not contain ADB or .NET for real execution
