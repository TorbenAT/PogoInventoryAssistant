# Project state

## Persistent oldest-first frontier (2026-08-01, in progress)

SQLite schema v5 now contains restartable `WorkBuckets`, `WorkItems` and
append-only `WorkAttempts`. Read-only live probes found no 2016--2019 results,
then established 2020 as the first non-empty year (1025 results).
`year2020&1` isolated one favorite Bulbasaur; its `!favorite` complement was
empty. No tag or destructive action has been sent.

## Phase 6C protection enrichment (2026-08-01)

Phase 6C is implemented offline-first but is **PARTIAL**, not a production
truth gate for every P0 field. `PokemonProtection` is the canonical,
evidence-bound contract: `Known(true)`, `Known(false)`, `Unknown` and
`Conflicting` remain distinct. Any non-Known P0 field blocks aggressive cleanup
classification. Protection is serialized explicitly through schema version 4
and is also retained in the canonical observation JSON.

The read-only `replay-stream-protection` command hash-verified all 600 saved
frames from the canonical 120-item run and sent zero phone inputs. It produced
Favorite `Known(true)=46`, `Known(false)=74`, `Unknown=0`, `Conflicting=0`.
Shiny, Costume, SpecialBackground, Lucky, Shadow and Purified remain Unknown
for all 120 items because no separately proven visual truth rule has been
accepted. Legendary/Mythical/Ultra Beast are only reference-derived when
species is Known. No Phase 7 action, tagging, delete or transfer work occurred.

## Offline reference-safe species polish (2026-08-01)

No phone run was made. The new read-only `replay-stream-species` command
hash-verified and re-read all 600 saved PNG evidence frames from final run
`stream-20260731T195333664Z-bc04d28b23614d6f`. It changed no source artifact.
The reference-safe suffix rule accepts only `100`/OCR `l00` ratings or pure
punctuation and still requires a unique reference match; free text, other
ratings and ambiguous terminal completions remain Unknown. The replay raised
Species Known from 114/120 to 119/120 (Snover, Karrablast, two Shelmet and
Joltik), with 0 false-Known contradictions and species conflicts unchanged at
0. Phase 7 remains NOT STARTED; this is a clean baseline preparation only.

Stream reports now call the bounded-ring-buffer count `FramesEvicted`, not
`FramesDropped`; it is retention eviction and must not be interpreted as
decoder or transport loss.

## Final continuous 120-item proof run (2026-07-31)

The 19-item conclusion remains rejected: the account contains 1857 Pokémon.
The final clean stream run `stream-20260731T195333664Z-bc04d28b23614d6f`
used one scrcpy/H.264/FFmpeg/BGRA stream session on
`192.168.1.185:37877` and completed 120/120 requested items. Integrity is
PASS: 120 unique item fingerprints, 119 verified progressions, 600 distinct
semantic evidence frames, 120 report rows, no broken evidence links,
119 named progression swipes, zero semantic input, and Clean shutdown with
zero leases. Coverage is Species 95.00%, CP 85.00%, IV triple 92.50% and
Complete 74.17%; all declared final-run minimums pass.

Thirty final-run frames were visually reviewed, including items 1-10, 20,
30, 40, 50, 75, 100, the CP-conflicting item, five Unknown cases and five
long-settling cases. No false-Known species, CP or IV triple was observed;
ambiguous readings stayed Unknown or Conflicting. Final evidence is local and
ignored under `local-data/validation/stream-reader-final-120`.

## High-similarity Appraisal handoff correction (2026-07-30)

The reported 19-item inventory limit was false. The Inventory screen showed
1857 / 1925, and saved evidence proved that the twentieth swipe changed from
Pikachu CP 88 to Pikachu CP 129. The five regional 64-bit pHashes still
averaged 0.9875 similarity, so the settling evaluator classified every stable
item-20 frame as the previous item.

The handoff now requires an observed post-action transition before any
changed-item candidate can arm. A stable candidate still at or above the
visual same-item threshold requires a Known semantic difference from the
previous item; Unknown values never count. The real-phone `cp10-` checkpoint
completed 25/25 with 25 unique item fingerprints, 24 verified progressions,
75 distinct evidence frames, zero broken evidence links and zero semantic
phone input. Item 20 was proven by changed Attack, Defense and HP IV values.
The persistent-observer 25-item run exposed one lease at report time; disposing
the observer before stream shutdown was then proven on a 3/3 phone checkpoint
with Clean shutdown and zero leases. The final 100+ threshold remains pending.
Focused builds pass with zero
warnings/errors and the package-free suite passes 266/266. The full required
script suite also passes after restore was allowed to read the user-level
NuGet.Config. The no-argument phone-preparation wrapper cannot find `adb` on
PATH; its canonical-ADB rerun completed read-only and retained the expected
Candidate/unverified readiness state.

## Stream reader settling handoff (2026-07-29)

`feature/stabilize-stream-reader-10` replaces the immediate post-swipe gate
with a stream-only, eight-second settling handoff. It applies a fresh frame-id
and capture-time barrier, evaluates only the calibrated required regions,
requires three distinct stable AppraisalBars frames, and checks a stable
region fingerprint after swipes. Transition failures are counted and ignored
until timeout. The reader reports requested/completed items, run status,
actual carousel-swipe count, handoff evidence and stop reasons.

