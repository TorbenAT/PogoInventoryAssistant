# Streaming Vision Phase 6B shadow package

## Purpose

This additive package prepares the read-only Phase 6B shadow boundary. It copies
stable BGRA32 frames out of the existing lease system, runs one or more semantic
candidate analyzers, optionally compares their output with a screenshot-flow
reference provider, and writes bounded JSON/Markdown discrepancy reports.

The package does not create a scrcpy session, navigate the phone, invoke
Automation, authorize input, mutate tags, or make collection decisions.

## Main types

- `ShadowFrameFactory` copies an `IFrameLease` or selected gate frame set.
- `StreamingShadowFrameCapture` samples an already-running
  `IStreamingFrameSource`.
- `IShadowSemanticAnalyzer` is the adapter point for deterministic analyzers,
  OCR, embeddings and the separately developed Ollama/VLM work.
- `SemanticFieldAnalyzerAdapter<T>` wraps existing Phase 6A analyzers.
- `IShadowReferenceProvider` is the comparison boundary for the established
  screenshot flow.
- `SemanticShadowRunner` applies bounds, per-analyzer timeout, deterministic
  ordering and fail-closed fault handling.
- `ShadowComparisonEngine` records agreement, conflicts and coverage gaps. It
  never promotes a candidate into an action authorization.
- `ShadowReportWriter` writes atomic bounded reports without frame pixels.

## Read-only contract

Every `ShadowSessionReport` reports:

```text
AuthorizesPhoneInput = false
InputCommandsSent = 0
```

The project references Streaming, Streaming.Gates and Streaming.Semantics only.
It does not reference Device or Automation.

## Intended integration

After current Appraisal calibration and Ollama work are complete:

1. Create a clean Phase 6B branch from the final integration commit.
2. Copy this package without overwriting files.
3. Add both projects to the solution.
4. Build and run the Phase 6B self-test.
5. Add concrete adapters for:
   - the existing game-state detector,
   - the existing Appraisal analyzer,
   - the existing Details identity analyzer,
   - OCR and Ollama candidate providers.
6. Add an opt-in CLI composition that starts the already accepted read-only
   stream, consumes gate-selected frames and writes shadow reports.
7. Run real-phone shadow evidence without allowing any analyzer result to
   trigger navigation or input.

## Deliberate omissions

This package does not include a concrete Device/Automation reference, a phone
setup route, an Ollama implementation, PNG encoding or production decision
logic. Those are deliberately omitted to avoid conflict with work currently in
progress and to preserve the read-only boundary.
