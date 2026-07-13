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

## Implemented in 0.2.0

```text
PogoInventory.Cli
    |
    +--> analyze
    |      |
    |      v
    |   PogoInventory.Core
    |      +--> policy
    |      +--> decision engine
    |      +--> reports
    |
    +--> device-snapshot
           |
           v
       DeviceSnapshotService
           |
           +--> IAndroidDeviceTransport
           |      +--> AdbAndroidDeviceTransport
           |      +--> FakeAndroidDeviceTransport
           |
           +--> device selection
           +--> screenshot validation
           +--> atomic file output
           +--> SHA-256 manifest
```

## Projects

### PogoInventory.Core

Contains domain types and analysis logic. It has no dependency on Android, ADB, Calcy, OCR, UI frameworks or databases.

### PogoInventory.Device

Owns all Android and ADB interaction.

Public capabilities are deliberately limited to:

- list devices
- read metadata
- capture a screenshot

There is no input-control method in the interface.

The ADB implementation exposes no arbitrary shell API. Commands are assembled internally from a small read-only set:

```text
adb devices -l
adb -s <serial> shell getprop
adb -s <serial> shell wm size
adb -s <serial> shell dumpsys battery
adb -s <serial> exec-out screencap -p
```

### PogoInventory.Cli

Provides:

- `analyze`
- `device-snapshot`

It converts structured device failures to stable exit codes.

### PogoInventory.SelfTest

Runs deterministic tests without third-party test packages or a connected phone.

## Device snapshot flow

```text
List devices
    |
    v
Select exactly one authorised device
    |
    +--> zero: fail closed
    +--> more than one: fail closed unless --serial is supplied
    |
    v
Read properties, screen and battery
    |
    v
Capture PNG screenshot
    |
    v
Validate PNG signature
    |
    v
Write temporary files
    |
    v
Atomically move into final paths
    |
    v
Write SHA-256 manifest
```

## Failure model

Device failures use `DeviceHarnessException` and `DeviceErrorCode`.

Important categories:

- ADB missing or unable to start
- command timeout
- command returned non-zero
- no authorised device
- multiple authorised devices
- requested serial missing or unauthorised
- invalid ADB output
- invalid screenshot
- file-system failure

The caller must not infer success from partial output.

## Future module boundaries

### Screen State Detector

Must classify screenshots into explicit states and return evidence. It must not control the phone.

### Calcy Adapter

Must remain behind an interface. Logcat, clipboard or intent integration is not a guaranteed public API and must be replaceable.

### Visual Scanner

Must return nullable values. Failure to detect an icon is not absence.

### Inventory Database

Planned SQLite storage for observations, evidence, decisions, execution plans and audit logs.

### Identity Matcher

Must assign one of:

- Exact
- HighConfidence
- Ambiguous
- Mismatch

Only Exact may later receive a delete tag.

### Tag Executor

Must use a strict action whitelist and verified screen states. It must not contain code for transfer, evolve, purify, power-up, TM use, purchase, catch, battle, spin or location changes.
