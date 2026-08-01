# Architecture

## Offline reference-safe species replay (2026-08-01)

`replay-stream-species` is an offline-only CLI path. It reads the existing
stream `items.jsonl`, resolves every relative evidence path underneath the
declared source root, SHA-256 verifies every PNG against the recorded hash,
then re-runs the header parser. It has no Device or Automation dependency and
never changes the source run. A replayed Known value requires two independent
hash-verified frames and is compared with the prior Known value; a differing
Known result is reported as false-Known rather than overwritten.

`ReferenceSafeSpeciesResolver` first uses ordinary reference normalization.
For an otherwise unreadable label it may remove only pure punctuation or an
exact `100` rating (including OCR `l00`), and may complete at most two missing
terminal characters only when exactly one reference species has that prefix.
All other suffixes, ratings and ambiguous completions remain Unknown.

`StreamProofMetrics.FramesEvicted` is bounded-buffer retention telemetry. The
report deliberately no longer calls it `FramesDropped`, which could imply a
decoder or network loss.

## Final stream-proof safeguards (2026-07-31)

The final reader still treats the normal 0.70 IV bar-confidence floor as the
only route to Complete inventory fields. `StreamPokemonReaderCommand` also
constructs a separate progression-only semantic result from a stable,
five-frame IV tuple whose individual bars are at least 0.65 confident. That
result is consulted only for the high-similarity handoff comparison and is
explicitly prevented from changing `IsComplete`. This admits the observed
stable Burmy IV 3/2/8 transition without turning an uncertain field into
inventory truth.

`AdbAndroidDeviceTransport` retries only a failed read-only screenshot when
ADB reports that its daemon is unreachable and the selected serial is an
explicit IPv4/IPv6 endpoint with a port. It runs one `adb connect <serial>`
and retries the same screenshot once; input operations never retry. For
`guarded-back`, the unsafe-modal detector can be bypassed only from an already
authorized Details state with both full Details topology and a canonical close
control in the fresh frame. All other unsafe-modal decisions remain blocking.

## High-similarity progression fallback (2026-07-30)

For every ordinal after the first, `AppraisalHandoffEvaluator` requires
post-action transition evidence before collecting stable candidate frames.
This prevents pre-render frames from arming a handoff. The candidate still
uses three compatible stable AppraisalBars frames and the calibrated regional
pHash cluster.

One `MultiRegionTemporalObserver` lives across the complete run, so the first
post-swipe frame is compared with the last observed pre-swipe frame. A frame
tagged `MissingPreviousFrame` cannot establish transition. The observer is
disposed before the stream stops and before shutdown integrity is measured.

When the canonical candidate remains at or above the visual same-item
threshold, the CLI analyzes it without yet recording it. Progression is
accepted only if at least one field is Known on both items and has changed
(species, CP or an IV). Unknown and Conflicting fields are not evidence of a
change. With no Known difference, the run safe-stops as
`SEMANTIC_PROGRESSION_NOT_PROVEN`; it does not guess filter end and sends no
retry swipe.

## Stream reader settling handoff (2026-07-29)

`device-stream-read-pokemon` records the latest stream frame before a named
setup action or carousel swipe, then uses only subsequent scrcpy/FFmpeg frames
for a bounded settling handoff. `FrameBarrier` enforces frame-id, capture-time,
age and AppraisalBars state freshness. `AppraisalHandoffEvaluator` applies the
existing required-region thresholds without considering Model or animated
background; it retains three distinct qualifying frames and compares only
Header/AppraisalPanel/IV-bar fingerprints for progression. Motion, sharpness,
difference and similarity failures are non-terminal observations during the
handoff. Timeout yields bounded evidence and a fail-closed stop; it never
sends a retry swipe.

## Consolidated semantic boundaries (2026-07-28)

The streaming transport and regional temporal gates are read-only and remain
separate from Device/Automation input. Phase 6A semantic contracts preserve
Unknown, Unsupported and candidate states without guessing. The Ollama client
is a diagnostic candidate provider only: it can inspect model capabilities,
embeddings and vision candidates, but it cannot authorize a Known observation
or phone action. Phase 6B copies leased BGRA evidence before disposal, runs
bounded analyzers and compares candidates with an explicit reference provider;
it reports conflicts, timeouts and coverage gaps fail-closed. EasyOCR/Ollama
production wiring and verified screenshot reference evidence remain incomplete.

