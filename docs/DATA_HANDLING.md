# Data handling

The repository may remain public while it contains only source code and synthetic data.

Before real inventory collection begins, make the repository private or keep every real file outside the repository.

## Never commit while public

- phone serial numbers
- real Pokémon GO screenshots
- account names or trainer identifiers
- full inventory exports
- capture manifests from a real phone
- local screen profiles generated from real screenshots
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
data\screen-fixtures-real\
```

The local real-screen profile should use the ignored path:

```text
data\screen-profile.local.json
```

The application should later support a configurable data directory outside the repository.

## Test fixtures

Only synthetic, cropped or manually redacted fixtures may be committed to a public repository. A fixture must not expose a real account, location, device serial, notification content or other personal information.

Before approving a real screenshot as a public fixture, verify that:

- trainer name is absent or redacted
- location information is absent
- notification banners are absent or redacted
- device identifiers are absent
- Pokémon details are not linked to a private inventory export
- the image is needed for a stable UI anchor rather than account-specific content
