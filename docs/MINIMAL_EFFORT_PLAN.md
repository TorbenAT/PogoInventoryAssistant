# Kodegennemgang og minimal-indsats-plan

**Dato:** 2026-07-21 · **Baseret på:** main @ `b4639d7` · **Mål:** Fra nuværende tilstand til et system, der reelt kan gennemgå inventaret og foreslå sletning via tags — med mindst mulig indsats.

---

## 1. Diagnose — hvorfor virker det ikke i dag

Koden er ikke "i stykker". Navigations-/sikkerhedslaget er solidt og telefon-accepteret (20-item carousel, canonical close, tag-mutation på navn, søgning — alt bevist på rigtig telefon). Problemet er, at **den semantiske kerne mangler helt**, og alt nedstrøms derfor degenererer:

| # | Fund | Konsekvens | Placering |
|---|------|-----------|-----------|
| 1 | **Ingen OCR/tekstlæsning overhovedet.** Eneste NuGet-pakke i hele løsningen er `Microsoft.Data.Sqlite`. Al vision er håndrullet pixelanalyse. | Art og CP kan ikke aflæses. | hele `src/` |
| 2 | **Art = søgequeryen, by design.** `--species`-argumentet gemmes uvalideret som `SpeciesName`. | `age0-1825` i alle rækker. | `Program.cs:1794` → `CleanupProofRunner.cs:263` |
| 3 | **CP hardcoded Unknown.** Aldrig tildelt nogen steder i cleanup-flowet. | CP mangler i alle rækker. | `CleanupProofRunner.cs:288` |
| 4 | **IV måles men nedgraderes.** `AppraisalAnalyzer` måler bars, men `Complete` kræver `profile.Verified` (20-case truth-gate aldrig gennemført), så alt er `Candidate` og field evidence "Unknown". | `HasKnownCriticalValues=false` → alt REVIEW. | `AppraisalAnalyzer.cs:86-95`, `CleanupProofRunner.cs:289-291` |
| 5 | **Ingen cross-run-identitet.** Fingerprint gemmes som byte-eksakt SHA-256; similarity-sammenligning (0.965) bruges kun run-internt. Ingen kode matcher på tværs af runs. `GroupKey` falder tilbage til unik per-instans-nøgle → selv in-run-gruppering bliver singletons. | Samme Pokémon genkendes aldrig igen — din mistanke bekræftet. | `PokemonIdentityModels.cs:101`, `FingerprintComparer.cs`, `PokemonObservation.cs:46-47` |
| 6 | **Ingen resume i cleanup-flowet.** Nyt `runId` hver start; checkpoint/resume findes kun i det ældre `InventoryAutomationRunner`-flow. | Kan ikke skalere til 10.000. | `CleanupProofRunner.cs:75` |
| 7 | **Tagging virker men er ikke koblet på.** `device-set-pokemon-tag` er telefon-accepteret (navnematch, verifikation, nul fejlvalg) — men tager én manuel Pokémon ad gangen; ingen pipeline fra anbefalinger. | Sidste led mangler. | `Program.cs:2754` |
| 8 | **Policy hardcoded i cleanup-flowet.** `new RulePolicy()` med defaults; ingen konfigfil, ingen referencedata (legendary/mythical-lister findes ikke). | Beskyttelsesregler kan ikke håndhæves reelt. | `CleanupProofRunner.cs:341` |
| 9 | **~50 % af kodebasen er scaffolding.** 26 af 50 CLI-kommandoer diagnostik; hele Calcy-kæden (4 projekter, ~3.900 LOC) ubrugt til produktmålet; iPhone-kæden (4 projekter, ~4.000 LOC) engangs; ~480 LOC dødt (ExplorationRecoveryService-klyngen, RunCoordinator/TagWorkflowService). | Vedligeholdelsesstøj, ikke blokerende. | se arkitekturafsnit |

**Kernekonklusion:** Én manglende komponent — tekstlæsning fra skærmen — blokerer art, CP, semantisk identitet, gruppering, DELETE-beslutninger og dermed hele produktværdien. Alt andet eksisterer allerede i brugbar stand.

---

## 2. Strategisk valg: brug Windows' indbyggede OCR