Local build and 258/258 self-tests pass. Real-phone acceptance is blocked
before stream startup because `192.168.1.185:5555` is no longer present in ADB
device discovery. No setup or carousel input was sent in this attempt; 3- and
10-item results remain NOT RUN for this revision.

## Minimal stream-first gated Pokemon reader (2026-07-28)

The `feature/stream-gated-pokemon-reader` implementation adds the CLI command
`device-stream-read-pokemon`. It starts one scrcpy/FFmpeg stream, gates
AppraisalBars on Header/AppraisalPanel/AttackBar/DefenseBar/HpBar, requires
three distinct stable evidence frames, and uses the canonical semantic core
with Tesseract plus the existing appraisal analyzer. Evidence PNGs, JSONL,
CSV and auto-refresh HTML are written under ignored local validation paths.
No VLM, Calcy, tagging, transfer or destructive action is used.

Real-phone 3-item acceptance produced 3/3 items and 9/9 distinct evidence
frames: 2/3 complete; one CP remained Unknown under fail-closed consensus.
A 10-item run stopped at item 2 with `MotionTooHigh`; a bounded 8-second
post-swipe gate window was added, but the follow-up stopped at item 1 with
`SharpnessTooLow` while the phone was not in a stable capture state. The
10-item acceptance is therefore NOT GREEN and must be repeated after a
deterministic stable-state handoff is added. Phase 7 remains NOT STARTED.

## Canonical semantic core and stream capture (2026-07-28)

`feature/canonical-semantics-stream-capture` adds the pure
`PogoInventory.Semantics` item contracts and shared fail-closed consensus
helper. Cleanup IV consensus uses that helper. `VisualFrame`, `FrameBarrier`
and the tested arbitrary-stride BGRA-to-RGBA bridge are in the pure streaming
semantics project. Full self-test is 253/253. Real-phone baseline is blocked
by the existing canonical close postcondition in `PokemonDetails`; no forced
input was introduced. Stream A/B remains pending.

## Phase 6B real semantic results sprint (2026-07-28)

The verified replay covers 30 rows / 150 fields with FalseKnown=0,
FalseComplete=0 and InputCommandsSent=0. The 3-item named-operation phone
capture completed, but live semantic acceptance remains blocked: the provider
was not attached to the capture stream and the replay recorded one EasyOCR
shadow timeout. The 10-item pilot was not run. Phase 7 remains NOT STARTED.

## Current canonical status (2026-07-28)

Current canonical branch: `main`; consolidation merge commit is `192d1c7`.
Streaming Vision Phases 1-5: ACCEPTED. Details gate: ACCEPTED. Appraisal
gate: ACCEPTED. Phase 6A technical benchmark: ACCEPTED. Ollama provider:
DIAGNOSTIC/CANDIDATE ONLY. Phase 6B shadow foundation: ACCEPTED. Real
semantic accuracy: NOT MEASURABLE. Phase 7: NOT STARTED.

The consolidation branch contains the verified Phase 5/Appraisal tip
`48da170`, Ollama tip `5c7d1d0`, and Phase 6B tip `3ddd6b8`. VLM evidence
tooling is merged into `main`; its source commit is `8e9769b` and the merge is
being finalized in the current consolidation round. No phone input, tagging,
transfer or delete authority was added.

Obsolete clean streaming branches may be removed only after ancestry
verification. Dirty/active worktrees and unique worker/WIP branches remain
preserved, including `tag-verification-guard` and the Phase 5/6A worktree with
uncertain `.codex_tmp` contents.

## VLM evidence bake-off (2026-07-28)

Phase 6A technical benchmark: ACCEPTED. Stage 3 produced 139/144
schema-valid responses. `qwen3-vl:2b-instruct` produced 67/72 valid responses
(P50/P95/P99: 2911/4228/10476 ms) as the latency-first candidate.
`gemma3:4b` produced 72/72 (6144/18250/22519 ms) as the schema-reliable
diagnostic comparison. Accuracy is NOT MEASURABLE; FalseKnown and
FalseComplete remain null. No model is production-approved.

## Streaming Vision Phase 6B shadow integration (2026-07-28)

The additive Phase 6B shadow package is integrated on
`feature/streaming-phase6b-shadow`. The supplied package hash was verified,
the solution builds with 0 warnings/errors, and the package self-test passes
10/10. The live read-only shadow runner completed 3 stable AppraisalBars
frames on the authorized OnePlus with zero input. Semantic providers remain
Unsupported until EasyOCR/Ollama and screenshot-reference accuracy are
verified; no real field accuracy or wrong-screen acceptance is claimed.

## Streaming Vision Phase 5 (2026-07-28)

The local official scrcpy v4.0 and FFmpeg 8.1.2 toolchain is installed only
under ignored `tools/local/` and preflight passes for the authorized
`ONEPLUS_A6013`: `1080x2340` portrait resolves to `886x1920`. The zero-frame
root cause was FFmpeg `-fflags nobuffer` suppressing pipe-fed rawvideo output;
removing it restored frames. Real observe acceptance passed 30s/30s/60s with
837/837, 833/832 and 1691/1691 decoded/published frames, clean shutdown and
zero input. Details gate calibration is PASS 3/3 (10 s, 10 s, 20 s) on an
ordinary Details screen. Appraisal gate calibration is also PASS 3/3 after
the existing named route safely normalized `PokemonDetails` to `AppraisalBars`.
Setup sent 3 named inputs; calibration sent 0. Header, AppraisalPanel and all
three IV bars are required; Model/AnimatedBackground remain volatile. Final
phone state is AppraisalBars. Phase 6B/7 were not started.
## Streaming Vision Phase 6A offline semantic foundation (2026-07-28)

