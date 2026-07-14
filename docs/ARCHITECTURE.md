# Architecture

## Target flow

```text
Android phone
    |
    | USB / ADB
    v
PogoInventory.Device
    |
    +--> discovery, metadata, battery and screenshots
    +--> allow-listed tap and swipe primitives
    |
    v
PogoInventory.Automation
    |
    +--> validated normalised control profile
    +--> screen-state checked navigation
    +--> identity-change verification
    +--> ordered evidence and checkpoint
    |
    +----------------------+
    |                      |
    v                      v
PogoInventory.Vision   Calcy / visual extraction
    |                      |
    +----------+-----------+
               v
       structured observations
               |
               v
       inventory database
               |
       +-------+--------+
       |                |
       v                v
  PvP analysis     collection rules
       |                |
       +-------+--------+
               v
      KEEP / REVIEW / DELETE plan
               |
               v
        exact-match tag executor

Final transfer remains manual.
```

## Project boundaries

### PogoInventory.Core

Owns Pokémon observations, decision policy, conservative duplicate logic and reports. It has no Android or image dependency.

### PogoInventory.Device

Owns all ADB execution.

Interfaces:

```text
IAndroidDeviceTransport
  ListDevicesAsync
  ReadMetadataAsync
  CaptureScreenshotPngAsync

IAndroidAutomationTransport
  extends IAndroidDeviceTransport
  TapAsync
  SwipeAsync
```

The input interface contains no text input, arbitrary shell, app launch, key event, location control or destructive game action.

`AdbAndroidDeviceTransport` converts the two input methods to these fixed ADB command forms:

```text
adb -s <serial> shell input tap <x> <y>
adb -s <serial> shell input swipe <x1> <y1> <x2> <y2> <duration>
```

Higher layers do not receive the ADB runner.

### PogoInventory.Vision

Owns PNG decoding, normalised regions, fingerprints and fail-closed screen-state classification.

It has no dependency on ADB or automation.

### PogoInventory.Automation

Owns automatic traversal and evidence sequencing.

Responsibilities:

- validate the automation profile
- select and lock one authorised device
- lock screen geometry
- navigate only through named actions
- verify the state after every action
- verify item change independently from screen state
- write evidence and checkpoint atomically
- resume only from a matching last item
- stop on unsafe state or health condition

It does not know Pokémon species, IVs, PvP value or deletion rules.

### PogoInventory.Calibration

Retains fixture indexing, profile generation and acceptance reporting. The earlier manual privacy-promotion route remains available as a fallback, but automatic local bootstrap becomes the target path from the next milestone.

### PogoInventory.Cli

Commands include:

```text
analyze
device-snapshot
screen-detect
screen-fingerprint
inventory-scan
calibration-*
```

### PogoInventory.SelfTest

Runs deterministic package-free tests. The scripted Android transport emulates the state path and three distinct appraisal items.

## Automatic state machine

```text
Current state       Allowed action               Required next state
-------------       --------------               -------------------
InventoryList       TapFirstInventoryCard        PokemonDetails
PokemonDetails      TapDetailsMenu               PokemonMenuOpen
PokemonMenuOpen     TapAppraise                   AppraisalOpen
AppraisalOpen       SwipeNextPokemon             AppraisalOpen + changed identity
```

No further input is sent until the required state is observed.

`Loading` may be tolerated while polling. `Unknown`, `Popup` and `NetworkError` stop the run.

## Item-change verification

The automation profile contains a normalised `IdentityRegion` and fingerprint settings.

The current and previous fingerprints are compared with the same deterministic similarity function used by the vision layer.

```text
similarity < SamePokemonSimilarityThreshold
    => next item accepted

similarity >= SamePokemonSimilarityThreshold
    => keep polling or repeat the configured swipe

no change after MaxSwipeAttemptsAtEnd
    => end of inventory
```

This is a traversal identity only. Exact identity for tagging will later include species, form, CP, IV, date, moves and neighbour context.

## Persistence

```text
<output>/
  inventory-scan-checkpoint.json
  captures/
    000001.png
    000002.png
    ...
```

The checkpoint records:

- run and profile identity
- device serial and geometry
- status and stop reason
- ordered items
- screenshot and fingerprint hashes
- complete input audit

Writes are atomic. Sequence numbers must be contiguous.

## Resume

A running checkpoint can resume only if the phone is still on `AppraisalOpen` for the last captured Pokémon and the identity fingerprint matches. The runner then swipes once and waits for a changed identity before capturing the next sequence item.

Completed and safely stopped checkpoints are immutable. A new output directory starts a new run.