Worker C-prompten foreslår at håndrulle "den mindste deterministiske extraction". **Anbefales ikke.** Håndrullet tekstgenkendelse af et helt fontsæt er den dyre vej. I stedet:

- **`Windows.Media.Ocr`** (indbygget i Windows 11, tilgås fra .NET 8 via `Microsoft.Windows.CsWinRT` / TFM `net8.0-windows10.0.xxxxx`): offline, deterministisk nok, ingen native-DLL-cirkus, læser spil-UI-tekst (høj kontrast, ren font) meget pålideligt.
- Alternativ hvis TFM-ændring er uønsket: Tesseract NuGet. Windows OCR er dog mindst indsats på denne maskine.
- OCR-output valideres altid mod artslisten (referencedata) + CP-interval — så OCR-støj ikke kan blive til falske arter. Det matcher kravspec VIS-006/VIS-007 og genbruger eksisterende multi-frame-consensus-mønster.

Dette erstatter Worker C's Task 2's "smallest deterministic implementation" med noget færdigt og bedre. Resten af Worker C's opgaveliste (ROI-analyse, query-semantik, offline reprocess, konsensus) er stadig rigtig og genbruges.

---

## 3. Plan — trin i rækkefølge, mindst indsats først

### Trin 1 — OCR-spike offline på eksisterende evidence (≈1 dag)
Ingen telefon. Kør Windows OCR over de 200 gemte screenshots fra 20-item-kørslen (`local-data/validation/cleanup-value-proof/appraisal-carousel-20`). Mål: kan art + CP læses fra header-ROI på Details/AppraisalBars? Rapportér hit-rate.
**Go/no-go:** ≥19/20 art og CP → fortsæt. Ellers justér ROI/preprocessing (skalering, binarisering) før noget andet bygges.

### Trin 2 — `PokemonHeaderAnalyzer` + fix query-som-art (≈2-3 dage)
- Ny komponent i `PogoInventory.Vision` (eller nyt lille projekt pga. Windows-TFM): input frame → `{Species, Cp, Nickname, confidence, evidence}`. Multi-frame 2-af-3-konsensus (mønster findes allerede i `PokemonDetailsIdentityAnalyzer.Consensus`).
- Artsvalidering mod artsliste; nickname-detektion = header-tekst ∉ artsliste (VIS-007).
- Query-klassifikation: kun eksakt enkelt-arts-query må være `QueryDerived`; `age0-1825` m.fl. må aldrig blive art (VIS-001). Fjern `Species = species`-tildelingen i `CleanupProofRunner.BuildObservation`.
- Offline reprocess-kommando (`analyze-cleanup-evidence`) der genkører de 20 eksisterende rækker mod ny extractor og skriver ny SQLite-kopi — præcis som Worker C Task 7. Acceptmål: ≥19/20 art, ≥19/20 CP, 0 rækker med query som art.

### Trin 3 — Lås IV op (≈1 dag)
IV-målingen findes og virker. To ændringer:
- Multi-frame-konsensus på bar-måling (2 af 3 frames enige) → markér `Complete` uden Calcy-truth-gaten; Calcy-sporet parkeres helt.
- Sæt `FieldEvidence` for IV til `Automated` når konsensus. Dermed bliver `HasKnownCriticalValues` sand, og regelmotoren kan begynde at producere andet end REVIEW.

### Trin 4 — Semantisk identitet + dobbeltscanningstest (≈2 dage)
- `SemanticKey = art|variant|ivA|ivD|ivH|cp|nickname` (ID-005). Gem på Observations/PokemonRecords; brug som primær cross-run-nøgle. Fingerprint degraderes til run-intern progression (det er alt, den kan).
- `GroupKey`-fallback fixes samtidig, så gruppering bruger art+variant i stedet for per-instans-nøgle — ellers virker dubletanalysen stadig ikke.
- Acceptance: scan samme 200-scope to gange, mål re-match ≥99 %, 0 falske merges (ID-006 / trin H i kravspec). **Dette er go/no-go-porten for alt oprydningsarbejde.**

### Trin 5 — Resume + chunking i cleanup-flowet (≈2-3 dage)
- Genbrug mønstret fra `InventoryScanCheckpointRepository` (findes, testet, med schema-migration): cleanup-runner gemmer checkpoint pr. item og kan genstarte med `--resume` — spring allerede-sete semantiske nøgler over i stedet for fingerprints.
- Scan-plan med stabile partitioner (arter/`year`-filtre, ikke `age`) til flerdages fuld scanning.
- Acceptance: 200-item med kontrolleret stop/genstart uden dubletter (trin D).

