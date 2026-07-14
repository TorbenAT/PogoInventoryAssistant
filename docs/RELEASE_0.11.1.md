# Release 0.11.1

## Purpose

Correct the version 0.11.0 CLI compilation failure.

## Failure observed in GitHub Actions

`PogoInventory.RegionDiscovery` compiled successfully, but
`src/PogoInventory.Cli/Program.cs` could not resolve:

- `RegionDiscoveryOptions`
- `RegionDiscoveryRunner`
- `RegionDiscoveryReportWriter`
- `RegionCandidateKind`

The CLI project already referenced `PogoInventory.RegionDiscovery`. The missing
piece was the two explicit namespace imports.

## Fix

Added:

```csharp
using PogoInventory.RegionDiscovery.Models;
using PogoInventory.RegionDiscovery.Services;
```

## Scope

No image-analysis algorithm changed. No report schema changed. No thresholds
changed. No phone input action was added.

Expected total: 91 self-tests.
