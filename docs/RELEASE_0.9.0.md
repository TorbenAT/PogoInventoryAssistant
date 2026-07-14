# Release 0.9.0

## M4 phase 3, evidence verification gate

This release does not fabricate a production Calcy adapter without real phone evidence. It adds the complete local harness needed to prove one candidate mechanism safely.

New capabilities:

- local 20-case verification workspace
- raw-source or parsed-observation evidence ingestion
- strict path containment and SHA-256 evidence hashes
- expected-versus-observed comparison of identity, CP and IV values
- explicit detection of wrong Complete observations
- JSON, Markdown and CSV verification reports
- conservative long-scan gate
- provider selection file locked to verification and parser hashes
- refusal to select a provider when the gate fails
- no new phone input actions

CI uses synthetic evidence only and must pass 78 self-tests plus the twenty-case provider gate.
