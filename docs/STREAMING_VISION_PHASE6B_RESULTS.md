# Streaming Vision Phase 6B real semantic results

Date: 2026-07-28. This report is fail-closed: the verified Task-K CSV is
manual screenshot truth, while analyzer output is never treated as truth.

## Offline verified replay

- 30 verified screenshot rows, 150 verified fields.
- `FalseKnown=0`, `FalseComplete=0`, `InputCommandsSent=0`.
- One persistent EasyOCR JSON-lines worker used queue capacity 1; dropped jobs: 0.
- Replay runs through `SemanticShadowRunner` with EasyOCR header, IV-bar
  geometry and the verified screenshot-reference provider.
- EasyOCR and IV geometry emit candidates as `Unknown` until agreement is
  established. Unknown is not converted to false or Known.
- HTML, crops and per-item JSON are under
  `local-data/validation/phase6b-real-semantic-results/offline-verified/`.

The first shadow replay showed one EasyOCR analyzer timeout in the first item
under the 3 second shadow budget. It remains reported and is not retried or
guessed away; this is not a production accuracy claim.

## Real-phone pilot

The authorized existing `device-run-index-sequence` operation completed the
3-item capture on `192.168.1.185:5555` with checkpoint `Completed`, ordinal 3,
zero tag application, and 21 named input audit records. The semantic provider
was not attached to the live capture stream, so this is captured-evidence
pilot data rather than live semantic acceptance. The 10-item pilot was not
started because the 3-item gate requires a clean provider run with no timeout
or backlog and zero shadow input.

No semantic path authorizes phone input. No tagging, transfer, evolve,
power-up, purify, TM, purchase, catch, spin, battle, raid or location-changing
function was added.

## Next gate

Resolve the EasyOCR timeout and attach the provider boundary to the existing
read-only shadow capture. Repeat the 3-item pilot and record clean shutdown,
zero leases, zero shadow input, zero backlog and a readable HTML report. Only
then may the 10-item pilot run.