## Phase 6A VLM evidence boundary (2026-07-28)

`PogoInventory.Streaming.Semantics.Ollama.Evidence` is an offline, read-only
benchmark runner. It sends the actual JSON schema object in Ollama's
`format` field, records raw and parsed responses, and retains unknown,
conflicting, occluded, unreadable, not-visible and unsupported outcomes.
Model output is always a `Candidate`; the VLM is never a per-frame provider,
never authorizes an action, and never reaches Device or Automation. Evidence
accuracy remains unmeasured until verified Android/Calcy truth is available.

## Phase 6B bounded semantic shadow boundary (2026-07-28)

`PogoInventory.Streaming.Semantics.Shadow` consumes copied BGRA evidence from
the existing streaming leases and runs named semantic adapters in bounded
parallelism. It compares analyzer candidates with an explicit reference
provider and preserves conflicts, timeouts, faults and coverage gaps rather
than resolving them. `PogoInventory.Streaming.Observe.Shadow` is an opt-in
read-only composition root for scrcpy/FFmpeg; it has no phone-input surface.
EasyOCR, Ollama and screenshot-reference implementations remain adapter
points until real verification evidence exists.

## Phase 6A offline semantic boundary

`PogoInventory.Streaming.Semantics` is isolated from Device, Automation, live
streaming and the decision engine. It accepts replay metadata and normalized
geometry, binds Known fields to evidence, and applies deterministic
fail-closed consensus. It can later adapt to a `FrameLease` without changing
Phase 5 runtime behavior.

## Opt-in Streaming Vision read-only boundary (2026-07-28)

Streaming Vision Phases 1-3 form an isolated observation path:
scrcpy raw H.264 (`control=false`, `audio=false`, `raw_stream=true`) is
decoded to pooled BGRA32 frames, published through bounded leases and
subscriptions, analyzed by regional temporal observers, and evaluated by
fail-closed gates. `observe-stream` and `observe-gates` emit diagnostics and
bounded evidence only. These projects do not reference `PogoInventory.Device`
or any navigation, tap, swipe, clipboard, tagging or destructive operation;
the existing screenshot and cleanup flows remain unchanged.

The FFmpeg pipe binding intentionally omits `-fflags nobuffer`: real-phone
evidence showed that flag suppresses rawvideo output for this pipe-fed H.264
mode. `-flags low_delay` remains enabled. Partial stdout reads are accumulated
to exact `width × height × 4` BGRA frames, and Phase 6A semantic benchmarks
remain offline and do not cross this boundary.

## Appraisal gate calibration boundary (2026-07-28)

Screen normalization for streaming calibration remains outside the streaming
projects. `device-calibrate-appraisal-streaming-gates` performs a bounded
read-only state consensus, then delegates the `PokemonDetails -> PokemonMenuOpen
-> AppraisalIntro -> AppraisalBars` route to
`AndroidVerifiedInventoryNamedOperations.CaptureAppraisalAsync`. The named
host and `GuardedInventoryRecovery` own all input authorization, fresh-frame
checks, postconditions, audit records and the six-input setup budget. Unknown,
conflicting or unsafe states stop with zero input; AppraisalIntro receives at
most one Continue tap and neither AppraisalIntro nor AppraisalBars authorizes
Android Back.

Once `AppraisalBars` is confirmed with three compatible frames among five,
setup stops and `observe-gates` runs read-only with `control=false`,
`audio=false` and `raw_stream=true`. The AppraisalBars profile requires the
Header, AppraisalPanel and all three IV-bar regions; BottomControl is
diagnostic-only, while Model and AnimatedBackground are volatile. Setup input
and calibration input are reported separately, so the streaming layer remains
read-only and cannot navigate or send arbitrary shell commands.

## Cleanup value-proof transition

### Persistent oldest-first ledger

