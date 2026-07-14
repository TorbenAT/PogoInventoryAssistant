# Release 0.13.1

## Purpose

Correct the version 0.13.0 compilation failure in
`SemanticEvidenceRunner.cs`.

## Failure observed in GitHub Actions

The compiler could not resolve:

- `PixelImageTransforms`
- `CropAtlasJson`

Both helpers already existed in the same assembly under
`PogoInventory.CropAtlas.Services`. The nested semantic namespace lacked the
required import.

## Fix

Added:

```csharp
using PogoInventory.CropAtlas.Services;
```

## Scope

No algorithm, threshold, report schema, truth policy or phone action changed.

Expected total: 103 self-tests.
