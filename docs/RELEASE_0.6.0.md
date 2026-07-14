# Release 0.6.0

## Automatic navigation

This release implements unattended inventory traversal after one-time local phone/profile setup.

The runner can:

- start from inventory list, details, menu or appraisal
- navigate automatically to appraisal
- capture each visible Pokémon
- swipe to the next Pokémon
- verify that the item changed
- detect the end of the inventory
- checkpoint after every item
- resume from a matching interrupted state
- reject a checkpoint when either local profile has changed

## Device input

`PogoInventory.Device` now exposes a separate `IAndroidAutomationTransport` with only:

```text
TapAsync
SwipeAsync
```

The automation layer maps those primitives to four named actions. It cannot issue arbitrary ADB commands.

## Automatic local evidence

The automatic scan path does not require image-by-image approval. PNG evidence, hashes, sequence data and audit actions are recorded automatically under ignored local output.

## Safety

The runner stops on unknown screens, popups, network errors, timeouts, device or geometry mismatch and unsafe battery conditions.

There is still no transfer, tagging, text input, evolution, power-up, purification, TM use, purchase, gameplay or location change.

## Testing

- deterministic scripted Android transport
- three visually distinct appraisal fixtures
- automatic state path test
- end detection test
- maximum-item test
- completed-checkpoint idempotency test
- ADB tap and swipe command test
- expected self-test count: 52

## Next

The next release will automatically bootstrap the core real-phone screen profile and attach Calcy-derived Pokémon observations to each sequence item.