Local Phase 6A runtime completion is now evidenced: Python 3.13.14,
RTX 4060 Ti/driver 591.86/8188 MiB, PyTorch 2.11.0+cu128 CUDA smoke test,
ResNet18 embedding-equivalent benchmark and EasyOCR 1.7.2 fixed-crop
benchmark. The synthetic truth manifest is versioned, while real screenshot
field accuracy remains NOT MEASURABLE and no live semantic integration exists.

An isolated worktree from baseline `4ecfad3` adds
`PogoInventory.Streaming.Semantics` and its package-free benchmark/self-test.
The contracts validate frame dimensions/orientation/ROIs, preserve explicit
Known/Conflicting/Occluded/Unreadable/NotVisible/Unsupported/Unknown states,
bind every Known result to evidence, and use deterministic order-independent
field consensus. No live streaming, Device, Automation, phone input, tagging,
Calcy, or decision-engine code was changed.

The clean baseline contains 23 committed iPhone pretest fixtures and synthetic
screen fixtures. The synthetic benchmark produced one correct CP consensus and
`False Complete = 0`; Phase 6A self-test is 8/8. Local runtime completion is
documented in `docs/STREAMING_VISION_PHASE6A.md`: Python 3.13.14, RTX 4060 Ti,
PyTorch CUDA, ResNet18 timing and EasyOCR fixed-crop timing. Real screenshot
field accuracy remains NOT MEASURABLE because crop truth is Unverifiable.

## Streaming Vision Phase 4 (2026-07-28)

Fase 4 preflight and automatic stream-dimension resolution are integrated.
The clean branch baseline is 28 projects with full restore/build green at
0 warnings and 0 errors; clean `test.ps1` is 249/249 with one absent
`local-data` sanity check skipped. Fase 2/3 self-tests are 3/3 and 15/15;
Fase 4 self-test is 7/7.

The authorized OnePlus A6013 (`192.168.1.185:5555`) was verified read-only by
ADB: `1080x2340` portrait, automatically resolved to `886x1920` at max-size
1920. Generic real-stream acceptance is PASS after the `-fflags nobuffer` fix;
gate calibration remains conservative NOT ACCEPTED after three Header
motion/sharpness timeouts. Input count remains zero.

## Streaming Vision Phases 1-3 integration (2026-07-28)

The cumulative Phase 3 delivery is integrated on the opt-in integration
branch. It adds `PogoInventory.Streaming`, read-only scrcpy/FFmpeg transport,
regional temporal gates, `observe-stream`, `observe-gates`, built-in JSON
profiles, bounded PNG evidence and package-free self-tests. The streaming
projects have no phone-input reference or public input surface; diagnostics
report `InputCommandsSent = 0`.

Validation currently available: Phase 2 self-test 3/3 and Phase 3 self-test
15/15. An initial dirty-workspace restore attempt hit the sandbox's
inaccessible global NuGet.Config; the clean baseline later verified full
solution restore/build successfully.

## Run-local decision tag planning (2026-07-26)

Added a deterministic dry-run-only `DecisionTagPlanner`. It maps KEEP to
`AI-Indexed` and every uncertain or destructive recommendation to
`AI-Review`, bound to run ID, ordinal and stable fingerprint. It never invokes
phone tagging or swipe logic and does not use cross-run identity or exact CP.

## Task-K review-case ground truth (2026-07-26)

The 15 NoMatch cases were manually labeled from paired Details/Appraisal
evidence. All 15 pairs are evidence-backed same-ordinal matches with 0 true
identity collisions and 0 false merges. Causes: 9 CP-related (7 missing, 2
incorrect), 3 IV-not-extracted and 2 species-not-extracted; categories are
case-primary and overlap with earlier aggregate diagnostics. Counterfactual
re-match rates are CP-only 88% (44/50), IV-only 76% (38/50), species-only 70%
(35/50), CP+IV 96% (48/50), and all documented extractor errors 100% (50/50).
The single recommended next change is guarded CP extraction; it is not
implemented here.

## Ground-truth labeling tooling (2026-07-26)

Added offline `prepare-ground-truth` and `analyze-field-completeness` CLI
commands. The preparation pack is generated from the two Task-K captured-
observation JSON files and keeps scanner values in separate `Scanner*` CSV
columns; manual ground truth starts `Unverifiable` and requires concrete
evidence sources and a reviewer-assigned entity ID. The analyzer compares
manual values against both SQLite runs, reports overall/per-run field metrics,
and lists the 15 current NoMatch review cases with fail-closed causes and
counterfactual gain scenarios. No scanner behavior was changed. The current
pack has 100 Unverifiable rows, so field acceptance and gain estimates remain
blocked pending manual labeling.

## Phase 2 field-completeness baseline (2026-07-26)

