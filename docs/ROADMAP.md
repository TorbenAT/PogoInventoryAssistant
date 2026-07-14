# Roadmap

## M0 Foundation, complete in 0.1.0

- solution structure
- domain and policy models
- conservative decision engine
- reports and self-tests

## M1 Device Harness, complete in 0.2.0

- authorised Android discovery
- metadata, battery and screen geometry
- screenshots
- fake transport
- timeouts and atomic output

## M2 Screen detection and calibration framework, complete in 0.3.0 to 0.5.0

- PNG decoder
- fingerprint anchors
- fail-closed screen states
- synthetic calibration
- local capture tools

The manual capture-approval route is retained only as a fallback.

## M3 Automatic navigation, complete in 0.6.0

- strictly allow-listed ADB tap and swipe transport
- state-driven setup from inventory to appraisal
- automatic swipe-through
- independent identity-change verification
- automatic end detection
- local screenshots and hashes
- checkpoint after every item
- resume verification
- device, geometry and battery controls
- deterministic fake phone and CI test
- no per-Pokémon user interaction

## M4 Automatic bootstrap and observation extraction, next

- automatically collect the core real screen states from a known start screen
- automatically build and validate a local core screen profile
- remove manual fixture promotion from the normal path
- verify current Calcy invocation and data output
- add a stable Calcy adapter interface
- attach structured species, CP, level, HP and IV data to each captured sequence item
- preserve raw evidence when Calcy returns incomplete data

## M5 Complete inventory database and exact identity

- SQLite scan runs
- observations and moves
- exact item fingerprints
- catch date, weight, height and status observations
- neighbour links
- gap and duplicate detection
- robust crash recovery

## M6 Species, PvP and collection intelligence

- evolution paths
- Great and Ultra League IV ranks
- versioned PvP relevance
- raid and Master League roles
- costumes, forms, old Pokémon and special moves

## M7 Full overnight inventory and plan

- 1,000-item validation
- complete 10,000+ inventory run
- KEEP / REVIEW / DELETE plan
- reason for every result
- no DELETE on unknown data

## M8 Automatic batch tagging

- exact match only
- batch-specific tags
- plan hash
- before and after audit
- rollback by tag removal
- no transfer

## M9 Manual transfer

- search by batch delete tag
- count reconciliation
- final human spot-check
- manual transfer only

## M4 phase 1 completed in 0.7.0

- automatic core profile bootstrap
- structured observation contract
- fake and failure providers
- checkpoint schema 2.0 migration

## M4 phase 2 next

- verify current Calcy IV version on the fixed phone
- implement the proven provider mechanism
- add freshness and stale-output protection
- run a one-Pokémon real verification before a long inventory scan

## M4 phase 2: Calcy evidence and parser foundation, version 0.8.0

Completed:

- named read-only app inspection
- package and installed-version discovery
- local process, accessibility, app-ops, service and log evidence
- automatic one-Pokémon live check
- profile-driven text parser
- synthetic CI verification

Next gate:

- run on the fixed Android phone
- select the provider mechanism from evidence
- verify 20 Pokémon with zero false Complete observations
