# Appraisal visual pretest

Version 0.14.0 uses the real iPhone screenshots to bootstrap a normalised
definition of the three appraisal bars.

The profile contains only definitions:

- normalised bar rectangles
- permitted translation and scale search values
- orange-fill colour limits
- neutral-track colour limits
- candidate and confidence thresholds

The analyser searches those definitions across the screenshot geometry. It does
not assume fixed pixels.

Outputs include candidate IV estimates, overlays, bar crops and
`appraisal-review-pack.zip`.

The committed profile is unverified. It can produce `Candidate` but never
`Complete`. Even a verified profile requires an explicit verified-provider enable flag.
