# Architecture

## Action authorization and unsafe confirmation interlock

`MainMenuPreconditionValidator` is the typed boundary for the MainMenu to
Inventory transition. It accepts only three stable observations whose strict
state is MainMenu and whose MainMenu and Inventory topology are positive.
Details, PokemonMenu, Appraisal, visual Details fallback, unsafe modal
evidence and any conflicting state invalidate the precondition. The host then
captures and validates a fresh frame immediately before the named tap; the
precondition and fresh screenshot hashes are retained in the audit record.

`UnsafeConfirmationSurfaceDetector` recognizes the paired-adjuster and large
confirmation-panel topology of the observed Power Up dialog and conservatively
blocks uncertain confirmation surfaces for Evolve, Transfer, Purify and
purchase/item actions. `AndroidVerifiedInventoryNamedOperations` applies the
interlock before every named tap, search text/submit, Back and cursor swipe.
Unsafe evidence is saved and audited with `InputSent: false`; no automatic
Cancel operation exists. Normal Details action buttons are not modal evidence.

## Verified inventory task sequence

`VerifiedInventoryTaskSequence` is the single sequential orchestration
boundary. Its host supplies only named operations for Inventory, Details,
Appraisal, tag observation/application and cursor advancement. The sequence
does not construct ADB commands, run parallel navigation, or expose delete.
The first card is opened once; normal progression uses one allow-listed swipe
from stable Details to stable Details and rejects an unchanged identity.
Every completed item is atomically checkpointed with query, ordinal instance
ID, cursor fingerprints, evidence hashes, appraisal and structured tag
observation. Partial states are preserved and may advance directly while
Details remains verified. Resume replays only verified cursor steps and requires
an identity overlap match before any new swipe. Unknown, no-effect and failed
recovery states are controlled-stopped.

`AndroidVerifiedInventoryNamedOperations` is the concrete real-device host.
It uses `IAndroidAutomationTransport`, `PokemonGoGameStateDetector`,
`GuardedInventorySearch`, `GuardedInventoryRecovery`, `VisualControlLocator`
and `PokemonDetailsIdentityAnalyzer`; raw ADB construction remains inside
`PogoInventory.Device`. `device-run-index-sequence` is bounded and read-only
by default. Tag mutation is intentionally disabled for the first acceptance.

## Dynamic Details identity

`PokemonDetailsIdentityAnalyzer` is the Details identity boundary. It hashes
the complete PNG only as evidence integrity, then builds a separate stable
fingerprint from multiple model-independent ROIs. It dynamically records tag
section bounds and aligns lower content to a detected visual anchor. At least
three compatible usable frames are required for Complete; the canonical
consensus fingerprint is a deterministic bytewise median over compatible frame
fingerprints. Mutable tag state is not included in identity.
`PokemonIdentityInstance` uses `ScanRunId` plus ordinal and never uses a
screenshot hash as the instance key.

## Target flow

### Shared game-state detection

`PogoInventory.Exploration.PokemonGoGameStateDetector` is the single read-only
detector for the current game screen. It reuses `VisualControlLocator` for
Inventory, Details and Menu anchors and `AppraisalAnalyzer` for Appraisal.
Detection returns a normalized state, confidence, concrete evidence and the
SHA-256 of the captured screenshot. No UI hierarchy is used as sole evidence.

`GuardedInventoryRecovery` owns recovery stability and transition policy.
AppraisalIntro stability uses only the dialog and overlay-anchor ROIs;
AppraisalBars stability uses the three transformed IV-bar ROIs plus the fixed
label/frame ROI. Three compatible frames among the latest five form consensus,
while Unknown or conflicting evidence invalidates the active window. Animated
Pokémon models, particles and the central background are excluded.

`device-recover-inventory` only orchestrates captures, consensus calls,
service decisions, audited named actions and post-action polling. The service
owns Unknown-stop, unexpected-state-stop and action limits. An unchanged
post-action substate yields terminal `ACTION_NOT_OBSERVED`; no blind retry is
authorized. AppraisalIntro and AppraisalBars each authorize one normalized
`ExitAppraisal` tap at the documented left-middle target. Only verified
PokemonDetails authorizes Android Back to Inventory.

`device-continue-appraisal-intro` returns success without input when stable
bars already exist. Otherwise it requires stable intro ROI evidence, taps the
locator target exactly once and requires stable bars afterward.

### Guarded Inventory Search

