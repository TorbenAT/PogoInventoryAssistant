# Continuation prompt

## Round-3 checkpoint (2026-07-24 evening) — frame diet shipped; gate-4 pilot blocked by a MEASURED detector defect with a ready fix brief

Commits pushed: `5f99c7f` (frame diet: IV analysis shares the identity frames + ordinal>1 tag probe dropped — 12→7 captures/item, measured 13.0-14.3 s/item on the real phone), `29b32d7` (shared `CleanupObservationStatusRule` — Complete never survives Unknown CP/IV — plus bounded `VerifyGameplayMapSettledAsync`), `45c151a` (exit-settle verification: shared `VerifySettledStateAsync` core; the exit chain continues on late Details settle). Suite 243/243. All task-scoped reviews approved.

**BLOCKER for the gate-4 pilot (root-caused, measured, fix designed):**
Run1 stopped `CLEANUP_START_RECOVERY_BLOCKED`. Measured on 5 real frames
(`local-data/validation/reid-pilot-2x50/run1/evidence/0006..0010`):
`VisualControlLocator.LocateDetailsPageTopology` (VisualControlLocator.cs:112)
requires modelArea >= 0.15 but the warm sunset-bokeh Details background measures
0.103-0.158 — the state flickers Unknown/Details so no 3-consecutive consensus
ever forms (exit verify + canonical-close precondition both starve). The blue-
header fallback measures 0.000 (assumes a blue background). The X-button locator
is fine (0.975 on every frame). Secondary: `IsMenuBackground` 2-of-3 fallback
false-fired MainMenu on bokeh (frame 0006).
**Task H OUTCOME (commit `c42f620`, pushed, 244/244): partial — and the brief's
relaxation is DISPROVEN, do not implement it.** The safe half shipped:
`IsMenuBackground` tightened 2-of-3 → 3-of-3 (frame 0006's MainMenu false
positive fixed; verified against 2843 real corpus frames). The proposed
double-corroboration branch (modelArea>=0.08 && cpArea>=0.50 &&
detailsPanel>=0.50) was withheld after corpus verification: ~200 real
MainMenu/Appraisal/Inventory/GameplayMap frames satisfy those same floors,
some numerically indistinguishable from genuine Details on exactly those three
signals — and two call sites (`CaptureCleanupIdentityAsync`,
`CaptureIndependentDetailsFramesAsync`) treat the topology result as
PokemonDetails with no further gating. Frames 0007/0008/0010 still classify
Unknown; the gate-4 flicker is NOT yet fixed. Full analysis:
`.superpowers/sdd/task-H-report.md`.
**Recommended next approach (controller decision needed):** add a FOURTH
orthogonal corroboration signal to the relaxed branch — the canonical-close
X-button locator scores 0.975 on every failing Details frame and is exactly
the strong discriminator the three area signals lack (verify it scores low on
the ~200 colliding corpus frames first); alternatively gate the two unsafe
call sites. Note: the task-scoped review of `c42f620` was skipped at session
end — it is a one-line strictly-tightening change with a boundary test, but a
fresh session may re-review cheaply.
After the detector fix is proven on the 5 evidence frames via
`game-state-detect-image` (all must classify PokemonDetails), rerun the
gate-4 pilot 2×50 (~11 min/scan):
run1/run2 fresh dirs under `local-data/validation/reid-pilot-2x50/`, program
restart between, then `analyze-reidentification --database-a ... --database-b ...`.
Phone was left untouched on the Fletchling Details screen (sunset background) —
the first recovery after the fix is itself the live test.
SDD ledger: `.superpowers/sdd/progress.md`. Plan file:
`~/.claude/plans/til-n-ste-iteration-st-r-cryptic-chipmunk.md`.

## Speed iteration checkpoint (2026-07-24) — SHIPPED, phone-measured

Focused on run speed (Torben found runs very slow). All work via Superpowers
subagent-driven-development with Sonnet subagents; four commits pushed to `main`
(`0c1d884`, `7c70862`, `76362c6`, `a38a25e`, `d255ad6`), self-tests 223/223.

- **Root cause + fix (`76362c6`):** `ReadTagObservationAsync` waited for
  `PokemonDetails` while the phone was in the Appraisal carousel (every item
  after the first), burning the full 12 s `WaitForStateAsync` timeout (~34
  captures) per item and then reading tags off the wrong frame. Now probes the
  state first and short-circuits with `TagReadSkipped:AppraisalCarousel`
  (speed AND correctness fix).
