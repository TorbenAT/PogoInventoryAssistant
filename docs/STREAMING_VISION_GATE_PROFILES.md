# Gate-profiler

Profiler kan vælges ved navn eller indlæses fra en JSON-fil.

## Indbyggede profiler

### StableHeaderAndPanel

Anbefalet stabilitetsprofil til Pokémon Details og Appraisal.

Påkrævet:

- Header
- Panel
- BottomControl

Volatile og ignoreret som stabilitetskrav:

- Model
- AnimatedBackground

### GenericScreenTransition

Dokumenterer stabil A, transition og stabil B. Overgang og progression vurderes på Header, Panel og BottomControl. Model og animeret baggrund kan ikke alene starte eller fuldføre gaten.

### GenericStableScreen

Kræver stabilitet i hele billedet. Bruges kun på skærme, der reelt kan blive visuelt stille.

### StableFullFrame

Samme sikkerhedsmodel som `GenericStableScreen`. Den er ikke egnet som autorisationsgrundlag på Pokémon-skærme med permanent animation.

## JSON-filer

Eksempler ligger under `profiles/`:

```text
GenericStableScreen.json
GenericScreenTransition.json
StableHeaderAndPanel.json
StableFullFrame.json
```

## Centrale tærskler

```json
{
  "requiredRegions": ["Header", "Panel", "BottomControl"],
  "minimumStableFrames": 3,
  "minimumStableDuration": "00:00:00.1500000",
  "maximumMotionScore": 0.05,
  "maximumDifferenceScore": 0.04,
  "minimumSimilarityScore": 0.94,
  "minimumSharpnessScore": 0.18,
  "maximumObservationDuration": "00:00:04"
}
```

Værdierne er prototypestandarder. De skal kalibreres mod real-phone evidence, før de bruges til at frigive telefoninput i en senere fase.

## Diversitet

Evidence kan kræve:

- minimumafstand i `FrameId`
- minimumafstand i tid
- maksimum visuel lighed

For stabilitetsbevis er standarden tidsmæssig og sekventiel afstand. Identiske motiver kan stadig være uafhængige frames, når de er modtaget på forskellige tidspunkter. Visuel lighedsgrænse kan strammes i en profil, når det er relevant.
