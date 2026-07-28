# Streaming Vision Phase 6B shadow integration

## Status

The additive Phase 6B package is integrated on
`feature/streaming-phase6b-shadow`. The supplied ZIP was verified before
installation with SHA-256
`0AAFD7A8BB54B1DD17073E7E6C71B72E520A09B014939092FF50EB324CE37178`.

The integration contains:

- `PogoInventory.Streaming.Semantics.Shadow`, with copied BGRA frame evidence,
  bounded parallel analyzers, fail-closed comparison categories and atomic
  JSON/Markdown reports;
- `PogoInventory.Streaming.Observe.Shadow`, an opt-in read-only scrcpy/FFmpeg
  live runner using the existing calibrated gate profile regions;
- a package-free 10-test self-test;
- explicit adapter points for Phase 6A semantic analyzers and a screenshot
  reference provider.

The runner never references `PogoInventory.Automation`, sends no phone input,
and reports `AuthorizesPhoneInput=false` and `InputCommandsSent=0`.

## Validation

Release solution build passed with 0 warnings and 0 errors. The Phase 6B
self-test passed 10/10; Phase 2/3/4/5/6A self-tests passed 3/3, 15/15, 7/7,
8/8 and 8/8; repository self-test passed 250/250.

A live read-only run against the authorized OnePlus A6013 on the existing
AppraisalBars state completed with 3 stable frames, no analyzer faults or
timeouts, zero input commands and clean bounded shutdown.

## Deliberate limitations

The repository does not yet contain a verified production EasyOCR/Ollama
semantic provider or a verified screenshot reference implementation. The
live command therefore emits `Unsupported` readings and cannot claim species,
CP or IV accuracy. No Details/wrong-screen real-phone acceptance is claimed
from this run; obtaining those states must not be done by adding navigation or
phone input to the shadow path. Phase 6B remains observation-only and does
not authorize tagging, transfer, delete or any Phase 7 behavior.