Phase 2 measurement tooling is applied and the example acceptance fixture
passes. A conservative audit of the two fresh Task-K pilot databases covered
100 observations and 500 critical-field rows (Species, CP, AttackIV,
DefenseIV and HpIV). All 500 values are `Unknown`, with 0 `Incorrect`; the
tool therefore passed its fail-closed safety check but reported 0% correct
coverage. No ground-truth labels are available for these phone observations,
so this is not field-completeness acceptance and no OCR correctness claim is
made. Phase 3 and phase 4 remain unapplied.

## Task-K sand-background detector repair

The X-corroborated relaxed Details topology branch now requires
`modelArea >= 0.08`, `detailsPanel >= 0.50` and a verified canonical close
control; the brittle `cpArea >= 0.50` conjunct was removed. The measured
corpus result is 0/343 colliders passing the X locator, and the local hard
evidence gate classified 13/13 frames as PokemonDetails. Self-tests pass
245/245. A fresh 2x50 phone pilot is complete: run 1 and run 2 each captured
50/50 with SQLite integrity `ok` and final phone state `GameplayMap`. Offline
re-identification found 35 matches, 15 no-match candidates, 0 ambiguous
collisions and 0 false-merge guard blocks (70% re-match rate). The phase-1
CSV acceptance classified the 15 no-match cases as PossibleMatch review and
passed 100% accounted/reviewed, but this is not formal ID-006 acceptance;
the 70% re-match rate and lack of ground-truth labels remain blocking.

Focused ordinal diagnosis of the 15 no-match cases found the same species at
the same ordinal in all 15: 9 cases had missing CP, 3 missing IV, 2 Unknown
species/CP, and 2 CP mismatches (categories overlap where a field is absent).
This identifies field completeness/OCR as the next measured investigation;
phase 2 remains unapplied until the identity gate is reviewed.

## Evidence-machine gates result (2026-07-21 aften)

Gates 0-3 were executed against real evidence and the real phone.
The OCR crop transform was repaired (BitmapTransform scale-before-crop),
ROI defaults are spike-tuned, and Tesseract (tessdata-best) replaced
WinRT as the cleanup-flow engine after WinRT produced a false CP
consensus (29 vs real 129). Offline reprocess of the 20-item database:
species 19/20, CP 16/20, zero false values, original untouched, statuses
recomputed. The 50-item real-phone regression captured 50/50 with
36 distinct species, species 48/50, CP 41/50, IV 47/50, zero
query-as-species, zero destructive actions and SQLite integrity ok —
formally red on the >=48/50 coverage gate because large Pokémon models
physically occlude the CP header (structural UI limit, not OCR). The
controller stopped the iteration after gate 3; gate 4
(re-identification, ID-006) has not run and still blocks cleanup work.
Self-tests 208/208. Open defects: live runner does not recompute
ObservationStatus; final-map verification runs before the exit chain
completes. See NEXT_PROMPT.md.

## Semantic foundation checkpoint (2026-07-21)

The semantic core now exists offline: `PokemonHeaderAnalyzer` extracts
species/CP/nickname from header ROIs through an `ITextRecognizer` abstraction
with a Windows.Media.Ocr production implementation (`ocr-header-spike` CLI),
`SearchQueryClassifier` prevents broad queries such as `age0-1825` from ever
becoming a species, `data/reference/species-reference.json` provides 1025
validated species with rarity classification, `RulePolicyLoader` makes the
policy file-configurable, and `SemanticIdentityKey`/`SemanticIdentityMatcher`
(schema v3) provide cross-run identity with an `analyze-reidentification`
measurement command. GroupKey no longer degenerates to singletons for known
species. Offline self-tests pass 193/193.

The header analyzer is now wired into `CleanupProofRunner`: broad queries can
never persist as species (guarded), species/CP/nickname come from OCR
consensus with Automated evidence, IVs accept two-frame exact consensus
independent of the parked Calcy gate, and Complete requires species+CP+all
IVs. `analyze-cleanup-evidence` reprocesses existing databases offline into a
new copy with coverage reporting. Self-tests 201/201.

Not yet done (requires the evidence/phone machine — see NEXT_PROMPT.md for
the gated order): OCR spike hit-rate + ROI tuning on the real 20-item frames,
offline reprocess acceptance, 50-item real-phone regression, double-scan
re-identification acceptance (>=99 %), then resume/chunking and the
manifest-to-tag pipeline. See `docs/MINIMAL_EFFORT_PLAN.md`.

## Persistent Appraisal carousel checkpoint

The cleanup-proof runner now opens Appraisal once, captures and persists each
stable appraisal identity before the next allow-listed swipe, and exits once
after the bounded sequence. Details swipes are not used between ordinary
items. The concrete host has bounded transient-Unknown observation recovery,
changed-fingerprint progression and unchanged-fingerprint end-of-filter
handling. Offline self-tests pass 163/163. Real-phone acceptance is pending.

The real-phone `age0-1825` / 20-item acceptance then completed all 20 items
through the persistent carousel. Evidence shows 19 appraisal cursor swipes,
zero Details cursor swipes, 20 unique fingerprints, one appraisal open
operation, one end-of-carousel exit, SQLite integrity `ok`, and final
`GameplayMap`. All 20 observations are retained as `REVIEW`; no tags or
destructive actions were performed.

## Long self-recovering database acceptance result

