# Android phone preparation

Connect the fixed Android phone only after ADB and USB debugging are available.

1. Unlock the phone.
2. Authorise the computer for USB debugging.
3. Open Pokémon GO manually.
4. Open one Pokémon and its appraisal screen.
5. Run:

```powershell
.\scripts\prepare-android-phone.ps1
```

The command is read only. It:

- selects exactly one authorised Android device
- reads metadata
- captures one screenshot
- applies the normalised appraisal profile
- auto-fits translation and scale
- writes `phone-readiness.json`
- writes a local device-adjusted profile when appraisal bars are found

It does not tap, swipe, tag, transfer or navigate.

The generated profile remains unverified. Run the command on at least three
different appraisal screens and later pass a twenty-case verification gate
before Complete IV extraction is permitted.
