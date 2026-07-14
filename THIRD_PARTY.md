# Third-party status

Version 0.7.0 contains no copied source code from PGo-CalcaBotaBotaCalca, Calcy IV, PvPoke, Ohbem, scrcpy or other external projects.

The .NET projects use only the .NET 8 base class library and contain no NuGet package references.

The PNG decoder, fingerprint extractor and screen-state detector were implemented inside this repository for the project. The committed synthetic fixtures were generated specifically for testing and contain no Pokémon GO artwork or personal data.

The GitHub Actions workflow references the standard actions:

- `actions/checkout@v4`
- `actions/setup-dotnet@v4`
- `actions/upload-artifact@v4`

PGo-CalcaBotaBotaCalca and other projects remain possible future technical references. Their licences and current technical suitability must be reviewed before code or data is copied or integrated.


Version 0.7.0 defines an original adapter boundary for Calcy IV. It does not copy Calcy IV code and does not claim an undocumented integration method is working.