Schema v5 adds `WorkBuckets`, `WorkItems` and append-only `WorkAttempts`.
Buckets use absolute dates and rendered, auditable phone queries, and SQLite
selects the earliest non-complete bucket after restart. A work item is durable
before any phone checkpoint; completion requires explicit evidence rather than
relative age or a phone tag alone.

Cleanup proof uses a persistent Appraisal carousel for ordinary item
progression. The first Details baseline is persisted before Appraisal opens;
subsequent stable AppraisalBars fingerprints are persisted before the next
single named horizontal swipe. Appraisal is exited once at the end, then the
existing canonical unwind and SQLite report generation run. Details-only tags
remain Unknown in this pass.

The Bars anchor uses the device-adjusted appraisal profile, not a generic UI
guess: it requires a profile candidate at >=0.80 and all three measured IV
tracks. This accommodates the observed 0.832 real carousel frame while still
rejecting partial/weak candidates. A rejected recovery window persists each
frame and anchor as read-only diagnostic evidence and authorizes no input.

The permanent value proof starts directly with
`EnsureFilteredInventoryAsync`. It reuses an already verified Inventory state
or establishes Inventory through its own named, bounded operations; no
pre-batch close-to-map action is sent. This avoids an unnecessary transition
and still fails closed on an unknown, popup or unsafe state before input.
`CanonicalCloseUnwindService` remains the separately verified end-of-run
recovery path. Once a Details identity and read-only tags
are captured, the runner writes `ScanRuns`, `Observations`, `PokemonRecords`
and an `Observed` event before it attempts appraisal. Appraisal is an
enrichment, not a persistence gate. A single authorized appraisal-exit tap may
use a bounded three-frame Details topology fallback; that fallback is not used
for preflight, inventory opening, ReturnToInventory or cursor authorization.

Strict `InventoryAnalyzer` recommendations are exported separately from
human-review-only comparative duplicate suggestions. Unknown protection fields
are listed and comparative suggestions can never trigger tags or destructive
actions.

The unsafe-confirmation detector is deliberately structural, but a raw
Power-Up-like score is not sufficient when the common host independently sees
PokemonDetails topology and its canonical close control on the same fresh ADB
screencap. The detector records that suppression as evidence; it never sends a
dismissal and leaves genuine/unknown modal surfaces fail-closed.

## Real cleanup proof value chain

`device-run-cleanup-proof` is the permanent bounded read-only value-chain
command. It uses `AndroidVerifiedInventoryNamedOperations` for filtered
Inventory navigation, one first-card open, guarded appraisal and one verified
cursor swipe per next ordinal. `CaptureCleanupIdentityAsync` accepts up to
eight bounded Details frames: three compatible frames are Complete, two are
Partial, and unresolved or unsafe surfaces stop input without a blind retry.

`CleanupProofRunner` writes each structured observation through
`InventoryPersistenceService` in a transaction that inserts `Observations`,
`PokemonRecords` and an `Observed` `InventoryEvents` row. The write service is
disposed before a new service instance reloads the batch. The existing
report validation compares this exact run-local reload with the in-memory
captured records; global database totals are only required to contain that
subset because a persistent inventory database accumulates prior batches.
An interrupted Partial observation is never silently merged into a later
matching Partial observation; it remains reconciliation-required until exact
identity evidence exists.
`InventoryAnalyzer` then evaluates only reloaded `PokemonObservation` rows;
recommendations and `RecommendationGenerated` events are written back before
CSV, Markdown and JSON reports are generated. No tag or destructive executor
is reachable from this command.

## Action authorization and unsafe confirmation interlock

`MainMenuPreconditionValidator` is the typed boundary for the MainMenu to
Inventory transition. It accepts only three stable observations whose strict
state is MainMenu and whose MainMenu and Inventory topology are positive.
Details, PokemonMenu, Appraisal, visual Details fallback, unsafe modal
evidence and any conflicting state invalidate the precondition. The host then
captures and validates a fresh frame immediately before the named tap; the
precondition and fresh screenshot hashes are retained in the audit record.

