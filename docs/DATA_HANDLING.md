# Data handling

The repository may remain public while it contains only source code and synthetic data.

Before real inventory collection begins, make the repository private or keep all real data outside the repository.

## Never commit while public

- phone serial numbers
- real Pokémon GO screenshots
- account names or trainer identifiers
- full inventory exports
- capture manifests from a real phone
- SQLite inventory databases
- log files containing personal data
- authentication tokens or ADB keys

## Local folders

Use ignored local folders such as:

```text
captures\
local-data\
private\
out\
```

The application should later support a configurable data directory outside the repository.

## Test fixtures

Only synthetic, cropped or manually redacted fixtures may be committed to a public repository. A fixture must not expose a real account, location, device serial or notification content.
