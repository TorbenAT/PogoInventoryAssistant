# Release 0.2.0

This release completes M1, the read-only Android Device Harness.

## Main result

The project can now safely connect to exactly one authorised Android device through ADB, read basic metadata and capture one validated screenshot. It does not contain phone input or Pokémon GO actions.

## Validate after pushing

1. Open the repository's **Actions** tab and confirm the `CI` workflow is green.
2. On Windows, run:

```powershell
.\scripts\run-fake-device.ps1
```

3. Install Android Platform Tools and run:

```powershell
.\scripts\capture-device.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

4. Confirm that `out\device\screen.png` is correct and that the JSON metadata matches the phone.

## Next milestone

M2 is a read-only Screen State Detector. No taps or swipes are introduced yet.
