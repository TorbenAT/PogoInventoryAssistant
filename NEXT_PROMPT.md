# Continuation prompt

The guarded appraisal recovery is accepted offline and on the real OnePlus
A6013. Full-screen animation is excluded from stability; intro and bars use a
three-of-five ROI consensus. `ExitAppraisal` performs one documented
left-middle tap per appraisal substate, and Android Back is authorized only
from PokemonDetails. Three complete cycles passed with zero Unknown states,
zero wrong states and zero Back actions on AppraisalBars. Build passes and
144/144 self-tests pass.

Continue with the generic Inventory Search increment. Centralize Android text
escaping inside `PogoInventory.Device`, accept ordinary caller text including
`#Trade` and `!#Trade`, and require verified Inventory/InventorySearch pre- and
post-states, bounded waiting and audit evidence. Preserve the accepted recovery
state machine and the ignored evidence under
`local-data/validation/sol-high-android-implementation`.

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
