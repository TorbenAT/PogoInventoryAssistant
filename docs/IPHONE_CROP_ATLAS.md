# iPhone crop atlas

Version 0.12.0 converts the accepted visual-region discovery report into
compact derived evidence.

The command selects strong candidate regions for:

- screen-state discrimination
- changing Pokémon-specific content
- text-dense content

It then selects deterministic representatives from every visual cluster,
crops each candidate region, and produces:

- `cluster-overview.png`
- one contact sheet per selected candidate
- individual crop PNG files
- JSON, Markdown and CSV manifests
- a readiness decision
- an explicit list of visual clusters that need more screenshots

The source screenshots are read only. No source image is modified. Generated
images are written under `out` and uploaded only as the workflow artifact.

Candidate labels remain provisional. A green crop atlas does not prove OCR,
species recognition, CP extraction or IV-bar interpretation.