`UnsafeConfirmationSurfaceDetector` recognizes the paired-adjuster and large
confirmation-panel topology of the observed Power Up dialog and conservatively
blocks uncertain confirmation surfaces for Evolve, Transfer, Purify and
purchase/item actions. `AndroidVerifiedInventoryNamedOperations` applies the
interlock before every named tap, search text/submit, Back and cursor swipe.
Unsafe evidence is saved and audited with `InputSent: false`; no automatic
Cancel operation exists. Normal Details action buttons are not modal evidence.

`KnownBenignInterruptDetector` is a higher-priority, separate classifier for
three evidence-backed Pokémon GO interruptions: `EggHatch`/`Oh?`,
`WeeklyChallenge`, and the app's exact `KnownExitDialog`. It must establish
three compatible observations in a five-frame window, then re-locate the
permitted control on a fresh frame immediately before one named tap. For the
exact KnownExitDialog only, a separate named Android-Back fallback may run
after the same fresh three-stable precondition; no generic caller receives
Back authority. Its
central `KnownBenignInterruptRecovery` is called from the same verified host's
state waits and input authorization path, so inventory setup, cleanup batches,
stream-reader setup and resume share the recovery rather than adding bespoke
popup handling. A recovery has a six-input absolute budget (the current
families each use one); its next screen must be a stable known non-interrupt
state. Otherwise it records evidence and stops with no follow-up input.

This precedence does not weaken destructive protection: explicit Power Up,
Evolve, Transfer, Purify and purchase/item topology is denied even if another
visual rule matches. Generic or unrecognised modals stay unsafe/unknown; only
the dedicated exit rule may tap the separate CANCEL control, never OK.

## Verified inventory task sequence

`VerifiedInventoryTaskSequence` is the single sequential orchestration
boundary. Its host supplies only named operations for Inventory, Details,
Appraisal, tag observation/application and cursor advancement. The sequence
does not construct ADB commands, run parallel navigation, or expose delete.
The first card is opened once; normal progression uses one allow-listed swipe
from stable Details to stable Details and rejects an unchanged identity.
Every completed item is atomically checkpointed with query, ordinal instance
ID, cursor fingerprints, evidence hashes, appraisal and structured tag
observation. Partial states are preserved and may advance directly while
Details remains verified. Resume replays only verified cursor steps and requires
an identity overlap match before any new swipe. Unknown, no-effect and failed
recovery states are controlled-stopped.

`AndroidVerifiedInventoryNamedOperations` is the concrete real-device host.
It uses `IAndroidAutomationTransport`, `PokemonGoGameStateDetector`,
`GuardedInventorySearch`, `GuardedInventoryRecovery`,
`KnownBenignInterruptDetector`, `VisualControlLocator`
and `PokemonDetailsIdentityAnalyzer`; raw ADB construction remains inside
`PogoInventory.Device`. `device-run-index-sequence` is bounded and read-only
by default. Tag mutation is intentionally disabled for the first acceptance.

## Dynamic Details identity

`PokemonDetailsIdentityAnalyzer` is the Details identity boundary. It hashes
the complete PNG only as evidence integrity, then builds a separate stable
fingerprint from multiple model-independent ROIs. It dynamically records tag
section bounds and aligns lower content to a detected visual anchor. At least
three compatible usable frames are required for Complete; the canonical
consensus fingerprint is a deterministic bytewise median over compatible frame
fingerprints. Mutable tag state is not included in identity.
`PokemonIdentityInstance` uses `ScanRunId` plus ordinal and never uses a
screenshot hash as the instance key.

## Target flow

### Shared game-state detection

`PogoInventory.Exploration.PokemonGoGameStateDetector` is the single read-only
detector for the current game screen. It reuses `VisualControlLocator` for
Inventory, Details and Menu anchors and `AppraisalAnalyzer` for Appraisal.
Detection returns a normalized state, confidence, concrete evidence and the
SHA-256 of the captured screenshot. No UI hierarchy is used as sole evidence.

`GuardedInventoryRecovery` owns recovery stability and transition policy.
AppraisalIntro stability uses only the dialog and overlay-anchor ROIs;
AppraisalBars stability uses the three transformed IV-bar ROIs plus the fixed
label/frame ROI. Three compatible frames among the latest five form consensus,
while Unknown or conflicting evidence invalidates the active window. Animated
Pokémon models, particles and the central background are excluded.