- **Timing instrumentation (`0c1d884`+`7c70862`):** `OperationTimingCollector`,
  `TimingReport`, `TimingAndroidAutomationTransport`; each run now writes
  `timing-report.json` + a `## Timing` section (null-collector default = zero
  behavior change).
- **Pre-swipe reuse (`a38a25e`):** reuse the just-confirmed stable AppraisalBars
  frame as the pre-swipe reference (−3 captures/item); post-swipe verification
  untouched.
- **Final-review fixes (`d255ad6`):** honest per-item timing (EndItem in a
  try/finally that includes the advance), run-scoped wall clock (MarkRunStart),
  and a fail-closed `requiredState: Appraisal` gate on the skipped-pre-swipe
  swipe authorization.

**Measured WiFi vs USB A/B (12 items each, OnePlus 6T, both 12/12 green,
integrity ok, identical species multiset, all REVIEW):**
baseline ~41 s/item → **WiFi 22 s/item, USB 18 s/item**. Capture-transfer is the
dominant cost (58 % WiFi / 51 % USB); mean `screencap -p` 1021 ms WiFi / 733 ms
USB. USB cuts 28 % per capture, 18 % wall clock, no accuracy regression →
**adopt USB for long runs**, but the per-frame cost is on-device PNG encode (not
link), so the next structural lever is capture COUNT/format, not transport.
Evidence: `local-data/validation/ab-usb/comparison.md`.
CLI note: must pass `--adb 'C:\Data\PokemonGo\tools\platform-tools\adb.exe'`
(absolute path; bare `adb`/relative fails AdbNotFound). USB serial `01f5c502`.

### Addendum (2026-07-24 evening): USB dropped, adb path fixed, full attribution

- **USB is dead for long runs**: the phone loses charge while running over USB
  (battery dropped during the A/B). Controller decision: WiFi only from now on.
- **adb path fixed permanently** (`052b59d`): `--adb` is no longer needed —
  `AdbPathResolver` auto-resolves the bundled `tools\platform-tools\adb.exe`
  (Path.GetFullPath from the repo root, PATH fallback, `--adb` still overrides).
- **Timing is now fully attributed** (`044165d`): swipes/taps measured as
  `InputGesture`, every capture/input stamped with its enclosing named operation,
  per-operation capture counts, per-item `ResidualMilliseconds`. Verified run
  (12 items WiFi, no `--adb`, 12/12, integrity ok):
  steady-state item ≈ 19,8 s = 11,9 s captures (~12 × 1025 ms) + 1,2 s input +
  2,1 s fixed delay + 0,7 s OCR + 3,8 s analysis. Nothing unexplained.
  Evidence + visual overview: `local-data/validation/timing-full/`
  (timing-oversigt.html). Suite 231/231.
- **Next speed lever** (confirmed by the numbers): capture PRICE, not transport —
  ~1 s/frame is on-device PNG encode in `screencap -p`. A raw-framebuffer
  capture path (if it can be made safe/verified) hits all ~12 frames/item at
  once; theoretical floor with current price is ~16-17 s/item.

**Still open for next iteration (design done, controller decisions taken —
see plan `til-n-ste-iteration-st-r-cryptic-chipmunk.md`):**
1. Runner-status defect (`CleanupProofRunner.cs:218` marks Complete despite
   Unknown CP/IV) — extract shared `CleanupObservationStatusRule` from
   `CleanupEvidenceReprocessor.RecomputeObservationStatus`. (The A/B runs'
   `CompleteItems 12` reflects this defect; recommendations correctly stay
   REVIEW, so fail-safe.)
2. Final-map verification timing — bounded input-free `VerifyGameplayMapSettledAsync`.
3. Coverage→accuracy requirement restatement (Torben decided 2026-07-24) +
   reclassify gate 3 GREEN.
4. Gate 4 pilot 2×50 (`analyze-reidentification`); formal ID-006 (≥200) stays
   blocking.
5. Capture-count/format reduction (raw framebuffer vs PNG) if it can be made safe.

## Evidence-machine result checkpoint (2026-07-21 aften) — CONTROLLER STOPPED AFTER GATE 3

Gates 0-3 of the semantic integration checkpoint were executed on the
evidence machine. Commits `401d2cd`, `1924f39`, `bfb6ed6`, `c0af853` are
pushed; self-tests are 208/208.

