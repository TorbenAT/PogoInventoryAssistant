# iPhone screenshot analysis input

Place uncropped iPhone Pokémon GO screenshots in this directory.

The commands read PNG files only and never modify them:

```powershell
.\scripts\run-iphone-image-pretest.ps1
.\scripts\run-iphone-region-discovery.ps1
```

The first command validates decoding, geometry, similarity and visual clusters. The second measures stable, changing and cluster-discriminating normalised regions.

Neither command proves Android navigation, Android coordinates or Calcy integration.

The screenshots currently committed by Torben remain in this directory when a release ZIP is unpacked over the repository.
