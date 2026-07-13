# Roadmap

## M0 Foundation, complete in 0.1.0

- solution structure
- domain model
- policy model
- conservative decision engine
- reports
- self-tests
- guardrails and project handoff

## M1 Device Harness, complete in 0.2.0

Read-only only:

- discover exactly one authorised Android device
- query metadata, screen size, battery and temperature where available
- capture screenshots
- fake device implementation
- timeouts, cancellation and structured errors
- atomic output and SHA-256 manifest
- no device input

Pending acceptance checks:

- green GitHub Actions run
- successful fake capture on the Windows computer
- one successful real-phone capture

## M2 Screen State Detector, next

- image-anchor framework
- normalised screen regions
- known screen states
- Unknown and conflicting-state handling
- orientation and resolution validation
- synthetic or redacted test fixtures
- JSON evidence report
- no device input

## M3 Calcy Spike

- verify whether current Calcy can be invoked and read reliably
- isolate integration behind an interface
- compare 20 known Pokémon
- abandon or replace the adapter if results are unstable

## M4 Read-only Scanner

- state machine
- observation pipeline
- JSON or SQLite persistence
- checkpoints
- evidence screenshots
- 200-Pokémon test

## M5 SQLite and identity

- observations
- scan runs
- fingerprints
- neighbour references
- resume logic
- duplicate and gap detection

## M6 Full PvP and species intelligence

- Ohbem or equivalent IV-rank engine
- PvPoke-derived versioned relevance data
- evolution paths
- league caps
- moveset relevance
- raid and Master League roles

## M7 Full inventory dry-run

- 1,000-Pokémon validation
- complete overnight scan
- coverage report
- no phone changes

## M8 Tag Executor in simulator

- strict action whitelist
- plan hash
- batch-specific tags
- audit log
- rollback plan

## M9 Controlled phone tagging

- 20 manually selected test items
- then 200 controlled decisions
- zero false delete tags required
- full tagging only after review

## M10 Manual transfer workflow

- search by batch delete tag
- count reconciliation
- targeted review
- manual transfer only