- Gate 0 green (201/201 at start).
- Gate 1: `ocr-header-spike` initially read 0/60 — root cause was a
  BitmapTransform scale-before-crop defect in `WindowsMediaTextRecognizer`
  (fixed, pure-helper tested). After ROI tuning: species 60/60 frames.
  WinRT CP consensus produced one FALSE value (29 for a real CP129 —
  two near-identical frames dropped the same thin "1"). Four hardening
  experiments measured; binarization/3x-upscale/ROI-jitter all regressed.
- Controller decision: Tesseract (`TesseractOCR` 5.5.2, tessdata-best)
  replaced WinRT as the cleanup-flow engine (`--engine winrt` remains in
  the spike command only). Gate 2 rerun: species 19/20, CP 16/20, 100 %
  accuracy on everything extracted, zero false values, original database
  untouched, `RowsWithQueryAsSpecies` 0. The reprocessor now recomputes
  ObservationStatus (Complete requires species+CP+all IVs; else Partial).
- Gate 3 (`age0-1825`, item-limit 50 — limit range extended to 6-50):
  50/50 items captured, 36 distinct species, species 48/50, CP 41/50,
  IV 47/50, zero query-as-species, zero destructive/tag actions, SQLite
  integrity ok. Formally RED on the >=48/50 species+CP+IV requirement.
  STRUCTURAL finding: the misses are dominated by large models (Hoopa x2,
  Enamorus, Tyranitar) physically occluding the CP header — a UI fact,
  not an OCR defect; header OCR coverage caps around 82-90 % on a real
  inventory. Accuracy remained 100 % (unknowns stay fail-safe REVIEW).
  Evidence: `local-data/validation/cleanup-value-proof/age0-1825-50-semantic`.
- The phone ended on a verified real GameplayMap, but the run reported
  `FinalMapNotVerified:PokemonDetails` — the final verification ran before
  the recovery/exit chain finished. Open defect.

Open items for the next iteration (controller decisions needed):
1. `CleanupProofRunner` still marks rows Complete despite Unknown CP/IV —
   mirror the reprocessor status recompute in the live runner.
2. Final-map verification timing defect above.
3. Gate 4 (double-scan re-identification, >=99 %, ID-006) was NOT run and
   still blocks all cleanup/tagging work.
4. Decide whether the >=19/20 / >=48/50 CP coverage requirements should be
   restated as accuracy gates (zero false values) plus a documented
   occlusion-driven coverage cap.

## Semantic integration checkpoint (2026-07-21) — HANDOVER TO EVIDENCE MACHINE

Waves 1+2 of `docs/MINIMAL_EFFORT_PLAN.md` are merged and offline-green at
201/201 self-tests. The semantic core is implemented AND wired into the
cleanup flow:

- `PokemonHeaderAnalyzer` (species/CP/nickname, multi-frame consensus) +
  `WindowsMediaTextRecognizer` + `ocr-header-spike` command.
- `CleanupProofRunner` no longer stores the search query as species (guarded
  regression: broad query as species now throws). Species evidence is
  QueryDerived (exact single-species query) / Automated (OCR consensus) /
  Unknown. CP and IVs become Automated on >=2-frame consensus;
  ObservationStatus upgrades to Complete only when species, CP and all three
  IVs are known. The Calcy Verified gate is untouched and parked.
- `analyze-cleanup-evidence` reprocesses an existing cleanup-proof.sqlite
  offline into a NEW database copy + reports + species-cp-coverage.json.
- Reference data (`data/reference/species-reference.json`, 1025 species),
  `--policy` / `--species-reference` CLI options, `RulePolicyLoader`.
- `SemanticIdentityKey` (schema v3), `SemanticIdentityMatcher`,
  `analyze-reidentification` for the double-scan acceptance.

All remaining work requires the machine that holds `local-data` and the
phone (ADB `C:\Data\PokemonGo\tools\platform-tools\adb.exe`, device
`192.168.1.185:5555`, OnePlus 6T). Run in this order; stop at the first
failed gate:

1. OCR spike against the accepted 20-item evidence frames:
   `dotnet run --project src\PogoInventory.Cli -- ocr-header-spike
    --input local-data\validation\cleanup-value-proof\appraisal-carousel-20\<frames dir>
    --screen appraisal --out local-data\validation\ocr-spike`
   Gate: >=19/20 species and CP. Below target: tune `HeaderAnalysisProfile`
   ROIs (docs/HEADER_OCR.md) using the raw line bounds in the spike report;
   do not proceed on red.
