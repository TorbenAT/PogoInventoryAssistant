# Data handling

The repository can remain public while it contains only source code and synthetic data.

Real inventory evidence must remain under ignored local paths.

## Never commit while public

- phone serial numbers
- real Pokémon GO screenshots
- inventory scan checkpoints
- account or trainer names
- complete inventory exports
- real screen profiles
- local automation profiles
- SQLite databases
- logs with personal or account data
- authentication material or ADB keys

## Automatic scan workspace

The recommended output is:

```text
local-data\inventory-scans\<run>\
```

It contains:

```text
inventory-scan-checkpoint.json
captures\000001.png
captures\000002.png
...
```

The entire `local-data` tree is ignored by Git.

## No per-image approval

The automatic scan does not pause for privacy approval. Screenshots are local processing evidence and are accepted into the run automatically.

This is separate from public sharing. Automatic local acceptance does not make a screenshot suitable for GitHub, issues or chat.

## Checkpoint data

The checkpoint contains:

- device serial
- screen geometry
- profile name
- run timestamps
- ordered screenshot paths and hashes
- identity fingerprints
- screen confidence
- action audit

Treat the checkpoint as private account data.

## Calibration fallback

The older guided calibration workspace may still contain `incoming`, `fixtures` and manual review records. That workflow remains available for debugging but is no longer the target scan path.

## Integrity

- every screenshot has SHA-256
- every identity fingerprint has SHA-256
- item sequences must be contiguous
- checkpoint writes are atomic
- resume requires matching device, geometry and last identity
- a completed or stopped checkpoint is not silently modified

## Public test data

Only synthetic, cropped or deliberately redacted fixtures may be committed.
