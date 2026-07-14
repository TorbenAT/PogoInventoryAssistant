# Release 0.6.2

## Self-test hotfix

GitHub Actions confirmed that version 0.6.1 compiled and reached the self-test stage. 51 of 52 tests passed.

The single failing test expected the old device-harness version `0.2.0`, although the application correctly wrote its current version to the snapshot manifest.

The assertion now uses:

```csharp
DeviceHarnessOptions.CurrentVersion
```

instead of a historical hard-coded value. This prevents the same regression on future version bumps.

The runtime version and deterministic fake build fingerprints are updated to `0.6.2`. No automation behavior changed.

## Expected validation

- all seven projects compile
- all 52 self-tests pass
- fake inventory traversal captures three items
- synthetic screen detection and calibration remain green