`GuardedInventorySearch` owns the bounded OpenSearch, ClearSearch, EnterQuery
and SubmitQuery sequence. `InventorySearchVisualAnalyzer` verifies the search
surface, keyboard, query ink, clear control and a stable result-region
signature. An unobserved action terminates the workflow and cannot loop.

Ordinary search text crosses `IAndroidAutomationTransport` into
`PogoInventory.Device`. `AndroidInputTextEncoder` alone translates it to the
remote-shell-safe token used by Android `input text`; Submit is a separate
named `KEYCODE_ENTER` transport operation. CLI and Automation never construct
raw shell syntax.

### Guarded tag selection by name

`TagSelector` first discovers visible rows from their left-side marker geometry.
It then compares each row's name region with a named template in an ignored
device profile at bounded 0.94, 1.00 and 1.06 normalized height scales. A match
requires both an absolute confidence threshold and a second-best margin. Row
order and fixed row coordinates are never match inputs.

The CLI owns state-validated Menu and Done transitions, while `TagSelector`
owns read-only row, checkmark and Details-pill observations. The only mutation
is `SetExistingPokemonTag` against the matched row. It is omitted when the
requested state already holds or no confident name match exists. Selector
scrolling is profile-bounded and every action and postcondition is audited.
Details verification dynamically counts connected gray or colored pill
components in the tag section and requires the expected before/after delta.
This supports zero, one and multiple simultaneous tags without treating one
fixed tag color or location as authoritative.

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
  EnterTextAsync
  SubmitAsync
```

The input interface contains only named text entry and submit in addition to
tap/swipe. It contains no arbitrary shell, arbitrary key event, location
control or destructive game action.

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

## Version 0.14.1: nullable candidate-IV correction

The appraisal analyzer represents an unavailable IV estimate as null and a
measured candidate as an integer from 0 through 15. Version 0.14.1 makes that
nullable contract explicit in the local measurement variable. No architectural
boundary changed.

## Version 0.14.2: consistent decoder diagnostics

All image-analysis stages now treat `ScreenVisionException` as a recoverable
per-file diagnostic when the surrounding acceptance gate still has enough
decoded evidence. Unsupported files remain traceable and cannot silently
become valid observations.

## Version 0.14.3: exception-pattern syntax correction

The appraisal pretest's recoverable decoder policy is unchanged. Version
0.14.3 only corrects the C# syntax used to express the existing list of
recoverable exception types.

## 2026-07-19 real-phone validation update

The connected OnePlus A6013 has now exercised the real validation path:

- `phone-prepare` produced a device-adjusted appraisal profile from a live
  appraisal screen.
- `phone-calibration-stability.md` recorded three appraisal cases with
  stable transforms, zero Complete observations and distinct IV triplets.
- `calcy-probe` confirmed `tesmath.calcy` version 3.44 and the read-only
  evidence surfaces used by the current probe boundary.
- `calcy-live-check` completed a one-item appraisal capture and then ran the
  same read-only probe path.

This confirms that the architecture's current read-only boundaries still hold
on a real phone while the verified provider gate remains closed.

## 2026-07-21 dynamic identity tuning

`PokemonDetailsIdentityAnalyzer` keeps full screenshot SHA-256 as evidence and
uses a separate stable fingerprint. The Android profile searches the observed
Details tag band for bounded pill-shaped components, then detects a long
near-gray divider below the mutable section. The stable lower ROI is sampled
relative to that divider and is deliberately short enough to exclude fixed
bottom navigation controls. The synthetic fixture covers zero, one and two
tags with shifted lower content and passes with 155/155 package-free tests.

The real captured zero/one/two-tag states produce tag counts 0/1/2. Their
zero-tag versus tagged similarity is 0.9815 against the configured 0.965
threshold, while one- and two-tag states share the same fingerprint. This is
evidence for the guarded identity path, not a production provider gate or a
real-phone Task 4 approval. A local five-frame Inventory capture is rejected
as Unavailable rather than interpreted as Details.

## 2026-07-21 deterministic navigation safety validation

`PogoInventory.Cli device-validate-navigation-safety` is a validation shell
over `AndroidVerifiedInventoryNamedOperations`. The host remains the owner of
locators, state detectors, authorization, recovery and transport calls. The
optional `NavigationSafetyTraceRecorder` observes host captures and records
phase-aligned evidence; it cannot send input. Post-input evidence is completed
with bounded screenshot reads only, and POSTCONDITION is written after five
frames. The command is limited to read-only navigation and does not establish
real-phone acceptance until a manual safe-state precondition and bounded phone
run pass.
