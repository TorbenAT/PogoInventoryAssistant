# Canonical semantic core and stream-backed capture

The pure `PogoInventory.Semantics` project contains shared item-evidence
contracts, two-frame species/CP consensus, confident two-frame IV-triplet
consensus and fail-closed conflict handling. Cleanup's IV consensus now calls
the same `SemanticConsensus` helper; the existing behavior is preserved by the
253/253 self-test suite.

`PogoInventory.Streaming.Semantics` contains `VisualFrame`, `FrameBarrier`,
and `BgraPixelBridge`. The bridge converts arbitrary-stride BGRA32 to tightly
packed RGBA32 while preserving alpha and ignoring padding. The barrier
requires a newer frame id, optional post-input timestamp, maximum age and
verified screen-state tag. These contracts have no Device, Automation or
Persistence reference.

The real-phone baseline attempt is currently blocked fail-closed in
`PokemonDetails`: the existing canonical close operation did not establish a
changed stable state. No forced navigation was added. Stream A/B and the 6-10
item HTML report remain pending that recovery gate. VLM remains outside
primary acceptance; no Phase 7, tagging, transfer, delete or Calcy was added.
