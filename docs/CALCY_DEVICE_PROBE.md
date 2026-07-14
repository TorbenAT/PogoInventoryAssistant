# Calcy device probe

## Purpose

The probe determines what the installed Calcy IV version exposes on the fixed Android phone. It does not assume that an integration from an old open-source project still works.

The default package name is:

```text
tesmath.calcy
```

This matches the official Google Play listing:

```text
https://play.google.com/store/apps/details?id=tesmath.calcy
```

## Read-only Android surfaces

`IAndroidAppInspectionTransport` exposes only named operations:

```text
ReadPackageDumpAsync
ReadPackagePathAsync
ReadProcessIdAsync
ReadRecentLogcatAsync
ReadAccessibilityStateAsync
ReadAppOpsAsync
ReadActivityServicesAsync
```

The ADB implementation maps those methods to fixed command shapes. No arbitrary shell method is exposed to higher layers.

The probe never clears logcat, launches an activity, changes settings or writes to the phone.

## Output

The local output contains:

```text
calcy-probe-report.json
calcy-probe-report.md
evidence/package-dump.txt
evidence/package-path.txt
evidence/process-id.txt
evidence/logcat-full.txt
evidence/logcat-filtered.txt
evidence/accessibility.txt
evidence/appops.txt
evidence/activity-services.txt
evidence/screen.png
```

Every evidence file has a SHA-256 recorded in the report.

## Decisions

The probe reports one of:

```text
PackageMissing
InstalledNeedsLiveEvidence
CandidateEvidenceFound
InspectionFailed
```

`CandidateEvidenceFound` does not mean that a production adapter is finished. It only means that one or more potentially useful local Android surfaces produced evidence.

## Local handling

The full logcat can contain unrelated local device information. It is written under `local-data/`, which is ignored by Git. The public repository contains only synthetic evidence.
