# Wrong-screen action authorization

MainMenu and Inventory actions require typed, stable state authorization. A
raw screenshot locator cannot authorize an Inventory tap. The required
MainMenu precondition is three stable MainMenu frames with positive Inventory
topology, no mutually exclusive Details/Menu/Appraisal/modal evidence and a
fresh same-screen revalidation immediately before input. If MainMenu and
PokemonDetails are both positive, authorization is denied and input count is
zero.

Power Up, Evolve, Transfer, Purify and purchase/item confirmation surfaces are
unsafe interlocks. They block normal navigation, search, menu, appraisal,
Back and swipe inputs, save screenshot evidence and record `InputSent: false`.
The safety path never auto-cancels a destructive dialog.

# Guardrails

## Non-negotiable restrictions

The software must not include functions for:

- transferring Pokémon
- evolving Pokémon
- powering up Pokémon
- purifying Pokémon
- using TMs
- buying anything
- spending Stardust or Candy
- changing location
- catching Pokémon
- spinning PokéStops
- raids or battles

Final transfer remains manual.

## Allowed input in version 0.14.3

Only these named input actions are allowed in the accepted navigation and
search increments:

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
TapAppraisalIntroContinue
ExitAppraisal
OpenInventorySearch
ClearInventorySearch
EnterInventorySearchText
SubmitInventorySearch
OpenPokemonTagSelector
SetExistingPokemonTag
CommitPokemonTagSelection
AdvanceKnownEggHatch
ContinueKnownWeeklyChallenge
CancelKnownExitDialog
```

Coordinates come from a validated local automation profile and are converted from normalised values to the locked screen geometry.

`TapAppraisalIntroContinue` is authorized only by three compatible
AppraisalIntro ROI observations among the latest five frames. It uses the
`LocateAppraisalIntroContinue` target, is capped at one tap and must be followed
by three compatible AppraisalBars ROI observations. Stable bars require no tap.
For profile-bound Bar classification, each of the three IV tracks must be
measured and the appraisal candidate confidence must be at least 0.80; lower
or partial candidates remain untrusted and authorize no carousel input.

An unknown or modal-like overlay stops with zero input before ordinary state
routing. No underlying Details/map topology may authorize Back. The only Back
exception is the separately named, exact three-stable-frame KnownExitDialog
fallback with its required known postcondition.

`ExitAppraisal` is authorized only by stable AppraisalIntro or AppraisalBars
evidence. It uses the documented normalized left-middle target once and must be
followed by the expected next substate. It never authorizes Android Back;
ordinary PokemonDetails, Inventory, GameplayMap, Search and timeout recovery
are all zero-input if their named visual control cannot be verified.

Search text is validated as ordinary text at the caller boundary and encoded
for Android only inside `PogoInventory.Device`. Search requires verified
Inventory/Search state, bounded post-action checks and audit.

Tag selection is limited to an existing tag whose name is confidently matched
from geometric row discovery and a validated local profile. A fixed row index,
a fixed tag-row coordinate, unbounded scrolling and a row action after failed
matching are forbidden. Selected/unselected state and the resulting Details
state must be verified. The action is reversible and conveys no transfer or
delete authority; an `AI-Delete` visual template, if locally configured, is
identity evidence only.

There is no arbitrary shell command, arbitrary higher-layer coordinate API or
destructive action.

## No anti-detection behaviour

Do not add:

- random timing intended to mimic a human
- random tap positions intended to hide automation
- detection avoidance
- account-behaviour camouflage

Adaptive waiting is allowed only for correctness, such as waiting for a recognised state, image change or timeout.

## No per-image approval in the automatic path

Automatic inventory evidence is local machine data and is captured without user approval per image. Privacy approval is not a correctness gate for an overnight scan.

The old guided calibration and manual-promotion commands remain as fallback utilities. They are not the target production workflow.

## Unknown screen state is a hard stop

The automation must not act when:

- screen state is `Unknown`
- required anchors are missing
- forbidden anchors are present
- orientation or layout is unsupported
- two states conflict
- confidence is below threshold
- popup or network error is present

### Known benign Pokémon GO interruptions

Known benign interruptions are not treated as ordinary navigation states and
are never dismissed by a generic Back or centre-screen tap. The shared
`KnownBenignInterruptRecovery` layer may act only after 3 compatible frames
among the latest 5, a fresh pre-input frame, and a visually located named
control. Its whole interruption budget is six inputs; the currently proven
families use exactly one input: `AdvanceKnownEggHatch` on the visible egg,
`ContinueKnownWeeklyChallenge` on its green CTA, and
`CancelKnownExitDialog` on the separate CANCEL glyph band. A single Android
Back is an exceptional named fallback only when the precise three-stable
KnownExitDialog topology has just been revalidated; it is not available to
EggHatch, WeeklyChallenge, Appraisal, PokemonDetails, Inventory, GameplayMap,
Search, Unknown or unsafe dialogs. The concrete Back capability is owned only
by the KnownExitDialog recovery path; generic recovery interfaces and CLI
commands do not expose it. Every input is
audited and must reach a stable, known, non-interrupt postcondition. These are
implemented recovery candidates, not live-accepted until their individual
real-phone postcondition has been observed.

Unsafe confirmation detection is also fail-closed against false positives:
colour/shape scores alone cannot label ordinary PokemonDetails action rows a
modal when independent Details topology and the canonical Details close are
both visible. This exception does not authorize Power Up, Cancel or Back; the
ordinary Details state must still pass the named-action precondition. Any
unknown or actual modal remains a terminal zero-input stop.

If the post-tap screen is animated, unknown, conflicting, or another
unrecognised modal, the layer sends zero further input, saves evidence and
stops. `UnsafeConfirmationSurfaceDetector` still denies Power Up, Evolve,
Transfer, Purify and purchase/item confirmations before they can be confused
with a benign recovery.

## Identity rules

The navigation fingerprint proves that the screen changed. It does not by itself authorise DELETE or tagging.

A future delete tag requires:

- exact Pokémon identity
- locked execution plan
- documented better retained duplicate
- no protected status
- before and after evidence

## Fail closed

Stop or return REVIEW when:

- device is missing, unauthorised or ambiguous
- device serial changes
- screen geometry changes
- input command times out
- capture output is invalid
- resume screen does not match the checkpoint
- sequence is not contiguous
- critical observation data is unknown
- inventory counts do not reconcile

## Auditability

Every input action records:

- sequence number
- action kind
- state before
- state after
- start and completion time
- action detail

Every captured item records screenshot and fingerprint hashes.

## Public repository data

Do not commit real screenshots, device serials, checkpoints, inventory exports, databases, logs or real local profiles while the repository is public.
