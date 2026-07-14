# Validation report

## Version

0.13.0

## Accepted prior checkpoint

Torben reported version 0.12.0 fully green in GitHub Actions.

## Static validation completed

- all project files parse as XML
- every project reference resolves
- all 14 projects remain present in the solution
- CLI imports both semantic evidence namespaces
- CLI contains the `image-semantic-evidence` command
- GitHub Actions runs and verifies the semantic evidence pack
- 103 self-tests are declared
- all committed JSON files parse
- ZIP integrity passes
- the review ZIP writer includes only derived crops, atlas evidence and
  manifests
- source screenshots are never written into the review ZIP
- semantic truth fields are created as null
- automated-extraction readiness is always false in this release

## Runtime validation delegated to GitHub Actions

The preparation environment does not contain the .NET SDK. GitHub Actions is
therefore authoritative for compilation and execution.

The green workflow must confirm:

- 103 of 103 self-tests
- at least twenty real semantic evidence cases
- one crop per selected region per case
- accepted review-pack generation
- unreviewed null truth values
- generated `semantic-review-pack.zip`
- explicit more-image readiness output

## Limitation

Version 0.13.0 prepares evidence for semantic review. It does not itself
determine screen state, species, CP or IV values.
