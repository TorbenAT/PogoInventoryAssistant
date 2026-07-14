# Continuation prompt

Use this after version 0.14.2 is green.

I am building Pogo Inventory Assistant in C# and .NET 8.

Open the repository and read `PROJECT_STATE.md`,
`docs/GUARDRAILS.md`, `docs/IPHONE_APPRAISAL_PRETEST.md`,
`docs/ANDROID_PHONE_PREPARATION.md`,
`docs/CALCY_PROVIDER_VERIFICATION.md` and
`docs/AUTOMATIC_NAVIGATION.md`.

Current version: 0.14.2.

Accepted:

- 23 decoded real iPhone screenshots
- four labelled visual clusters
- normalised appraisal bar definitions
- automatic translation and scale fitting
- candidate IV estimates only
- zero Complete results from unverified profiles
- read-only Android `phone-prepare`
- 113 self-tests

First verify that version 0.14.2 is green.

Next milestone when the fixed Android phone is available:

1. Run `scripts/prepare-android-phone.ps1` while the phone is manually on an
   appraisal screen.
2. Inspect `phone-readiness.json`, `appraisal-overlay.png` and the generated
   device profile.
3. Repeat on at least three different Pokémon.
4. Add a profile-stability report comparing fitted bar coordinates and values.
5. Run `calcy-probe` and `calcy-live-check`.
6. Build twenty real truth cases.
7. Require zero false Complete observations before selecting the visual
   appraisal provider.
8. Add no new phone input action unless it is one of the existing four named
   actions and remains state validated.
