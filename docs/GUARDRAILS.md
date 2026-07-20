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

Only these six named input actions are allowed in the accepted appraisal
increment:

```text
TapFirstInventoryCard
TapDetailsMenu
TapAppraise
SwipeNextPokemon
TapAppraisalIntroContinue
ExitAppraisal
```

Coordinates come from a validated local automation profile and are converted from normalised values to the locked screen geometry.

`TapAppraisalIntroContinue` is authorized only by three compatible
AppraisalIntro ROI observations among the latest five frames. It uses the
`LocateAppraisalIntroContinue` target, is capped at one tap and must be followed
by three compatible AppraisalBars ROI observations. Stable bars require no tap.

`ExitAppraisal` is authorized only by stable AppraisalIntro or AppraisalBars
evidence. It uses the documented normalized left-middle target once and must be
followed by the expected next substate. Android Back is forbidden on
AppraisalBars and is authorized only after PokemonDetails is verified.

There is no arbitrary shell command, arbitrary higher-layer coordinate API, text input, tag action or destructive action.

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
