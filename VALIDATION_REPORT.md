# Validation report

## Version

0.2.0

## Performed in the assistant build environment

- All repository JSON files were parsed successfully.
- The solution and project references were inspected for consistency.
- The Device Harness exposes only list, metadata and screenshot operations.
- The ADB implementation contains only the documented read-only commands.
- The fake PNG fixture has a valid PNG signature and valid chunk CRC values.
- C# source delimiters were checked for balanced parentheses, brackets and braces.
- PowerShell and YAML files were reviewed structurally.
- No Android device, ADB command, Pokémon GO account or external game service was accessed.

## Not performed in the assistant build environment

The environment did not contain:

- .NET SDK
- C# compiler
- ADB
- an Android phone

The solution was therefore not compiled or executed here.

## Automated validation added

GitHub Actions now performs on each push and on pull requests:

1. .NET 8 restore
2. Release build with warnings treated as errors by the projects
3. package-free self-tests
4. inventory analysis demo
5. fake device snapshot
6. upload of generated validation output

The GitHub Actions result is the first authoritative compilation result for this release.

## Required acceptance sequence

On the Windows computer:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-demo.ps1
.\scripts\run-fake-device.ps1
```

Then perform one real read-only capture:

```powershell
.\scripts\capture-device.ps1 `
  -AdbPath "C:\Android\platform-tools\adb.exe"
```

Acceptance requires:

- CI is green
- all self-tests pass
- fake output contains PNG and two JSON files
- real `screen.png` is visually correct
- metadata matches the intended phone
- no phone state was changed

Any build, test or capture failure must be fixed before M2 begins.
