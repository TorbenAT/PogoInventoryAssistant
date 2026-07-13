# Architecture

## Target architecture

```text
Android phone
    |
    | USB / ADB
    v
Device Harness
    |
    +--> device discovery
    +--> metadata and health
    +--> screenshot capture
    +--> later: strictly whitelisted input actions
    |
    v
Screen State Detector
    |
    +--> Calcy Adapter
    +--> Visual Scanner / OCR
    |
    v
Observation Pipeline
    |
    v
Inventory Database
    |
    +--> Identity Matcher
    +--> PvP Analyzer
    +--> Rule Engine
    |
    v
Execution Plan
    |
    v
Tag Executor

Manual final transfer only
```

## Implemented in 0.3.0

```text
PogoInventory.Cli
    |
    +--> analyze
    |      |
    |      v
    |   PogoInventory.Core
    |
    +--> device-snapshot
    |      |
    |      v
    |   PogoInventory.Device
    |
    +--> screen-detect
    |      |
    |      v
    |   PogoInventory.Vision
    |      +--> PNG decoder
    |      +--> profile validation
    |      +--> fingerprint extraction
    |      +--> anchor evaluation
    |      +--> state selection
    |      +--> evidence report
    |
    +--> screen-fingerprint
           |
           v
       anchor calibration helper
```

## Project boundaries

### PogoInventory.Core

Contains Pokémon observations, policy, conservative decision logic and reports. It has no dependency on Android, ADB, screen images, Calcy, OCR, databases or UI frameworks.

### PogoInventory.Device

Owns all Android and ADB interaction.

Current public capabilities:

- list devices
- read metadata
- capture a screenshot

There is no input-control method or arbitrary shell method.

### PogoInventory.Vision

Owns screen-image parsing and classification. It has no dependency on ADB, Android, file locations or the inventory rule engine.

Responsibilities:

- decode supported PNG screenshots
- validate image geometry
- extract deterministic fingerprints from normalised regions
- compare screen evidence against a validated profile
- return `ScreenDetectionResult`

The detector receives a `PixelImage` and a `ScreenDetectionProfile`. It does not know how the image was captured.

### PogoInventory.Cli

Provides:

- `analyze`
- `device-snapshot`
- `screen-detect`
- `screen-fingerprint`

### PogoInventory.SelfTest

Runs deterministic tests without third-party test frameworks or a connected phone.

## Screen detection flow

```text
PNG bytes
    |
    v
Validate PNG structure and safety limits
    |
    v
Decode to RGBA pixels
    |
    v
Validate orientation, dimensions and aspect ratio
    |
    +--> invalid: Unknown with geometry reason
    |
    v
For every state definition
    |
    +--> extract each named normalised-region fingerprint
    +--> compare against every reference sample
    +--> evaluate Required / Optional / Forbidden condition
    +--> calculate deterministic score
    |
    v
Keep eligible states above minimum score
    |
    +--> none: Unknown
    +--> winner margin too small: Unknown
    +--> one clear winner: classified state
    |
    v
JSON evidence report
```

## Fingerprint modes

### Color

Stores average RGB values in a fixed-size grid. Best for stable colored UI controls.

### Grayscale

Stores luminance only. Best where shape and brightness matter more than color.

### Edge

Stores local grayscale changes. Best for stable outlines where backgrounds vary.

A future calibrated profile may combine modes across different anchors.

## Anchor semantics

### Required

Must match its configured threshold. Failure makes the state ineligible.

### Optional

Contributes to the state score but does not independently reject the state.

### Forbidden

Must not match. A match makes the state ineligible. Forbidden anchors do not inflate the positive state score.

## State selection rules

The detector fails closed.

A state is selected only when:

- all required anchors match
- no forbidden anchor matches
- the weighted positive score meets `MinimumStateScore`
- the winner exceeds the second eligible state by `MinimumWinnerMargin`

Otherwise the output is `Unknown` with explicit reasons.

## PNG safety boundary

The internal decoder supports common Android screenshot formats:

- 8-bit grayscale
- 8-bit RGB
- 8-bit grayscale with alpha
- 8-bit RGBA
- non-interlaced PNG
- PNG filters 0 through 4

It rejects:

- invalid signatures or chunk lengths
- unsupported color types or bit depths
- interlaced images
- dimensions above the configured hard limit
- decompressed data beyond the exact expected size

CRC validation is not implemented in 0.3.0. Structural validation, exact decompressed length and the Device Harness SHA-256 manifest provide the current integrity boundary.

## Synthetic versus real profiles

`data/screen-profile.synthetic.json` proves the classification framework. It contains no Pokémon GO or account data.

A real profile must be generated locally from redacted screenshots. Stable anchors must avoid:

- Pokémon artwork
- Pokémon name
- CP and HP values
- trainer name
- Stardust or Candy counts
- dynamic background content

Prefer stable buttons, panels, icons and dialog geometry.

## Future module boundaries

### Real-screen calibration

Creates a private local profile and a confusion report from approved fixtures. Real images remain outside Git.

### Calcy Adapter

Must remain behind an interface. Any intent, logcat or clipboard integration is replaceable and may be abandoned if unstable.

### Observation Pipeline

Consumes a verified screen state and scanner results. It must not proceed from `Unknown`.

### Identity Matcher

Must assign Exact, HighConfidence, Ambiguous or Mismatch. Only Exact may later receive a delete tag.

### Tag Executor

Must use named actions, verified pre- and post-states, audit evidence and hard stop conditions. It must not contain transfer or gameplay functions.
