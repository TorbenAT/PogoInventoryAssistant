# Release 0.14.1

## Purpose

Correct the version 0.14.0 compile failure in `AppraisalAnalyzer.cs`.

## Failure

The compiler reported CS0173 because this expression had no target nullable
type:

```csharp
var estimatedIv = trackDetected
    ? integerValue
    : null;
```

## Fix

The local variable is now explicitly nullable:

```csharp
int? estimatedIv = trackDetected
    ? integerValue
    : null;
```

This preserves the intended semantics:

- detected and measurable track: candidate IV from 0 to 15
- missing or unmeasurable track: null

## Scope

No appraisal regions, color thresholds, search ranges, report schemas, phone
preparation behavior or phone actions changed.

Expected total: 112 self-tests.