2. Offline reprocess of the accepted 20-item database:
   `analyze-cleanup-evidence --database <appraisal-carousel-20>\cleanup-proof.sqlite
    --evidence-root <appraisal-carousel-20> --out <...>\appraisal-carousel-20-semantic`
   Gate: species >=19/20, CP >=19/20, rowsWithQueryAsSpecies = 0, SQLite
   integrity ok. The original database must remain untouched.
3. One bounded real-phone regression, query `age0-1825`, limit 50, through
   `device-run-cleanup-proof` (now with OCR + optional --policy). Gate:
   >=48/50 species+CP+IV, zero query-as-species rows, zero destructive/tag
   actions, final GameplayMap.
4. Double-scan re-identification: scan the same stable scope twice (program
   restart between), then
   `analyze-reidentification --database-a <run1>.sqlite --database-b <run2>.sqlite --out <dir>`
   Gate: >=99 % re-match, zero false merges. This gate blocks all cleanup/
   tagging work (kravspec ID-006).
5. Only after gates 1-4: resume/chunking for the cleanup flow (plan step 5)
   and the manifest-to-tag pipeline (plan step 7).

## Persistent Appraisal carousel checkpoint

The cleanup-proof implementation now keeps Appraisal open while advancing
through ordinary Pokémon. Build and self-tests pass 163/163. Commit and push
this increment, then run one bounded `age0-1825` / item-limit 20 acceptance in
`local-data/validation/cleanup-value-proof/appraisal-carousel-20`. Stop at the
first runtime defect, preserve SQLite rows and reports, and do not patch/rerun
automatically.

Acceptance completed: 20/20 real items, 19 appraisal swipes, zero Details
swipes, 20 unique fingerprints, SQLite integrity `ok`, and final
`GameplayMap`. No further carousel work is required in this iteration.

## Long database acceptance checkpoint

The fresh `age0-1825` / 20-item real run safely stopped after four Complete
rows at `CursorProgression:Unknown`. SQLite integrity is `ok`; all four rows
and reports were retained after database reopen. Do not patch and rerun in
this iteration. The precise remaining runtime defect is cursor progression
after ordinal 4, with the final phone screenshot visually PokemonDetails but
the detector returning Unknown. Evidence is under
`local-data/validation/cleanup-value-proof/long-age0-1825`.

## Current checkpoint: canonical close accepted; value proof blocked by query

Canonical close is pushed through `5a5ffc1`; the real phone diagnostic and
Inventory, Details and Appraisal cycles all returned safely to GameplayMap.
The required direct value-proof query `pidgey&age0-365` returned no Pokémon,
so the run safely stopped before Details with SQLite integrity `ok` and zero
persisted rows. Do not claim a value proof or change the requested query
without a new controller decision. Evidence is under
`local-data/validation/cleanup-value-proof`.

## Next action: rerun canonical-close diagnostic once

The first diagnostic stopped with zero input after visually showing the real
Details canonical X; the locator's single-radius model missed the Android
button scale. The focused repair now checks bounded scale-normalized radii and
still requires the full visual evidence set. Build/tests are 162/162. Commit
and push this repair, then rerun the direct diagnostic once before the three
program-created state cycles and SQLite value proof.

## Next action: canonical-close phone acceptance

The current code replaces the state-specific startup graph with
`CanonicalCloseUnwindService`. Run the bounded diagnostic unwind from the
phone's current reversible state, then the three program-created Inventory,
PokemonDetails and Appraisal acceptance cycles, and only then run the SQLite
value proof. The canonical close target must be visually located in three
frames and freshly revalidated before every single tap. No fixed coordinate,
Android Back fallback or automatic retry is allowed. Offline self-tests are
162/162; no real-phone acceptance is claimed yet.

## Next action: bounded autonomous value proof

The code checkpoint includes a focused startup stability repair. The previous
direct phone attempt stopped before input because strict Details evidence did
not reach consensus; `start-state-recovery.json` records `RecoveryInputCount=0`.
The repair saves recovery frames and allows three same-state ordinary Details
frames while retaining strict appraisal ROI rules. Build and self-tests are
162/162. Run the single direct value proof once; do not manually prepare the
phone, and do not claim acceptance unless the recovery report and reopened
SQLite proof satisfy the acceptance fields.

## Autonomous cleanup start recovery checkpoint

