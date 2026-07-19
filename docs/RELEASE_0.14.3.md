# Release 0.14.3

## Purpose

Correct the C# pattern syntax introduced in version 0.14.2.

## Failure

The exception filter incorrectly contained:

```csharp
exception is ScreenVisionException or
exception is InvalidDataException or
...
```

Inside one `is` pattern, the `or` alternatives must not repeat the expression.

## Fix

```csharp
catch (Exception exception) when (
    exception is ScreenVisionException or
    InvalidDataException or
    NotSupportedException or
    ArgumentException or
    OverflowException)
```

## Scope

No decoder policy, appraisal geometry, thresholds, report schema, removal
script or phone action changed.

Expected total: 114 self-tests.
