# Continuation prompt

The Phase 6 baseline is ready for Phase 7 development, but Phase 7 is NOT
STARTED. Do not add any prohibited PokÃ©mon action. The final clean stream run
`stream-20260731T195333664Z-bc04d28b23614d6f` remains the canonical capture:
120/120 completed, 120 unique identities, 119 verified progressions, 600
evidence frames, Integrity PASS, zero semantic input, Clean shutdown and zero
leases.

Before any Phase 7 work, preserve the fail-closed contracts: Unknown and
Conflicting never prove progression; the five-frame moderate-IV path is
progression-only and cannot complete an inventory record. The offline
reference-safe species replay is accepted: it hash-verified all 600 saved
frames, raised Species Known from 114 to 119, produced zero contradictory
Known results and did not worsen conflicts. The original evidence remains
immutable and ignored under `local-data/validation/stream-reader-final-120`;
the derived replay report is under
`local-data/validation/stream-reader-final-120-species-replay`.

Use `FramesEvicted` for bounded-buffer retention telemetry. Do not describe it
as dropped decoder or transport frames.