The permanent real-phone cleanup proof ran with `age0-1825` and limit 20.
It safely stopped at four Complete observations on
`CursorProgression:Unknown`; all four rows were retained, SQLite integrity
was `ok`, and the reopened database produced four Observations, four
PokemonRecords and twelve InventoryEvents. Reports were generated from the
reloaded database. The run did not meet the ten-item long-run threshold and
must not be described as long-sequence acceptance. The final screenshot was
visually PokemonDetails/Fletchling while the detector returned Unknown, so no
further phone input was sent.

## Canonical close phone acceptance and bounded value-proof result

The canonical-close locator was repaired for the real OnePlus A6013 scale and
requires the lower-centre position, circular shell, crossing X evidence and
stable target revalidation. The direct diagnostic and all three program-created
cycles reached GameplayMap with no affirmative, destructive or tag inputs.
Appraisal bars use the existing guarded named appraisal exit because that
surface has no lower-centre canonical X; Details, Inventory and remaining
layers use canonical close. The direct value-proof command reached the exact
query `pidgey&age0-365`, which produced zero results; it stopped before the
first-card action, preserved SQLite integrity `ok`, and wrote zero observation
and Pokémon-record rows. No real value proof with persisted Pokémon data is
claimed.

## Canonical close Android scale repair checkpoint

The first direct canonical-close diagnostic captured the actual Details
screen's lower-centre X but stopped with zero input because the locator used a
single button radius. The locator now evaluates bounded scale-normalized radii
around the expected Android control dimension and retains shell, X-stroke,
position, dimensions and contrast evidence. Build and self-tests pass 162/162.
The diagnostic must be rerun once; no phone acceptance is claimed yet.

## Canonical close unwind checkpoint

Cleanup startup now uses `CanonicalCloseUnwindService` rather than a large
state-specific recovery graph. `LocateCanonicalCloseControl` requires three
compatible screenshot-derived lower-centre targets, fresh revalidation and a
stable changed post-state after exactly one tap. The unwind stops on loops,
missing canonical control, unsafe ambiguity or five inputs. A positively
verified canonical close is the only input permitted on an unsafe confirmation
surface; affirmative controls remain blocked. Offline self-tests pass
162/162. No real-phone acceptance is claimed yet.

## Cleanup startup stability repair checkpoint

The first direct autonomous phone attempt started from the observed Details
screen but its strict recovery-frame evidence window did not reach consensus;
the command stopped before input with `RecoveryInputCount=0`. The adapter now
saves bounded recovery evidence and accepts three independent same-state
ordinary Details frames when topology evidence changes during model settling.
AppraisalIntro/AppraisalBars retain strict ROI consensus. Build and self-tests
pass, 162/162. The real value proof was not automatically repeated after this
runtime repair, so no phone acceptance is claimed.

## Autonomous cleanup start recovery checkpoint

This checkpoint is superseded by the canonical-close unwind above. The real
phone value proof remains pending and is not claimed.

## Cleanup proof pipeline implementation checkpoint

The CI packaging defect in `WrongScreenAuthorizationTests` is fixed with
deterministic `PixelImage`/`PngEncoder` fixtures and passes from a clean clone
with `local-data` absent. The permanent `device-run-cleanup-proof` command now
composes the existing named Android host, bounded identity/appraisal evidence,
`InventoryPersistenceService`, the existing recommendation engine and fresh
SQLite-backed reports. Two usable identity frames are preserved as Partial and
do not terminate the batch; unresolved/unsafe evidence remains fail-closed.
Offline self-tests pass 160/160. No real-phone cleanup proof is claimed yet.

## Deterministic navigation safety acceptance tooling checkpoint

The permanent `device-validate-navigation-safety` CLI command requires three
GameplayMap precondition frames and calls the concrete
`AndroidVerifiedInventoryNamedOperations` host for bounded navigation. It
records `action-trace.jsonl` phases around the actual named transport input,
including three precondition frames, fresh authorization, input return, five
post-input frames and postcondition. Three real read-only cycles passed on the
authorized OnePlus A6013 with five inputs, two Back actions, 25 postframes and
GameplayMap final state per cycle. No Cancel, tag or destructive action was
sent. Offline self-tests pass 159/159.

## Wrong-screen action authorization repair checkpoint

The historical incident in which a Details screenshot authorized the
MainMenu Inventory target `(300,1837)` is now explicitly fail-closed. Opening
Inventory requires three stable typed MainMenu observations, positive
MainMenu/Inventory topology, no Details/PokemonMenu/Appraisal/modal conflict,
and a fresh pre-tap revalidation. A stale MainMenu frame cannot authorize a
Details screen, and visual Details fallback never grants MainMenu.

The named-operation host has a conservative destructive-confirmation
interlock for Power Up, Evolve, Transfer, Purify and purchase/item confirmation
surfaces. It records UnsafeConfirmation evidence and sends no input; it never
auto-cancels. Named input audits include strict and fallback observations,
conflicts, target, precondition/fresh screenshot hashes and InputSent.

Offline self-tests: 158/158. The phone check is intentionally not run in this
checkpoint because the incident modal requires Torben's manual CANCEL first.
No real-phone acceptance claim is made.

## Android runtime repair checkpoint

`AndroidVerifiedInventoryNamedOperations` now delegates appraisal and return
navigation to `GuardedInventoryRecovery`. Intro and Bars use the authorized
visual ExitAppraisal target; Android Back is permitted only from verified
Details or PokemonMenu states. Cursor advancement requires observed transition
evidence, then captures three independent stable Details frames. Equal stable
fingerprints no longer imply a failed swipe.

