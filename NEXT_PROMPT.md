# Continuation prompt

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
