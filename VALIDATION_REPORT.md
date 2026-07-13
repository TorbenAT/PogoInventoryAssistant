# Validation report

## Performed in the build environment

- All JSON files were parsed successfully.
- The repository contains the expected projects and documentation.
- C# source delimiters were checked for balanced parentheses, brackets and braces.
- The sample inventory was reviewed against the intended rule flow.
- Expected demo result:
  - KEEP: 6
  - REVIEW: 3
  - DELETE: 1
- No Android device, ADB command, game account or external service was accessed.

## Not performed

The build environment did not contain the .NET SDK, `dotnet`, `csc`, Mono or another C# compiler. The solution was therefore not compiled or executed here.

The first action on the user's computer should be:

```powershell
.\scripts\test.ps1
```

Then:

```powershell
.\scripts\run-demo.ps1
```

Any compiler or test failure must be fixed before starting M1 Device Harness.