## Version 0.7.0 additions

### Bootstrap layer

`PogoInventory.Bootstrap` coordinates the existing device, automation, calibration and vision layers. It may use only the existing named phone actions.

### Observation layer

`PogoInventory.Observations` owns the Calcy provider boundary and result model. Automation depends on this abstraction, not on a specific Calcy transport.

```text
Android screenshot
       |
InventoryAutomationRunner
       |
ICalcyObservationProvider
       |
CalcyObservation
       |
Checkpoint schema 2.0
```

The real adapter will be added only after the current phone and Calcy version are verified.

## Version 0.8.0: Calcy evidence boundary

```text
PogoInventory.Device
  IAndroidAppInspectionTransport
        │
        ▼
PogoInventory.CalcyProbe
  package/version parser
  evidence collection
  automatic one-item live check
        │
        ▼
PogoInventory.Observations
  ICalcyRawOutputSource
  profile-driven parser
  CalcyObservation
```

`PogoInventory.Device` is still the only assembly that executes ADB commands. The probe layer receives named text outputs and cannot issue arbitrary commands.

The live check composes the existing `InventoryAutomationRunner` with `CalcyProbeRunner`. It does not add a new phone input action.

The parser is deliberately separated from the source mechanism. A real source may later be logcat, another local text surface or visual overlay extraction. Only the mechanism proven on the fixed phone may be enabled.


## Version 0.9.0: provider verification gate

`PogoInventory.Verification` owns expected-versus-observed comparison, evidence hashing and the zero-false-Complete gate. A production provider selection is locked to the exact verification report and parser profile hashes.

## Version 0.10.1: cross-platform image pretest

```text
data/iphone-images/*.png
        │
        ▼
PogoInventory.ImagePretest
  package-free PNG decode
  geometry and orientation inventory
  SHA-256 and normalised fingerprints
  pairwise similarity and clustering
        │
        ▼
out/iphone-image-pretest/*
```

The image-pretest layer depends only on `PogoInventory.Vision`. It has no ADB, automation, Calcy or inventory-rule dependency.

The layer never modifies or copies its source screenshots. It produces metadata, hashes, similarities and cluster membership only.

An accepted iPhone pretest proves that real screenshots can pass through the visual plumbing. It does not validate Android coordinates, Android timing or Calcy extraction.

## Version 0.11.0: visual-region discovery

```text
data/iphone-images/*.png
        │
        ├── PogoInventory.ImagePretest
        │     visual clusters
        │
        ▼
PogoInventory.RegionDiscovery
  normalised grid
  luminance and edge metrics
  global and consecutive variation
  within-cluster stability
  between-cluster separation
  provisional candidate rectangles
        │
        ▼
out/iphone-region-discovery/*
```

The region layer depends on the image pretest and vision layers only. It does not depend on ADB, automation, Calcy or inventory decisions. Candidate labels describe measured visual behaviour and are not semantic Pokémon field recognition.

## Version 0.11.1: CLI namespace correction

The CLI imports the `PogoInventory.RegionDiscovery.Models` and
`PogoInventory.RegionDiscovery.Services` namespaces explicitly. This corrects
the compile failure in the command-line integration without changing the
region-discovery algorithm, reports, input boundary or safety model.

## Version 0.12.0: crop-atlas evidence layer

`PogoInventory.CropAtlas` consumes the accepted
`PogoInventory.RegionDiscovery` report and the original read-only screenshots.

It produces derived PNG crops and manifests under `out`. The project does not
perform device control and does not assign semantic Pokémon fields. Its only
decision is whether the current visual clusters have enough representative
evidence for a later semantic experiment.

## Version 0.13.0: semantic evidence review layer

The semantic evidence layer remains inside `PogoInventory.CropAtlas`. It
combines the accepted region report, crop-atlas report and read-only source
screenshots into derived per-case crops.

The output is a review package, not a provider. It has no device-control
dependency and cannot enable automated extraction. A later provider must consume
a populated truth manifest and pass the existing zero-false-Complete safety
pattern.

## Version 0.13.1: namespace correction

The semantic evidence layer reuses internal crop and JSON helpers from
`PogoInventory.CropAtlas.Services`. The nested semantic service namespace now
imports that parent service namespace explicitly. No architectural boundary
changed.



## Version 0.14.0: appraisal and phone preparation

`PogoInventory.Appraisal` contains normalised visual definitions, bar measurement, offline pretesting and read-only phone preparation. It references the Device layer for screenshot capture but exposes no tap or swipe operation. Device-adjusted profiles remain unverified until a later truth gate passes.