`device-recover-inventory` only orchestrates captures, consensus calls,
service decisions, audited named actions and post-action polling. The service
owns Unknown-stop, unexpected-state-stop and action limits. An unchanged
post-action substate yields terminal `ACTION_NOT_OBSERVED`; no blind retry is
authorized. AppraisalIntro and AppraisalBars each authorize one normalized
`ExitAppraisal` tap at the documented left-middle target. Only verified
PokemonDetails authorizes Android Back to Inventory.

`device-continue-appraisal-intro` returns success without input when stable
bars already exist. Otherwise it requires stable intro ROI evidence, taps the
locator target exactly once and requires stable bars afterward.

### Guarded Inventory Search

`GuardedInventorySearch` owns the bounded OpenSearch, ClearSearch, EnterQuery
and SubmitQuery sequence. `InventorySearchVisualAnalyzer` verifies the search
surface, keyboard, query ink, clear control and a stable result-region
signature. An unobserved action terminates the workflow and cannot loop.

Ordinary search text crosses `IAndroidAutomationTransport` into
`PogoInventory.Device`. `AndroidInputTextEncoder` alone translates it to the
remote-shell-safe token used by Android `input text`; Submit is a separate
named `KEYCODE_ENTER` transport operation. CLI and Automation never construct
raw shell syntax.

### Guarded tag selection by name

`TagSelector` first discovers visible rows from their left-side marker geometry.
It then compares each row's name region with a named template in an ignored
device profile at bounded 0.94, 1.00 and 1.06 normalized height scales. A match
requires both an absolute confidence threshold and a second-best margin. Row
order and fixed row coordinates are never match inputs.

The CLI owns state-validated Menu and Done transitions, while `TagSelector`
owns read-only row, checkmark and Details-pill observations. The only mutation
is `SetExistingPokemonTag` against the matched row. It is omitted when the
requested state already holds or no confident name match exists. Selector
scrolling is profile-bounded and every action and postcondition is audited.
Details verification dynamically counts connected gray or colored pill
components in the tag section and requires the expected before/after delta.
This supports zero, one and multiple simultaneous tags without treating one
fixed tag color or location as authoritative.

```text
Android phone
    |
    | USB / ADB
    v
PogoInventory.Device
    |
    +--> discovery, metadata, battery and screenshots
    +--> allow-listed tap and swipe primitives
    |
    v
PogoInventory.Automation
    |
    +--> validated normalised control profile
    +--> screen-state checked navigation
    +--> identity-change verification
    +--> ordered evidence and checkpoint
    |
    +----------------------+
    |                      |
    v                      v
PogoInventory.Vision   Calcy / visual extraction
    |                      |
    +----------+-----------+
               v
       structured observations
               |
               v
       inventory database
               |
       +-------+--------+
       |                |
       v                v
  PvP analysis     collection rules
       |                |
       +-------+--------+
               v
      KEEP / REVIEW / DELETE plan
               |
               v
        exact-match tag executor

Final transfer remains manual.
```

## Project boundaries

### PogoInventory.Core

Owns Pokémon observations, decision policy, conservative duplicate logic and reports. It has no Android or image dependency.

### PogoInventory.Device

Owns all ADB execution.

Interfaces:

```text
IAndroidDeviceTransport
  ListDevicesAsync
  ReadMetadataAsync
  CaptureScreenshotPngAsync

IAndroidAutomationTransport
  extends IAndroidDeviceTransport
  TapAsync
  SwipeAsync
  EnterTextAsync
  SubmitAsync
```

The input interface contains only named text entry and submit in addition to
tap/swipe. It contains no arbitrary shell, arbitrary key event, location
control or destructive game action.

`AdbAndroidDeviceTransport` converts the two input methods to these fixed ADB command forms:

```text
adb -s <serial> shell input tap <x> <y>
adb -s <serial> shell input swipe <x1> <y1> <x2> <y2> <duration>
```

Higher layers do not receive the ADB runner.

### PogoInventory.Vision