`device-run-cleanup-proof` now normalizes supported reversible starting states
internally through canonical close unwind; no manual GameplayMap preparation
is required. The unwind is bounded to five visually verified close inputs and
stops on missing controls, unsafe ambiguity, loops or unchanged post-state.

Run the one bounded real value proof directly from the phone's current state.
Do not claim phone acceptance unless the recovery report and reopened SQLite
proof satisfy the acceptance fields. Preserve local evidence under ignored
`local-data/validation/cleanup-value-proof`.

## Cleanup proof implementation checkpoint

Commit 1 fixes the wrong-screen test packaging defect with package-free
synthetic fixtures and is pushed as `e3f8f25`. Commit 2 is the pending green
cleanup-proof pipeline increment: `device-run-cleanup-proof` accepts an exact
species query, item limit 6-20, SQLite path, output directory and
`--continue-on-partial`; it captures bounded Complete/Partial/Unresolved
identity evidence, persists observations transactionally, closes and reopens
SQLite before policy analysis, and writes database-derived recommendation
reports. Offline self-tests are 160/160. No real phone input has been sent for
this cleanup proof yet; begin only from three stable GameplayMap frames.

The current offline checkpoint repairs wrong-screen action authorization.
Before any phone input, Torben must manually press CANCEL on the currently
recorded Power Up confirmation and leave the phone in GameplayMap or
unfiltered Inventory. Do not send phone input before that manual step.

MainMenu -> Inventory is guarded by three stable typed MainMenu frames plus a
fresh pre-tap revalidation. Details, visual Details fallback, stale frames,
conflicting topology and destructive confirmation surfaces deny input. The
interlock covers taps, search text/submit, Back and cursor swipes and records
the authorization evidence. Offline self-tests are 158/158. Do not claim
real-phone acceptance until the bounded three-cycle safety check is run.

Runtime repair is implemented but not yet committed: guarded appraisal uses
visual Intro/Bars transitions, cursor swipes require observed transition
evidence plus three independent post frames, equal fingerprints are allowed,
and ControlledStopped checkpoints resume through overlap without duplicating
an ordinal. Offline self-tests are 157/157. Commit as
`Repair verified Android sequence runtime`, then perform the bounded ADB
preflight and phone acceptance.

The Android named-operation host and cursor sequence are implemented in
`AndroidVerifiedInventoryNamedOperations` and exposed as
`device-run-index-sequence`. First-card opening is single-use; normal items
advance by one guarded Details swipe, and checkpoints include cursor overlap
data plus structured tags. Resume stops before any new swipe when overlap does
not match. Tag mutation remains disabled and false by default for the first
real acceptance. Offline self-tests are 156/156. Real-phone acceptance is not
claimed until the bounded 3-item and resume runs are actually executed.

Latest real-phone attempt is blocked: `tools/platform-tools/adb.exe` discovered
no authorized device and the bounded reconnect to `192.168.1.185:5555` failed
with Windows socket error 10013. Do not claim phone acceptance until ADB is
available again.

Task 5 now has an offline `VerifiedInventoryTaskSequence` contract with
checkpoint/resume, Partial preservation plus bounded continuation after a
verified Inventory restore, and fail-closed tests at 156/156. Bind it to the
existing validated named Android operations before any real-phone run; keep
apply-tags false by default and never auto-apply AI-Delete.

The guarded appraisal recovery is accepted offline and on the real OnePlus
A6013. Full-screen animation is excluded from stability; intro and bars use a
three-of-five ROI consensus. `ExitAppraisal` performs one documented
left-middle tap per appraisal substate, and Android Back is authorized only
from PokemonDetails. Three complete cycles passed with zero Unknown states,
zero wrong states and zero Back actions on AppraisalBars. Build passes and
144/144 self-tests pass.

Generic Inventory Search is accepted. The caller supplies ordinary text,
`PogoInventory.Device` owns escaping and Enter, and the guarded workflow
verifies Open, Clear, Enter and Submit transitions. Two real rounds of all five
required queries passed and ended with a verified clear. Build passes and
146/146 self-tests pass.

Generic tag selection by name is accepted. It uses geometric row discovery,
normalized multi-scale device templates, confidence and margin gates, bounded
scrolling, checkmark plus Details-pill verification, and zero mutation for a
missing match. Two real Trade add/remove cycles passed on Ekans CP616 with zero
wrong selections. Build passes and 148/148 self-tests pass.

