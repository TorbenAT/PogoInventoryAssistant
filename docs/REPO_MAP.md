# Repository map

This file is a working map for the current repo state. It is meant to reduce duplicate work and keep the next steps obvious.

## Top-level layout

- `src/` contains the product code.
- `tests/` contains the deterministic self-tests.
- `scripts/` contains the supported entry points for builds, validation and device workflows.
- `docs/` contains the architectural and workflow notes.
- `data/` contains committed synthetic fixtures and base profiles.
- `profiles/` contains the current appraisal base profile used by phone preparation.
- `local-data/` is runtime output only and stays local.
- `tools/` is for local helper tools such as the downloaded `adb.exe`; it is not part of the product.

## Projects

The solution currently contains 15 projects:

- `PogoInventory.Core`
- `PogoInventory.Vision`
- `PogoInventory.Device`
- `PogoInventory.Automation`
- `PogoInventory.Calibration`
- `PogoInventory.Bootstrap`
- `PogoInventory.Observations`
- `PogoInventory.Verification`
- `PogoInventory.CalcyProbe`
- `PogoInventory.ImagePretest`
- `PogoInventory.RegionDiscovery`
- `PogoInventory.CropAtlas`
- `PogoInventory.Appraisal`
- `PogoInventory.Cli`
- `PogoInventory.SelfTest`

## Responsibility map

### Core

- `PogoInventory.Core` owns inventory policy, decision logic and domain reports.

### Vision

- `PogoInventory.Vision` owns PNG decoding, fingerprints, screen states and normalised geometry.

### Device

- `PogoInventory.Device` is the only project that executes ADB.
- It covers discovery, metadata, battery, screenshots, taps, swipes and named inspection reads.

### Automation

- `PogoInventory.Automation` owns the named inventory flow.
- The only allowed input actions are `TapFirstInventoryCard`, `TapDetailsMenu`, `TapAppraise`, and `SwipeNextPokemon`.
- It verifies state transitions and identity change after each swipe.

### Bootstrap

- `PogoInventory.Bootstrap` builds the core local screen profile from a known inventory list and three appraisal captures.

### Appraisal

- `PogoInventory.Appraisal` measures appraisal bars, fits the device-adjusted profile and writes the read-only phone-preparation report.
- It does not enable automatic navigation.

### Observations and verification

- `PogoInventory.Observations` defines the Calcy observation model and parser boundary.
- `PogoInventory.Verification` owns the zero-false-Complete gate and the provider selection lock.
- `PogoInventory.CalcyProbe` gathers real-device evidence for the installed Calcy package.

### Calibration and evidence packaging

- `PogoInventory.Calibration` retains the calibration workspace and acceptance pipeline.
- `PogoInventory.ImagePretest`, `PogoInventory.RegionDiscovery` and `PogoInventory.CropAtlas` cover the iPhone evidence chain.

## Main workflows

### Repository validation

Run:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
```

The current declared test count is 114.

### iPhone evidence chain

- `.\scripts\run-iphone-image-pretest.ps1`
- `.\scripts\run-iphone-region-discovery.ps1`
- `.\scripts\run-iphone-crop-atlas.ps1`
- `.\scripts\run-iphone-semantic-evidence.ps1`

### Android phone preparation

Use:

```powershell
.\scripts\prepare-android-phone.ps1 -Adb .\tools\platform-tools\adb.exe
```

This is read only. It captures one screenshot, fits the appraisal profile and writes:

- `local-data/phone-preparation/phone-readiness.json`
- `local-data/phone-preparation/phone-readiness.md`
- `local-data/phone-preparation/appraisal-profile.device.generated.json`
- `local-data/phone-preparation/appraisal-overlay.png`

### Core bootstrap and inventory run

- `.\scripts\bootstrap-local-core-profile.ps1`
- `.\scripts\start-local-inventory-scan.ps1`

These are the automated swipe-based flows. They depend on a validated automation profile and a validated screen profile.

### Calcy evidence

- `.\scripts\run-local-calcy-probe.ps1`
- `dotnet run --project .\src\PogoInventory.Cli -- calcy-live-check ...`
- `.\scripts\run-local-calcy-verification.ps1`
- `.\scripts\select-local-calcy-provider.ps1`

## Current state

- Build and self-tests are green locally.
- `phone-prepare` works on the connected OnePlus 6T at 1080x2340.
- The generated appraisal profile is still unverified.
- Calcy is not yet installed on the phone.
- Catch location/origin persistence is still a feature gap.

## Safe working rule

Do not add new phone actions unless they fit the existing allow-list and remain state-validated.
