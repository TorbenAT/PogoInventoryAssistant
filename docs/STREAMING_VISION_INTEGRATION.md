# Integration i PogoInventoryAssistant

## 1. Kopiér projekterne

Kopiér:

```text
src/PogoInventory.Streaming
src/PogoInventory.Streaming.Scrcpy
src/PogoInventory.Streaming.Gates
src/PogoInventory.Streaming.Observe.Gates
tests/PogoInventory.Streaming.Phase3.SelfTest
profiles
```

Fase 3-pakken indeholder også Fase 2-projekterne, så den kan bygges selvstændigt.

## 2. Tilføj projekter til solution

```powershell
dotnet sln .\PogoInventoryAssistant.sln add .\src\PogoInventory.Streaming\PogoInventory.Streaming.csproj
dotnet sln .\PogoInventoryAssistant.sln add .\src\PogoInventory.Streaming.Scrcpy\PogoInventory.Streaming.Scrcpy.csproj
dotnet sln .\PogoInventoryAssistant.sln add .\src\PogoInventory.Streaming.Gates\PogoInventory.Streaming.Gates.csproj
dotnet sln .\PogoInventoryAssistant.sln add .\src\PogoInventory.Streaming.Observe.Gates\PogoInventory.Streaming.Observe.Gates.csproj
dotnet sln .\PogoInventoryAssistant.sln add .\tests\PogoInventory.Streaming.Phase3.SelfTest\PogoInventory.Streaming.Phase3.SelfTest.csproj
```

Hvis Fase 1 eller Fase 2 allerede er integreret, skal de eksisterende projekter sammenlignes og opdateres. Opret ikke dubletter.

## 3. Build først

```powershell
dotnet build .\src\PogoInventory.Streaming.Gates\PogoInventory.Streaming.Gates.csproj -c Release
dotnet build .\src\PogoInventory.Streaming.Observe.Gates\PogoInventory.Streaming.Observe.Gates.csproj -c Release
```

## 4. Kør self-test

```powershell
dotnet run --project .\tests\PogoInventory.Streaming.Phase3.SelfTest -c Release
```

Et fejlet testresultat skal blokere real-phone acceptance.

## 5. Read-only telefonacceptance

Start med `StableHeaderAndPanel`. Telefonen placeres manuelt på en Details- eller Appraisal-skærm. Kontroller:

- stabil gate kan bestå trods model- og baggrundsanimation
- `IgnoredMotionRegions` indeholder de volatile regioner
- `InputCommandsSent` er 0
- `LeasesOutstandingAtShutdown` er 0

Kør derefter `GenericScreenTransition`, hvor brugeren manuelt ændrer skærmen.

## 6. Ingen runtime-aktivering

Fase 3 må ikke automatisk kobles til eksisterende navigation eller telefoninput. Integrér kun projekter og diagnostik. Gate-resultater skal forblive observerende, indtil en senere fase har særskilt acceptance og autorisationsdesign.
