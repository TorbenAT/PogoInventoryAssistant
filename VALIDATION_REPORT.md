# Validation report

## Version

0.3.0

## Previous accepted result

Torben reported that the 0.2.0 GitHub Actions workflow was fully green.

## Performed in the assistant build environment

- Expanded the complete 0.2.0 repository and applied the 0.3.0 changes.
- Parsed all 57 C# source files with a C# syntax parser.
- Result: zero syntax-tree errors or missing syntax nodes.
- Parsed the synthetic screen profile as JSON.
- Opened and verified all nine known-state PNG fixtures with an independent image library.
- Confirmed all known-state fixtures are 180 x 360 portrait images.
- Independently checked the synthetic profile fingerprints and fixture geometry.
- Verified that the clean InventoryList fixture has exact similarity 1.0.
- Verified that the noisy InventoryList fixture remains above the configured threshold but below 1.0.
- Verified that the Conflict fixture contains two exact required anchors.
- Reviewed solution, project references, scripts and CI structure.
- No Android device, ADB command, Pokémon GO account or external game service was accessed.

## Not performed in the assistant build environment

The environment did not contain:

- .NET SDK
- C# compiler
- ADB
- an Android phone

The 0.3.0 solution was therefore not compiled or executed here. The first authoritative compilation and runtime result is the GitHub Actions run after push.

## Automated validation added

GitHub Actions performs:

1. .NET 8 restore
2. Release build
3. package-free self-tests
4. inventory analysis demo
5. fake Android snapshot
6. synthetic InventoryList screen detection
7. synthetic anchor fingerprint extraction
8. upload of generated validation output

Self-tests cover:

- all existing inventory and Device Harness tests
- PNG decoding and dimensions
- all nine known screen states
- incomplete screen returns Unknown
- conflicting screen returns Unknown
- landscape screen fails closed
- confidence and threshold behaviour are deterministic

## Required acceptance sequence

After pushing 0.3.0:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-demo.ps1
.\scripts\run-fake-device.ps1
.\scripts\detect-synthetic-screen.ps1
.\scripts\extract-synthetic-fingerprint.ps1
```

Acceptance requires:

- GitHub Actions is green
- all self-tests pass
- synthetic screen report selects `InventoryList`
- synthetic fingerprint output is valid JSON
- no phone state is changed

## Real-screen limitation

The bundled profile is deliberately synthetic and must not be presented as a working Pokémon GO detector. Real-screen acceptance requires a private redacted fixture set and a calibrated local profile in the next milestone.
