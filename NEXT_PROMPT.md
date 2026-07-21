# Continuation prompt

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
internally through `KnownGameStateNormalizer`; no manual GameplayMap
preparation is required. The normalizer is bounded to six verified inputs,
requires stable pre/post frames, records `start-state-recovery.json` and
`start-state-recovery.md`, and stops on Unknown, unsafe, conflicting, repeated
or exhausted recovery. Offline self-tests are 162/162.

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