Owns PNG decoding, normalised regions, fingerprints and fail-closed screen-state classification.

It has no dependency on ADB or automation.

### PogoInventory.Automation

Owns automatic traversal and evidence sequencing.

Responsibilities:

- validate the automation profile
- select and lock one authorised device
- lock screen geometry
- navigate only through named actions
- verify the state after every action
- verify item change independently from screen state
- write evidence and checkpoint atomically
- resume only from a matching last item
- stop on unsafe state or health condition

It does not know Pokémon species, IVs, PvP value or deletion rules.

### PogoInventory.Calibration

Retains fixture indexing, profile generation and acceptance reporting. The earlier manual privacy-promotion route remains available as a fallback, but automatic local bootstrap becomes the target path from the next milestone.

### PogoInventory.Cli

Commands include:

```text
analyze
device-snapshot
screen-detect
screen-fingerprint
inventory-scan
calibration-*
```

### PogoInventory.SelfTest

Runs deterministic package-free tests. The scripted Android transport emulates the state path and three distinct appraisal items.

## Automatic state machine

```text
Current state       Allowed action               Required next state
-------------       --------------               -------------------
InventoryList       TapFirstInventoryCard        PokemonDetails
PokemonDetails      TapDetailsMenu               PokemonMenuOpen
PokemonMenuOpen     TapAppraise                   AppraisalOpen
AppraisalOpen       SwipeNextPokemon             AppraisalOpen + changed identity
```

No further input is sent until the required state is observed.

`Loading` may be tolerated while polling. `Unknown`, `Popup` and `NetworkError` stop the run.

## Item-change verification

The automation profile contains a normalised `IdentityRegion` and fingerprint settings.

The current and previous fingerprints are compared with the same deterministic similarity function used by the vision layer.

```text
similarity < SamePokemonSimilarityThreshold
    => next item accepted

similarity >= SamePokemonSimilarityThreshold
    => keep polling or repeat the configured swipe

no change after MaxSwipeAttemptsAtEnd
    => end of inventory
```

This is a traversal identity only. Exact identity for tagging will later include species, form, CP, IV, date, moves and neighbour context.

## Persistence

### Phase 6C protection proofs

`PogoInventory.Core.Models.PokemonProtection` is owned by the canonical
observation layer. Its P0 fields are evidence-bound proof fields, never plain
booleans. `InventoryAnalyzer` treats Unknown and Conflicting P0 values as a
mandatory Review gate. `PogoInventory.Persistence` schema v4 stores the full
contract in `ProtectionJson` on both Observations and PokemonRecords in
addition to `ObservationJson`; this preserves source, frame ID/hash and proof
state across a reload. `PogoInventory.Semantics` accepts only host-provided
decoded frames whose ID/hash matches canonical item evidence. It currently
authorizes only the bounded favorite-star detector and species-reference P1
rarity derivation; all other markers fail closed as Unknown.

```text
<output>/
  inventory-scan-checkpoint.json
  captures/
    000001.png
    000002.png
    ...
```

The checkpoint records:

- run and profile identity
- device serial and geometry
- status and stop reason
- ordered items
- screenshot and fingerprint hashes
- complete input audit

Writes are atomic. Sequence numbers must be contiguous.

## Resume

A running checkpoint can resume only if the phone is still on `AppraisalOpen` for the last captured Pokémon and the identity fingerprint matches. The runner then swipes once and waits for a changed identity before capturing the next sequence item.

Completed and safely stopped checkpoints are immutable. A new output directory starts a new run.

## Version 0.7.0 additions

### Bootstrap layer

`PogoInventory.Bootstrap` coordinates the existing device, automation, calibration and vision layers. It may use only the existing named phone actions.

### Observation layer

`PogoInventory.Observations` owns the Calcy provider boundary and result model. Automation depends on this abstraction, not on a specific Calcy transport.

```text
Android screenshot
       |
InventoryAutomationRunner
       |
ICalcyObservationProvider
       |
CalcyObservation
       |
Checkpoint schema 2.0
```

The real adapter will be added only after the current phone and Calcy version are verified.

## Version 0.8.0: Calcy evidence boundary

