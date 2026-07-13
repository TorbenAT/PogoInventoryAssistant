# Release 0.3.1

## Scope

Compiler hotfix for M2 Generic Screen State Detector.

## Fixed

GitHub Actions reported `CS1503` at `PngDecoder.cs` line 238 because `left`, `up` and `upLeft` are inferred as `int` when the PNG unfiltering code uses `0` as the fallback value, while `Paeth` accepted `byte`.

`Paeth` now accepts and returns `int`. The final reconstructed sample is still converted to `byte` at the existing output assignment:

```csharp
4 => unchecked((byte)(raw + Paeth(left, up, upLeft)))
```

This matches the PNG filter arithmetic and removes unnecessary narrowing conversions.

A dedicated RGBA fixture now exercises PNG filter type 4 and verifies the reconstructed pixels exactly.

## Safety

No phone control, account mutation or new runtime capability was added.

## Acceptance

Push the complete 0.3.1 repository and require the GitHub Actions workflow to complete successfully.