### Trin 6 — Referencedata + konfigurerbar policy (≈1 dag)
- Commit statisk dump under `data/reference/`: artsliste, Legendary/Mythical/UB-klassifikation, CP-max (kilde: PogoAPI/GameMaster-eksport, version i filnavn).
- `RulePolicy` indlæses fra JSON-fil i stedet for `new RulePolicy()`; dump-version gemmes pr. kørsel (afsnit 21A + 33 i kravspec).

### Trin 7 — Tag-pipeline: anbefaling → telefon (≈3-4 dage)
Sidste led. Ny kommando `apply-recommended-tags --manifest`:
1. Læs godkendte kandidater fra SQLite (manifest).
2. Pr. kandidat: `device-search-inventory` med indsnævrende filter (art + CP, evt. `4*`-filter) — søgning er allerede accepteret.
3. Åbn kandidat, verificér mod record (art/CP/IV via header+appraisal) — TAG-001/002; flertydig → `TAG_AMBIGUOUS`, spring over.
4. Anvend tag via eksisterende `TagSelector`-flow (accepteret).
5. Reconciliation: søg `#AI-Delete` bagefter, afstem antal mod manifest (TAG-003).
Alt reversibelt; ingen transfer.

### Trin 8 — Fuld kørsel (drift, ikke udvikling)
Med trin 1-7 grønne: fuld read-only scanning i chunks over flere nætter → analyse → manifest → godkendelse → tagging. Transfer forbliver manuel i spillet (du sletter selv det taggede — sikrest og kræver nul ekstra kode).

### Bevidst UDELADT (spar indsatsen)
- **Calcy-integration** — droppes; egen IV-læsning + OCR dækker behovet. (~3.900 LOC parkeres, slettes ikke nødvendigvis.)
- **Automatisk transfer** — manuel sletning af `#AI-Delete`-taggede i spillet er hurtig og risikofri.
- **Fuld PvP-rangberegning** — behold den simple IV-heuristik → REVIEW.
- **Oprydning i scaffolding/dødt kode, doc-drift, Program.cs-opsplitning** — støj, ikke blokerende; tag det ad hoc.
- **Ny 50-item telefonkørsel før trin 2 er offline-grøn** — spilder telefontid på ubrugelige rækker (samme konklusion som din egen analyse).

---

## 4. Samlet estimat

| Trin | Indsats | Frigør |
|------|---------|--------|
| 1. OCR-spike | ~1 dag | go/no-go-viden |
| 2. HeaderAnalyzer + reprocess | ~2-3 dage | art + CP |
| 3. IV Complete | ~1 dag | regelmotor producerer KEEP/DELETE |
| 4. Semantisk identitet + test | ~2 dage | cross-run + gruppering |
| 5. Resume/chunking | ~2-3 dage | 10.000-skala |
| 6. Referencedata/policy | ~1 dag | reelle beskyttelsesregler |
| 7. Tag-pipeline | ~3-4 dage | slutproduktet |

**≈ 2-3 ugers fokuseret arbejde** — og de første 4 trin (den kritiske usikkerhed) er ~1 uge og næsten udelukkende offline mod eksisterende evidence.

---

## 5. Arkitekturnoter (baggrund, ingen handling krævet nu)

- 18 projekter; `Exploration` (4.693 LOC) er de facto automation-runtime men udokumenteret i `REPO_MAP.md` (som fejlagtigt siger 15 projekter og 114 tests; reelt 163).
- `Program.cs` er én fil på 3.568 linjer med alle 50 kommandoer.
- Dødt: `ExplorationRecoveryService`-klyngen (~280 LOC), `RunCoordinator`/`TagWorkflowService` (~200 LOC, kun testreferencer).
- To parallelle sekvens-hosts (`VerifiedInventoryTaskSequence` og `CleanupProofRunner`) — cleanup-runneren er den nyeste og den, planen bygger videre på.
- Ingen dependency-cykler; lagdeling afviger fra det tiltænkte, men fungerer.