`VerifiedInventoryTaskSequence` distinguishes `ControlledStopped`, terminal
Unknown/Failure and `Completed`. A controlled checkpoint reopens the first card
only for bounded replay, verifies the overlap item, then advances once to the
next new ordinal. Completed checkpoints are idempotent. Offline self-tests:
157/157. Real-phone acceptance remains pending ADB preflight.

## Android verified sequence host checkpoint

`AndroidVerifiedInventoryNamedOperations` is now the concrete Android host
for `VerifiedInventoryTaskSequence`. It uses the existing named transport,
game-state detector, guarded inventory search/recovery, visual locators and
dynamic identity analyzer. The normal loop opens the first result once and
advances through stable Details with one allow-listed swipe per next ordinal.
No raw ADB is constructed above `PogoInventory.Device`.

The checkpoint schema records current/last-completed ordinals, previous/current
stable fingerprints, last verified state, identity status, evidence hashes and
structured tag observations. Resume replays verified cursor steps and fails
closed on overlap mismatch. `device-run-index-sequence` defaults to bounded,
read-only operation with tag application disabled. This checkpoint is not a
real-phone acceptance claim.

Offline self-tests: 156/156.

The bounded real-phone attempt found no authorized ADB device. Reconnect to the
expected Wi-Fi serial failed with Windows socket error 10013; no production
host input was sent and Tasks E/F/G remain unaccepted.

## Current version

0.14.3

## Task 5 sequence orchestration checkpoint

`VerifiedInventoryTaskSequence` composes only
`IVerifiedInventoryNamedOperations`. It validates bounded limits and tags,
atomically checkpoints each item, resumes only on matching request context,
preserves Partial evidence, attempts the named ReturnToInventory recovery and
continues only after Inventory is verified. It stops input on Unknown or failed
Partial recovery and assigns ordinal instance IDs independently of hashes.
AI-Delete cannot be auto-applied and no delete operation is exposed. This
checkpoint is offline-only; real-phone Task 5 acceptance is not claimed.

## Task 4 dynamic identity implementation checkpoint

`PokemonDetailsIdentityAnalyzer` keeps the concrete PNG SHA-256 evidence hash
separate from a stable multi-ROI fingerprint. It records dynamic tag-section
bounds, mutable tag observation, lower-content anchor evidence and three-frame
consensus. `PokemonIdentityInstance` assigns `ScanRunId:ordinal` independently
of both hashes, so identical fingerprints remain separate instances.

Offline self-tests are 156/156. Three real five-frame Details groups complete
with the tuned profile; a fourth captured group is Inventory rather than
Details and is correctly Unavailable. The zero/one/two-tag Task 3 captures are
counted as 0/1/2, and the zero-tag versus tagged stable fingerprint similarity
is 0.9815 against the 0.965 threshold. Real-phone acceptance remains PARTIAL
because the complete three-state acceptance set is not a 20-Pokémon provider
gate and one local group is not a Details screen. No real-phone Task 4 approval
claim is made.

The identity consensus contract now requires at least three compatible usable
frames for Complete. One or two usable frames are Partial, unavailable frames
do not count, and the consensus fingerprint is a deterministic bytewise median
over all compatible frame fingerprints. CLI exit codes are 0 Complete, 2
Partial and 3 Unavailable.

## Verified tag selection by name accepted on 2026-07-20

`device-set-pokemon-tag` now identifies visible rows geometrically and matches
the requested name through an ignored, device-calibrated visual profile at
three bounded normalized scales. It requires confidence plus separation from
the second-best row. Row position and row ordinal are never identity.

The operation verifies Details, Menu, selector visibility, selected/unselected
check state, Done and the resulting Details tag pill. It scrolls at most the
profile limit and records `TAG_NOT_FOUND_NO_MUTATION` without tapping any row
when a name is unavailable. Requests already in the desired state perform zero
row mutations.

Two real Trade add/remove cycles passed on Ekans CP616 with zero wrong tag
selections. Each addition was confirmed by the selector, Details and `#Trade`;
each removal was confirmed by the selector, Details, an empty `#Trade`, and
Ekans under `!#Trade`. Final tag state is unselected and Inventory is
unfiltered. Build passes and 148/148 self-tests pass. Real profiles and images
remain under ignored `local-data`.

Additional Task 3 acceptance passed for all four existing AI tag names. Each
named row was independently matched, selected, committed, observed on Details
and removed with zero wrong selections. Dynamic pill counting accepted zero,
one and two simultaneous tags and verified the 2 -> 1 -> 0 removal sequence.
AI-Delete was only a reversible tag-name test; no destructive action exists or
ran. The final phone state is unfiltered Inventory and Ekans CP616 has none of
the tested tags. Build passes and 149/149 self-tests pass.

## Verified Inventory Search accepted on 2026-07-20

`device-search-inventory` now owns a bounded Open -> Clear -> Enter -> Submit
workflow. It requires a visually verified Inventory search surface before any
input, checks each postcondition, records the expected ordinary query and local
screenshot hashes, and can clear the completed query. Android shell escaping is
centralized inside `PogoInventory.Device`; higher layers never supply raw ADB
syntax.