Continue with dynamic-tolerant Pokemon identity. Keep full screenshot SHA-256
strictly as evidence integrity. Build a separate stable multi-ROI fingerprint
that excludes the animated model, particles, dynamic background, status bar and
temporary overlays, and assign every observation a run-scoped ordinal instance
ID even when two stable fingerprints are identical.

Task 4 must also tolerate mutable Details layout. Real Task 3 evidence proves
that zero, one and two tags produce different full screenshot SHA-256 values
and move weight/height plus all lower content vertically. Tag names and pills
are mutable observations, never identity. Detect the tag section dynamically
and align stable ROIs to Details anchors/content after the section; do not
ignore one fixed tag rectangle. No Task 4 implementation is included in the
Task 3 checkpoint.

Task 4 implementation now exists locally: `PokemonDetailsIdentityAnalyzer` and
`identity-fingerprint` provide separate evidence hashes, stable fingerprints,
dynamic tag metadata, three-frame consensus and ordinal instance IDs. The
tuned profile passes 156/156 tests. Consensus requires at least three compatible
usable frames for Complete; one/two frames are Partial, and CLI exit codes are
0/2/3 for Complete/Partial/Unavailable. Three real five-frame Details groups are
Complete; one local group is Inventory and is Unavailable. The captured
zero/one/two-tag states count 0/1/2 and the zero-tag versus tagged similarity
is 0.9815 at a 0.965 threshold. Keep Task 4 real-phone acceptance PARTIAL
until a broader verified set is available; do not claim approval.

Use this after the 2026-07-19 real-phone validation run.

I am building Pogo Inventory Assistant in C# and .NET 8.

Open the repository and read `PROJECT_STATE.md`,
`docs/GUARDRAILS.md`, `docs/IPHONE_APPRAISAL_PRETEST.md`,
`docs/ANDROID_PHONE_PREPARATION.md`,
`docs/CALCY_PROVIDER_VERIFICATION.md` and
`docs/AUTOMATIC_NAVIGATION.md`.

Current version: 0.14.3.

Accepted:

- 23 decoded real iPhone screenshots
- four labelled visual clusters
- normalised appraisal bar definitions
- automatic translation and scale fitting
- real phone 3-item appraisal stability with zero Complete observations
- real Calcy probe on the connected OnePlus A6013
- real Calcy live-check on the connected OnePlus A6013
- candidate IV estimates only
- zero Complete results from unverified profiles
- read-only Android `phone-prepare`
- 138 self-tests

First verify that the repository stays green after the real-phone validation update.

Next milestone on the fixed Android phone:

1. Collect twenty real appraisal truth cases on different Pokémon.
2. Keep the generated device profile and stability report local.
3. Verify a parser profile only after a real output format is proven.
4. Require zero false Complete observations before selecting the visual
   appraisal provider.
5. Add no new phone input action unless it is one of the existing four named
   actions and remains state validated.

## Current Android sequence checkpoint

Commits `8151add`, `383e60f` and `67e6bb2` are pushed on `main`. Build and
self-tests are green at 157/157. Real-phone Task 7 (three items) and Task 8
(controlled stop/resume) passed with no tag or destructive actions. Task 9
stopped fail-closed after three items when one guarded progression swipe had
no observed effect; do not issue a blind retry. The next concrete milestone is
to improve bounded diagnosis of the age0-365 end-of-filter transition, then
rerun only Task 9 after the transition evidence is explicit. Final phone state
was verified as GameplayMap. This remains sequence-host acceptance evidence,
not unrestricted appraisal-provider approval.

## Current cursor checkpoint

The changed-identity cursor fallback is offline green at 157/157. The first
`age0-1825` real run recorded four items and accepted one missed transient as
`SUCCESS_CHANGED_IDENTITY` after three stable post frames. A later bounded
attempt reached a destructive Power Up confirmation screen; no further phone
input was sent. Do not retry the phone until the screen is manually returned to
a safe map or unfiltered Inventory state. Do not implement semantic species,
CP or IV extraction in the next iteration.
# Current continuation checkpoint

The permanent deterministic navigation safety command is implemented as
`device-validate-navigation-safety`. It requires three GameplayMap frames,
uses the existing named Android host, records phase-aligned action traces and
is green offline at 159/159 tests. Three bounded real-phone cycles passed on
the authorized OnePlus A6013 with GameplayMap final state. Do not start a
10-item sequence or semantic extraction in this checkpoint. Keep the phone
in GameplayMap or unfiltered Inventory, send no Cancel automatically, and
stop on any unsafe or unresolved postcondition.
