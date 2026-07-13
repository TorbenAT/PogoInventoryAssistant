# Architecture

## Target architecture

```text
Android phone
    |
    | USB / ADB
    v
Device Harness
    |
    +--> Screen Capture
    +--> Device Health
    +--> later: whitelisted input actions
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

## Current implementation

Version 0.1.0 implements only the shaded logical centre of the future system:

```text
Inventory JSON
    |
    v
Inventory Analyzer
    |
    +--> hard-protection checks
    +--> protected-status checks
    +--> duplicate grouping
    +--> preliminary PvP preservation
    |
    v
KEEP / REVIEW / DELETE plan
    |
    +--> JSON report
    +--> Markdown report
```

## Projects

### PogoInventory.Core

Contains all domain types and analysis logic. It has no dependency on Android, ADB, Calcy, OCR, UI frameworks or databases.

### PogoInventory.Cli

Loads policy and inventory JSON, runs the analysis and writes reports.

### PogoInventory.SelfTest

Runs deterministic tests without external test packages.

## Future module boundaries

### Device Harness

Must own all process calls to `adb`. Other modules must never run arbitrary shell commands.

### Screen State Detector

Must convert screenshots into explicit states such as:

- InventoryList
- PokemonDetails
- AppraisalOpen
- TagDialogOpen
- Popup
- NetworkError
- Unknown

### Calcy Adapter

Must be behind an interface. Calcy logcat or clipboard integration is not a stable public API and must therefore be replaceable.

### Visual Scanner

Must return nullable values. Failure to detect an icon must not be interpreted as absence.

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

Must use a strict action whitelist. It must not contain code for transfer, evolve, purify, power-up, TM use, purchase, catch, battle, spin or location changes.