```text
PogoInventory.Device
  IAndroidAppInspectionTransport
        │
        ▼
PogoInventory.CalcyProbe
  package/version parser
  evidence collection
  automatic one-item live check
        │
        ▼
PogoInventory.Observations
  ICalcyRawOutputSource
  profile-driven parser
  CalcyObservation
```

`PogoInventory.Device` is still the only assembly that executes ADB commands. The probe layer receives named text outputs and cannot issue arbitrary commands.

The live check composes the existing `InventoryAutomationRunner` with `CalcyProbeRunner`. It does not add a new phone input action.

The parser is deliberately separated from the source mechanism. A real source may later be logcat, another local text surface or visual overlay extraction. Only the mechanism proven on the fixed phone may be enabled.


## Version 0.9.0: provider verification gate

`PogoInventory.Verification` owns expected-versus-observed comparison, evidence hashing and the zero-false-Complete gate. A production provider selection is locked to the exact verification report and parser profile hashes.

## Version 0.10.1: cross-platform image pretest

```text
data/iphone-images/*.png
        │
        ▼
PogoInventory.ImagePretest
  package-free PNG decode
  geometry and orientation inventory
  SHA-256 and normalised fingerprints
  pairwise similarity and clustering
        │
        ▼
out/iphone-image-pretest/*
```

The image-pretest layer depends only on `PogoInventory.Vision`. It has no ADB, automation, Calcy or inventory-rule dependency.

The layer never modifies or copies its source screenshots. It produces metadata, hashes, similarities and cluster membership only.

An accepted iPhone pretest proves that real screenshots can pass through the visual plumbing. It does not validate Android coordinates, Android timing or Calcy extraction.

## Version 0.11.0: visual-region discovery

```text
data/iphone-images/*.png
        │
        ├── PogoInventory.ImagePretest
        │     visual clusters
        │
        ▼
PogoInventory.RegionDiscovery
  normalised grid
  luminance and edge metrics
  global and consecutive variation
  within-cluster stability
  between-cluster separation
  provisional candidate rectangles
        │
        ▼
out/iphone-region-discovery/*
```

The region layer depends on the image pretest and vision layers only. It does not depend on ADB, automation, Calcy or inventory decisions. Candidate labels describe measured visual behaviour and are not semantic Pokémon field recognition.

## Version 0.11.1: CLI namespace correction

The CLI imports the `PogoInventory.RegionDiscovery.Models` and
`PogoInventory.RegionDiscovery.Services` namespaces explicitly. This corrects
the compile failure in the command-line integration without changing the
region-discovery algorithm, reports, input boundary or safety model.

## Version 0.12.0: crop-atlas evidence layer

`PogoInventory.CropAtlas` consumes the accepted
`PogoInventory.RegionDiscovery` report and the original read-only screenshots.

It produces derived PNG crops and manifests under `out`. The project does not
perform device control and does not assign semantic Pokémon fields. Its only
decision is whether the current visual clusters have enough representative
evidence for a later semantic experiment.

## Version 0.13.0: semantic evidence review layer

The semantic evidence layer remains inside `PogoInventory.CropAtlas`. It
combines the accepted region report, crop-atlas report and read-only source
screenshots into derived per-case crops.

The output is a review package, not a provider. It has no device-control
dependency and cannot enable automated extraction. A later provider must consume
a populated truth manifest and pass the existing zero-false-Complete safety
pattern.

## Version 0.13.1: namespace correction

The semantic evidence layer reuses internal crop and JSON helpers from
`PogoInventory.CropAtlas.Services`. The nested semantic service namespace now
imports that parent service namespace explicitly. No architectural boundary
changed.



## Version 0.14.0: appraisal and phone preparation

`PogoInventory.Appraisal` contains normalised visual definitions, bar measurement, offline pretesting and read-only phone preparation. It references the Device layer for screenshot capture but exposes no tap or swipe operation. Device-adjusted profiles remain unverified until a later truth gate passes.

## Version 0.14.1: nullable candidate-IV correction

The appraisal analyzer represents an unavailable IV estimate as null and a
measured candidate as an integer from 0 through 15. Version 0.14.1 makes that
nullable contract explicit in the local measurement variable. No architectural
boundary changed.

