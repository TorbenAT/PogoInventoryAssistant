# Streaming Vision Phase 6A — offline semantic foundation

Phase 6A is an isolated, read-only offline layer. It accepts replay metadata and
normalized regions; it does not reference `PogoInventory.Device`, automation,
live streaming, navigation, tags, or destructive operations.

## Contracts and safety

`PogoInventory.Streaming.Semantics` defines `FieldReading<T>`, evidence-bound
frame observations, field analyzers, region validation, and a generic
`FailClosedFieldConsensusGate<T>`. A value is `Known` only when the configured
confidence threshold and independent agreement count are met. Conflicts,
occlusion, invalid layouts, missing evidence, and unsupported methods remain
explicit statuses. There is no global Pokemon-complete score.

The current baseline analyzer intentionally returns `Unsupported` for model
fields. This is safer than treating absent OCR/model runtimes as a negative or
guessing from species, level, or neighboring frames.

## Evidence inventory

The clean baseline contains committed synthetic screen fixtures under
`data/screen-fixtures/` and 23 committed iPhone pretest images under
`data/iphone-images/`. The iPhone images are cross-platform fixtures and are
not Android coordinate, Calcy, or live-stream truth. The existing Task-K files
document prior manually reviewed cases, but no Phase 6A truth manifest is
present in this clean worktree. Therefore Phase 6A has zero verified truth
cases and makes no accuracy claim against real screenshots.

## Benchmark

`PogoInventory.Streaming.Semantics.Benchmarks` runs without a phone and writes a
bounded JSON report. The synthetic contract check records one correct CP
consensus and `False Complete: 0`. GPU embedding and alternative OCR are
reported as unavailable until a local model/runtime is installed and a
provenance manifest exists; they are never silently substituted.

## Local tools and models

The intended ignored directory is `C:\Data\PokemonGo-Tools` (or a path supplied
by environment variables `POGO_FFMPEG_PATH`, `POGO_SCRCPY_SERVER_PATH`,
`POGO_PYTHON_PATH`, and `POGO_MODEL_CACHE`). No third-party binaries, Python
environment, model weights, or machine-specific paths are committed. The
implementation expects scrcpy server `4.0`, `control=false`, `audio=false`,
and raw H.264 output, matching the existing preflight options.

## Limitations and next phase

The current host has an NVIDIA GeForce RTX 4060 Ti with 8188 MiB reported by
`nvidia-smi`, but no `ffmpeg`, `scrcpy`, or Python executable in PATH. CUDA and
model latency are consequently unmeasured. Installing those dependencies and
adding a documented verified truth manifest are prerequisites for a real CPU/GPU
comparison. Phase 6B may later adapt replay results to `FrameLease`; this
commit does not alter Phase 5 runtime or authorize any phone input.

Read-only audit: no Device/Automation reference, no tap/swipe API, and
`InputCommandsSent = 0`.
