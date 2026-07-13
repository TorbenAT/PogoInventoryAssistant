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

Torben reported a green 0.2.0 GitHub Actions run.

## M2 Generic Screen State Detector, complete in 0.3.0

- package-free PNG decoder
- deterministic image fingerprints
- normalised screen regions
- named Required, Optional and Forbidden anchors
- known screen states
- Unknown and conflicting-state handling
- orientation, resolution and aspect-ratio validation
- synthetic and non-personal fixtures
- JSON evidence report
- no device input

## M2b Calibration workflow, complete in 0.4.0

- private workspace
- fixture indexing and SHA-256 locking
- anchor-plan profile generation
- acceptance policy and confusion report
- weak-anchor diagnostics
- synthetic CI validation
- read-only only

## M2c-a Guided real-screen capture, complete in 0.5.0

- private incoming capture staging
- versioned state coverage plan
- manual navigation with explicit Enter-to-capture
- device serial and exact geometry session locks
- SHA-256 capture integrity
- duplicate screenshot detection
- progress and missing-state reports
- explicit privacy review before fixture promotion
- no phone input automation

## M2c-b Real-screen acceptance, next

- collect and approve private fixtures on the fixed Android configuration
- select stable real UI anchors
- build phone-specific local profile
- zero false positives
- zero known-state misclassifications
- zero weak anchors
- manual acceptance report review

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
