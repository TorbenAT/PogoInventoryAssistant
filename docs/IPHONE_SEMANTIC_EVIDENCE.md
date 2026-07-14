# iPhone semantic evidence

Version 0.13.0 packages derived evidence for external visual inspection.

The pack contains:

- one case for every decoded screenshot
- one crop for every selected candidate region in every case
- the crop-atlas cluster overview
- all candidate contact sheets
- JSON, Markdown and CSV manifests
- an empty semantic truth template
- no copied full-size source screenshot

The truth template contains nullable fields for screen state, species, CP and
IV values. Every field starts null and every case starts unreviewed.

This prevents the pipeline from treating a visual guess as truth. A later
semantic extractor must be tested against at least twenty populated truth
cases and must produce zero false Complete results before it can be selected.

`readiness.needsMoreImages` does not block pack generation. It names only the
visual clusters that would benefit from additional screenshots.
