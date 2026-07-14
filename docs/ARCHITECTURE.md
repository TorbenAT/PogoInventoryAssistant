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