## Version 0.14.2: consistent decoder diagnostics

All image-analysis stages now treat `ScreenVisionException` as a recoverable
per-file diagnostic when the surrounding acceptance gate still has enough
decoded evidence. Unsupported files remain traceable and cannot silently
become valid observations.

## Version 0.14.3: exception-pattern syntax correction

The appraisal pretest's recoverable decoder policy is unchanged. Version
0.14.3 only corrects the C# syntax used to express the existing list of
recoverable exception types.

## 2026-07-19 real-phone validation update

The connected OnePlus A6013 has now exercised the real validation path:

- `phone-prepare` produced a device-adjusted appraisal profile from a live
  appraisal screen.
- `phone-calibration-stability.md` recorded three appraisal cases with
  stable transforms, zero Complete observations and distinct IV triplets.
- `calcy-probe` confirmed `tesmath.calcy` version 3.44 and the read-only
  evidence surfaces used by the current probe boundary.
- `calcy-live-check` completed a one-item appraisal capture and then ran the
  same read-only probe path.

This confirms that the architecture's current read-only boundaries still hold
on a real phone while the verified provider gate remains closed.

## 2026-07-21 dynamic identity tuning

`PokemonDetailsIdentityAnalyzer` keeps full screenshot SHA-256 as evidence and
uses a separate stable fingerprint. The Android profile searches the observed
Details tag band for bounded pill-shaped components, then detects a long
near-gray divider below the mutable section. The stable lower ROI is sampled
relative to that divider and is deliberately short enough to exclude fixed
bottom navigation controls. The synthetic fixture covers zero, one and two
tags with shifted lower content and passes with 155/155 package-free tests.

The real captured zero/one/two-tag states produce tag counts 0/1/2. Their
zero-tag versus tagged similarity is 0.9815 against the configured 0.965
threshold, while one- and two-tag states share the same fingerprint. This is
evidence for the guarded identity path, not a production provider gate or a
real-phone Task 4 approval. A local five-frame Inventory capture is rejected
as Unavailable rather than interpreted as Details.

## 2026-07-21 deterministic navigation safety validation

`PogoInventory.Cli device-validate-navigation-safety` is a validation shell
over `AndroidVerifiedInventoryNamedOperations`. The host remains the owner of
locators, state detectors, authorization, recovery and transport calls. The
optional `NavigationSafetyTraceRecorder` observes host captures and records
phase-aligned evidence; it cannot send input. Post-input evidence is completed
with bounded screenshot reads only, and POSTCONDITION is written after five
frames. The command is limited to read-only navigation and does not establish
real-phone acceptance until a manual safe-state precondition and bounded phone
run pass.
# Phase 6B real semantic result path

The additive `PogoInventory.Streaming.Semantics.Real` package owns the
persistent EasyOCR JSON-lines worker and fail-closed candidate analyzers. The
results runner feeds captured BGRA frames through `SemanticShadowRunner`,
keeps screenshot references offline-only, emits crops and per-item evidence,
and always reports `AuthorizesPhoneInput=false` / `InputCommandsSent=0`.
EasyOCR queue capacity is one, timeouts are surfaced, and no retry turns an
unknown result into a known result. The real-phone capture remains owned by the
existing named device operation; the semantic package adds no Device or
Automation input authority.
# Minimal stream-first gated Pokemon reader

`device-stream-read-pokemon` owns one `ScrcpyReadOnlyVideoTransport`,
`FfmpegBgraVideoFrameDecoder` and `StreamingFrameSource` for the complete
read. Named Android operations are limited to setup/navigation and the
allowlisted carousel swipe; semantic and gate evidence comes from stream
leases. `TemporalGateEngine` requires three distinct stable frames with the
profile's Header, AppraisalPanel and three IV-bar regions. The canonical
semantic analyzer validates every observation against both frame id and
evidence hash, deduplicates repeated frame observations, and leaves missing
or conflicting fields Unknown. Each item writes raw frame PNGs plus JSONL,
CSV, summary and auto-refresh HTML reports. VLM is not on the default path.