Two complete real-phone rounds passed for `age0-7`, `age0-365`, `age0-1825`,
`#Trade` and `!#Trade`. Visual review confirmed the exact text and result lists;
the visible counts were 7, 7, 303 and 0 for the currently empty Trade tag. The
`!#Trade` result was populated. Both rounds ended in unfiltered Inventory. The
first pre-input attempt stopped safely when the Search placeholder was too
broadly classified; the repaired analyzer was then accepted in all ten runs.
Build passes and 146/146 self-tests pass.

## Guarded appraisal recovery accepted on 2026-07-20

The recovery increment uses state-specific ROI stability rather than
full-screen pixels. Appraisal intro and appraisal bars each use documented
regions and a three-of-five consensus. `GuardedInventoryRecovery` owns every
transition and action-limit rule; the CLI recovery command contains no parallel
inline state machine. The self-test total is 144/144.

Real-phone acceptance passed on the connected OnePlus A6013 for three complete
Inventory -> Details -> Menu -> AppraisalIntro -> AppraisalBars -> Details ->
Inventory cycles. The documented left-middle normalized target `(0.1001,
0.5002)` is used once per appraisal substate. Android Back is never sent from
AppraisalBars and is sent once only after PokemonDetails is verified. The run
recorded zero Unknown states and zero wrong post-states. Evidence is retained
under ignored `local-data/validation/sol-high-android-implementation`.

## Build correction in 0.14.1

GitHub Actions built the existing projects and reached
`PogoInventory.Appraisal`, where `AppraisalAnalyzer.cs` failed with CS0173.

The conditional expression returned an `int` when a bar track was detected and
`null` otherwise. Because the local variable used `var`, the compiler had no
nullable target type available for the conditional expression.

Version 0.14.1 explicitly declares the variable as `int?`. The intended
behavior is unchanged: a measured candidate IV is 0 to 15, while an
unmeasurable bar remains null.

## Decoder correction in 0.14.2

The appraisal pretest initially terminated on `IMG_7699.png` because
`PngDecoder` reports unsupported PNG variants through `ScreenVisionException`,
while the appraisal runner only caught framework decoder exceptions.

Version 0.14.2 catches `ScreenVisionException` and retains the file name,
SHA-256, error code and error detail in the report. One unsupported image can
therefore no longer terminate an otherwise valid 23-image fixture set.

The known file can also be removed safely with:

```powershell
.\scripts\remove-known-unsupported-iphone-fixture.ps1
```

The script deletes only `IMG_7699.png` with the exact known SHA-256 and refuses
to delete a changed file.

## Build correction in 0.14.3

The 0.14.2 exception filter used `exception is` twice inside one C# `or`
pattern. After the first type pattern, the remaining alternatives must be type
names only.

Version 0.14.3 uses this valid pattern:

```csharp
catch (Exception exception) when (
    exception is ScreenVisionException or
    InvalidDataException or
    NotSupportedException or
    ArgumentException or
    OverflowException)
```

The runtime policy is unchanged: unsupported image files remain diagnostics and
do not terminate the appraisal pretest.

## Accepted checkpoint

Torben reported version 0.13.1 fully green in GitHub Actions.

Accepted real iPhone evidence:

- 24 committed PNG screenshots
- 23 decoded screenshots
- four visual clusters
- cluster 01 is inventory list
- cluster 02 is Pokémon details
- cluster 03 is appraisal
- cluster 04 is the details action menu
- accepted region discovery, crop atlas and semantic evidence pack

## Completed

### Foundation

- .NET 8 solution
- conservative KEEP, REVIEW and DELETE analysis
- read-only Android device harness
- deterministic screen calibration
- automatic inventory navigation limited to four named actions
- checkpoints and safe resume
- Calcy probe and verification gate

### iPhone evidence pipeline

- decoding, hashing, similarity and clustering
- normalised region discovery
- crop atlas and semantic evidence review pack
- no full source screenshot copied into review packs

### M4 phase 4e: appraisal definitions and phone preparation

Version 0.14.0 adds:

- `PogoInventory.Appraisal`
- normalised Attack, Defense and HP bar definitions
- automatic X/Y translation and uniform-scale fitting
- orange-fill and neutral-track measurement
- candidate IV estimates
- diagnostic overlays and bar crops
- iPhone appraisal pretest
- dominant-cluster concentration gate
- hard zero-Complete gate for unverified profiles
- read-only `phone-prepare`
- local device-adjusted profile generation
- Android readiness report
- 134 expected self-tests

### Real Android navigation and variant-safe evidence

The connected OnePlus 6T has completed a fresh 20-item appraisal scan with:

- 20 persisted captures and unique screenshot/fingerprint hashes
- 19 verified `SwipeNextPokemon` actions
- 3/3 stable phone calibration cases
- 20 Candidate-only appraisal observations and zero Complete observations
- 20 conservative REVIEW decisions and zero DELETE decisions
- schema-versioned semantic variant identity and per-run instance evidence

Unknown form, costume, background and special state values remain Unknown. They
cannot share an ordinary duplicate group or authorize DELETE.

`scripts/start-night-evidence-scan.ps1` runs the same conservative appraisal
evidence path with profile hashes, heartbeat, battery/disk/device safety checks,
an item limit and a wall-clock limit. It performs no transfer or tagging action.

### Real phone validation update on 2026-07-19

The connected OnePlus A6013 completed a fresh 3-item real-phone appraisal run
with:

