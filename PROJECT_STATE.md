# Project state

## Current version

0.2.0

## Completed

### M0: Foundation

- repository and .NET 8 solution structure
- Pokémon observation model with nullable special-status fields
- JSON decision policy
- conservative KEEP / REVIEW / DELETE analysis
- duplicate grouping and strictly-better duplicate requirement
- preliminary PvP candidate preservation
- JSON and Markdown decision reports
- package-free self-tests

### M1: Read-only Device Harness

- separate `PogoInventory.Device` project
- Android device abstraction with ADB and fake implementations
- exact-one-authorised-device selection
- optional explicit serial selection
- hard-coded read-only ADB operations only
- device properties, screen size and battery parsing
- screenshot capture and PNG validation
- atomic snapshot output
- SHA-256 screenshot manifest
- command timeout and cancellation support
- structured error codes and CLI exit codes
- console event logging
- fake-device CLI mode
- parser, selection, cancellation and file-output self-tests
- GitHub Actions build, test and fake-capture workflow

## Not completed

- compilation in the assistant build environment
- validation with Torben's real Android phone and installed Platform Tools
- screen-state recognition
- Pokémon GO screen anchors
- popup and network-error detection
- Calcy integration
- OCR and icon recognition
- inventory scanning loop
- SQLite database and checkpoints
- exact Pokémon fingerprinting
- full PvPoke / Ohbem integration
- device-side tagging

## Required user-side checkpoint

After pushing version 0.2.0:

1. Confirm the GitHub Actions CI run is green.
2. Run `scripts\run-fake-device.ps1` on the Windows computer.
3. Install Android Platform Tools.
4. Run one real `scripts\capture-device.ps1` capture.
5. Confirm that `screen.png` is a correct screenshot and metadata matches the phone.

Do not start M2 against the real phone until these checks pass.

## Next recommended milestone

M2: Screen State Detector, read-only.

The next package should classify screenshots into explicit states without sending input:

- `PokemonDetails`
- `InventoryList`
- `AppraisalOpen`
- `PokemonMenuOpen`
- `TagDialogOpen`
- `Loading`
- `Popup`
- `NetworkError`
- `Unknown`

It must start with a generic anchor framework and recorded test fixtures. It must not add tapping or swiping.

## Design decisions preserved

- C# and .NET 8
- no hidden game API
- no automatic transfer
- no anti-detection behaviour or human imitation
- unknown data results in REVIEW, never DELETE
- DELETE requires an exact identity and a documented better duplicate
- all ADB execution is isolated in `PogoInventory.Device`
- every release includes updated project state and continuation prompt
