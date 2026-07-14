# Validation report

## Version

0.6.2

## Reported CI result

The 0.6.1 solution compiled successfully. GitHub Actions then ran all 52 self-tests.

Result:

```text
51/52 tests passed.
```

The only failure was:

```text
Fake snapshot writes PNG, metadata and manifest:
Expected harness version to be '0.2.0', got '0.6.1'.
```

## Root cause

The production path already used `DeviceHarnessOptions.CurrentVersion` when writing the manifest. The self-test still contained a historical literal from the original device-harness release:

```csharp
AssertEqual("0.2.0", ...);
```

The application output was correct. The assertion was stale.

## Fix

The self-test now compares the manifest value with the same version constant used by the product:

```csharp
AssertEqual(
    DeviceHarnessOptions.CurrentVersion,
    root.GetProperty("harnessVersion").GetString(),
    "harness version");
```

`DeviceHarnessOptions.CurrentVersion` and the deterministic fake build fingerprints are bumped to `0.6.2`.

## Behaviour impact

None outside version metadata. The following remain unchanged:

- automatic inventory navigation
- input action whitelist
- screen-state checks
- evidence capture
- checkpoint and resume logic
- end-of-inventory detection
- no transfer automation

## Static validation completed

- complete 0.6.1 repository unpacked
- failing assertion located from the GitHub Actions log
- assertion changed to the shared version constant
- all JSON files parse successfully
- all project XML files parse successfully
- all project references resolve
- self-test declaration count remains 52
- ZIP contains no private captures, inventory data, `bin`, `obj` or `.git` content

## Required CI result

GitHub Actions must now:

1. build all seven projects
2. pass all 52 self-tests
3. complete the deterministic three-item fake inventory scan
4. complete the synthetic vision and calibration checks

Do not begin the next milestone until 0.6.2 is green.