- 3/3 calibration cases marked stable
- zero Complete observations
- 2/2 verified swipes
- 3 candidate observations
- real `phone-calibration-stability.md` and `phone-calibration-stability.json`
- zero transfer actions

Calcy evidence on the same device was also rechecked:

- `calcy-probe` reported `CandidateEvidenceFound`
- the installed package was `tesmath.calcy` version `3.44`
- overlay permission was proven
- accessibility and running-service surfaces remained non-observational
- `calcy-live-check` completed one navigation item and the read-only probe path
- no parsed observation was produced because no parser profile was supplied

## Input boundary

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
```

Version 0.14.3 adds no phone input action.

## What the iPhone images now provide

The screenshots provide reusable normalised definitions and initial colour
thresholds. They do not lock the solution to iPhone pixels.

When the Android phone is connected, the profile searches translation and scale
around those definitions. A single visible appraisal screen can therefore
generate phone-specific definitions automatically.

## Game-state detector and guarded recovery iteration (2026-07-20)

Added a shared read-only `PokemonGoGameStateDetector` in the exploration layer.
It reuses the existing visual control anchors and appraisal analyzer and emits
one of Inventory, PokemonDetails, PokemonMenu, Appraisal or Unknown together
with confidence, evidence and screenshot SHA-256. Added CLI commands
`device-detect-game-state` and `device-recover-inventory`; recovery sends at
most two Back actions and stops on Unknown or an unexpected post-state.

The real phone detector identified the current Details screen at confidence
1.000. The guarded recovery sent one Back, but the stable post-action frame
remained Details, so the run stopped without a second blind action.

## Not completed

### Gameplay map state detection (2026-07-20)

The shared detector now has an explicit `GameplayMap` state. It checks the
existing main-menu Poké Ball anchor before Inventory and Details, preventing
the map's lower-right teal control from being misclassified as PokemonDetails.
PokemonDetails now also requires an independent details-page topology anchor.
The saved real map frame is detected as GameplayMap at confidence 1.000.
No phone input was sent in this iteration.

- extraction of exact semantic variant identity from Android screenshots
- twenty-case appraisal truth verification
- verified Complete visual IV provider
- real Calcy provider selection and verified parsed-observation extraction
- species and CP extraction
- caught-on location/origin persistence for later tagging
- SQLite inventory database
- final tagging plan
- transfer remains manual

## Required checkpoint after push

1. Build all 18 projects.
2. Confirm 138 of 138 self-tests pass.
3. Confirm the existing iPhone evidence stages remain green.
4. Confirm appraisal pretest finds at least five candidates.
5. Confirm candidates are concentrated at least 70 percent in one cluster.
6. Confirm the unverified profile produces zero Complete observations.
7. Confirm `appraisal-review-pack.zip` is created.
8. Preserve zero new phone actions.

The M1 real-phone hardening is now implemented and pushed. WiFi ADB and wall
charging were stable during the controlled run, which captured 125 unique
appraisal frames. The run stopped safely at `UnknownScreen` when the current
inventory position was exhausted, so the 30-minute appraisal acceptance test
is still open and must not be reported as passed.

## Next recommended milestone

When the Android phone and PC are available:

1. manually open a Pokémon appraisal screen
2. run `scripts/prepare-android-phone.ps1`
3. inspect `phone-readiness.json`
4. confirm a device-adjusted profile is generated
5. repeat on at least three different Pokémon
6. compare fitted regions and candidate IV values
7. run Calcy probe and live check
8. collect twenty real verification cases before allowing Complete IV output

The first three steps were completed on 2026-07-19 against the connected
OnePlus A6013. Until the 20-case verification gate is passed, improve only
diagnostics and verification scaffolding. Do not add location changes,
transfer automation, anti-detection logic or arbitrary shell execution.

## Android sequence real-phone acceptance checkpoint

The repaired runtime was accepted on 2026-07-21 with the authorized OnePlus
A6013 at `192.168.1.185:5555`. The clean three-item `age0-7` run completed
ordinals 1, 2 and 3 with two observed transitions and three independent
post-swipe Details frames per transition. The controlled-stop run stopped after
2, replayed ordinal 2 for overlap comparison without recording it again, then
made one new progression to ordinal 3 and completed in Inventory.

The ten-item `age0-365` run remains partial and fail-closed: three items were
recorded, then item 4 produced one verified `NO_EFFECT` swipe and entered
`TerminalUnknown`; no second swipe was sent. A guarded recovery closed
Inventory and verified `GameplayMap`. No tag mutation or destructive action
occurred. Evidence is under `local-data/validation/android-sequence-host`.

## Cursor changed-identity checkpoint

The cursor repair is implemented locally with 157/157 self-tests passing. A
missed transient transition is now resolved by three independent stable Details
frames: changed fingerprint is `SUCCESS_CHANGED_IDENTITY`, unchanged
fingerprint is explicit `NoEffectOrEndOfFilter`, and exactly one swipe is ever
sent. Guarded recovery uses the same allow-listed Details topology fallback for
detector Unknown.

The first real `age0-1825`, limit-10 run recorded four complete items and
verified the changed-identity path. Later bounded attempts reached a visible
`POWER UP: FLETCHLING` confirmation screen. No further input was sent, so the
10-item acceptance remains blocked and must not be claimed.
