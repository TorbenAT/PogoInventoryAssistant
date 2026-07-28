# Historical delivery note

The original package-free delivery report below was produced before repository
integration and records that delivery environment's unavailable SDK. It is
kept as historical evidence; current integrated results are in
`VALIDATION_REPORT.md` and `docs/STREAMING_VISION_PHASE4.md`.

# Tests

Self-testprojekt:

```text
tests/PogoInventory.Streaming.Phase3.SelfTest
```

Kør:

```powershell
dotnet run --project .\tests\PogoInventory.Streaming.Phase3.SelfTest -c Release
```

Self-testen dækker:

- volatile Model og AnimatedBackground er ikke required
- reel pixelanalyse med bevægelig model og stabil header/panel
- regional stabilitetsgate
- required-region motion blokerer
- isoleret støjframe starter ikke transition
- stabil A -> transition -> stabil B
- baggrundsanimation kan ikke fake progression
- frameudvælgelse pr. region
- evidence-diversitet
- bounded session og eviction
- out-of-order frames
- lease-frigivelse
- AllOf, AnyOf og Sequence
- timeout, cancellation og fault
- freeze over flere frames
- ROI, stride og lifetime
- read-only public API
- determinisme

## Resultat i leverancemiljøet

```text
.NET SDK: NOT AVAILABLE
Build: NOT RUN
Phase 3 self-test: NOT RUN
Real-phone acceptance: NOT RUN
Input commands sent: 0
```

Der påstås ikke PASS uden faktisk build og kørsel.
