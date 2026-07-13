# Project state

## Current version

0.1.0

## Completed

- Repository and solution structure
- Core Pokémon observation model
- Nullable status fields so unknown is not silently treated as false
- Policy loaded from JSON
- Conservative KEEP / REVIEW / DELETE analysis
- Hard protection for 4*, shiny, mythical, background, favorite, old and Trade-marked Pokémon
- REVIEW protection for legendary, Ultra Beast, shadow, purified, lucky, costume, Dynamax, Gigantamax, special move, XXL and XXS
- Living-dex style minimum copy preservation within ordinary duplicates
- First-pass PvP candidate preservation using low Attack and high Defense/HP
- Exact-identity requirement before DELETE
- Better-duplicate requirement before DELETE
- JSON and Markdown decision reports
- Self-test project
- Architecture, rules, guardrails and roadmap

## Not completed

- ADB connection
- Screen capture
- Screen-state recognition
- Calcy integration
- OCR and icon recognition
- SQLite database
- Full PvPoke / Ohbem integration
- Scan checkpoints
- Device-side tagging
- Audit screenshots
- Rollback

## Next recommended milestone

M1: Device Harness in read-only mode.

The next package should:

1. Detect exactly one authorised Android device through ADB.
2. Read device model, resolution, battery and temperature where available.
3. Capture screenshots to disk.
4. Never send taps or swipes in the first iteration.
5. Provide a simulator interface so later components are testable without a phone.
6. Add an explicit action whitelist before input control is introduced.

## Design decisions already made

- C# and .NET 8
- No hidden game API
- No automatic transfer
- No anti-detection behaviour or human imitation
- Unknown data results in REVIEW, never DELETE
- DELETE requires an exact identity and a documented better duplicate
- Each package must include an updated `PROJECT_STATE.md` and `NEXT_PROMPT.md`
