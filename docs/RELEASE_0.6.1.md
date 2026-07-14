# Release 0.6.1

## Compile regression fix

GitHub Actions found a C# type-inference error in `InventoryAutomationRunner.FinishAsync`.

The completion timestamp expression returned either a `DateTimeOffset` value or `null`. With `var`, the compiler could not infer a common type for the conditional expression.

The local variable is now declared explicitly as nullable:

```csharp
DateTimeOffset? completedAt = status == AutomationRunStatus.Completed
    ? DateTimeOffset.UtcNow
    : null;
```

This matches the existing nullable `InventoryScanCheckpoint.CompletedAtUtc` property.

## Scope

No automation behavior, control point, screen-state rule, evidence format or safety boundary changed.

The release still contains:

- automatic navigation from inventory list to appraisal
- unattended swipe-through
- ordered evidence capture
- checkpointing and safe resume
- deterministic fake-phone traversal
- no transfer, tagging, resource use, gameplay or location changes

## Validation gate

GitHub Actions must now:

1. build all seven projects
2. run all 52 self-tests
3. complete the deterministic three-item fake inventory scan
4. complete the existing synthetic vision and calibration checks

The next milestone remains automatic core-profile bootstrap and Calcy observation extraction.
