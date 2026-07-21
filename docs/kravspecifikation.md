# Kravspecifikation

## Pokémon GO Inventory Assistant

**Dokumenttype:** Samlet kravspecifikation og overdragelsesgrundlag
**Projekt:** `TorbenAT/PogoInventoryAssistant`
**Primær platform:** Windows, .NET 8/C#, Android-enhed via ADB
**Overordnet mål:** Automatisk, sikker og dokumenterbar gennemgang af et stort Pokémon GO-inventar med analyse og oprydningsforslag.

---

# 1. Formål

Systemet skal kunne gennemgå et Pokémon GO-inventar med op mod cirka 10.000 Pokémon uden løbende manuel betjening.

Systemet skal:

1. Overtage Pokémon GO fra den skærm, appen aktuelt står på.
2. Navigere sikkert til Pokémon-inventaret.
3. Gennemgå Pokémon én efter én.
4. Indsamle identitet, CP, IV, variant- og beskyttelsesoplysninger.
5. Gemme alle observationer i en lokal database.
6. Kunne genoptage en afbrudt scanning uden dubletter.
7. Analysere inventaret efter konfigurerbare regler.
8. Udpege Pokémon, der skal beholdes, vurderes eller er sandsynlige oprydningskandidater.
9. Dokumentere alle beslutninger med årsag og billedbevis.
10. Forblive ikke-destruktivt, indtil en særskilt og eksplicit godkendt oprydningsfase eventuelt aktiveres.

Systemet må ikke være afhængigt af, at brugeren manuelt stiller spillet på en bestemt skærm før hver kørsel.

---

# 2. Baggrund

Brugeren har et meget stort Pokémon GO-inventar, som ikke realistisk kan gennemgås manuelt Pokémon for Pokémon.

Manuel brug af værktøjer som Poké Genie eller Calcy IV er for langsomt og kræver for meget menneskelig kontrol. Målet er derfor et selvstændigt system, som:

* styrer telefonens brugerflade,
* aflæser Pokémon-oplysninger,
* bygger et komplet databasegrundlag,
* finder dubletter og dårligere eksemplarer,
* beskytter sjældne, gamle og værdifulde Pokémon,
* og producerer en konkret oprydningsplan.

Projektet skal bygges som et vedligeholdeligt produkt og ikke som en samling midlertidige scripts eller engangstests.

---

# 3. Projektvision

Den ønskede slutoplevelse skal være:

1. Brugeren tilslutter Android-telefonen.
2. Brugeren starter én kommando eller et lille kontrolprogram.
3. Systemet finder selv ud af, hvor Pokémon GO står.
4. Systemet åbner inventaret og starter eller genoptager scanningen.
5. Telefonen kan ligge urørt i flere timer eller natten over.
6. Systemet gemmer løbende resultater.
7. Efter scanning leveres:

   * fuld inventarliste,
   * kvalitets- og beskyttelsesanalyse,
   * dubletgrupper,
   * forslag til behold,
   * forslag til gennemgang,
   * forslag til oprydning,
   * forklaring og bevis for hver beslutning.
8. En senere oprydningsfase kan tagge kandidater i Pokémon GO, så brugeren kan kontrollere resultatet i spillet.

---

# 4. Succeskriterier

Projektet betragtes som operationelt, når følgende er dokumenteret:

| Område            | Krav                                                                             |
| ----------------- | -------------------------------------------------------------------------------- |
| Autonom opstart   | Systemet kan starte fra kendte Pokémon GO-skærme uden manuel klargøring          |
| Stabil navigation | Ingen forkerte, blinde eller destruktive input                                   |
| Lang scanning     | Mindst 200 Pokémon kan scannes med stop og genoptagelse                          |
| Fuld scanning     | Hele det valgte inventory-scope kan gennemløbes uden dubletter eller wraparound  |
| Datakvalitet      | Art, CP og IV aflæses for mindst 95 % af almindelige Pokémon                     |
| Persistens        | Hver Pokémon gemmes før næste swipe                                              |
| Resume            | Programstop og ADB-afbrydelse må ikke medføre tab eller genstart fra begyndelsen |
| Analyse           | Pokémon grupperes korrekt efter art og variant                                   |
| Forklarlighed     | Alle anbefalinger har regler, årsager og evidence                                |
| Sikkerhed         | Ingen Pokémon overføres, slettes eller ændres utilsigtet                         |
| Rapportering      | Resultater kan eksporteres til SQLite, JSON, CSV og læsbar rapport               |

---

# 5. Afgrænsning

## 5.1 Inkluderet

Projektet omfatter:

* Automatisk styring af Pokémon GO på Android.
* Navigation fra kendte spiltilstande.
* Automatisk åbning af Pokémon-inventaret.
* Brug af Pokémon GO-søgefiltre.
* Gennemgang via en vedvarende Appraisal-carousel.
* Lokal billedanalyse og eventuelt lokal OCR.
* Aflæsning af art, CP og IV.
* Aflæsning eller klassifikation af variant- og beskyttelsesoplysninger.
* SQLite-database.
* Checkpoint og resume.
* Analyse og beslutningsmotor.
* Dublet- og sammenligningsanalyse.
* Rapportering.
* Mulighed for senere automatisk tagging.
* Komplet audit trail.

## 5.2 Ikke inkluderet i første produktionsversion

Følgende må ikke være aktiveret i første produktionsversion:

* Automatisk transfer/sletning af Pokémon.
* Automatisk Power Up.
* Automatisk Evolve.
* Automatisk Purify.
* Køb eller brug af genstande.
* Automatisk ændring af Favorite-status.
* Automatisk brug af eksterne apps som Calcy IV.
* Cloudbaseret billedanalyse, der kræver manuel godkendelse.
* Handling på login-, betalings- eller kontoskærme.

Automatisk transfer kan kun behandles som en senere, separat og strengere sikkerhedsfase.

---

# 6. Brugere og aktører

## 6.1 Primær bruger

Ejeren af Pokémon GO-kontoen, som:

* starter scanning,
* vælger scan-scope,
* kontrollerer rapporter,
* godkender regler,
* og eventuelt godkender tagging eller senere oprydning.

## 6.2 Systemkomponenter

* Windows-host.
* .NET-applikationen.
* Android-enheden.
* Pokémon GO.
* ADB-transport.
* Vision- og OCR-komponenter.
* SQLite-database.
* Analyse- og policy-motor.
* Rapportgenerator.

---

# 7. Teknisk ramme

## 7.1 Repository

```text
https://github.com/TorbenAT/PogoInventoryAssistant
```

## 7.2 Teknologi

* .NET 8
* C#
* Windows
* SQLite
* ADB
* Lokale screenshots
* Deterministisk billedanalyse
* Lokal OCR, hvor det er nødvendigt
* GitHub Actions
* Enheds-, integrations- og real-phone-tests

## 7.3 Overordnet arkitektur

Systemet bør opdeles i følgende lag:

1. **Device/Transport**

   * kommunikation med Android,
   * screenshots,
   * tap og swipe,
   * ADB-forbindelse.

2. **Vision**

   * skærmklassifikation,
   * control locators,
   * art-, CP- og IV-analyse,
   * visuel fingerprinting.

3. **Automation**

   * navngivne og autoriserede handlinger,
   * navigation,
   * recovery,
   * carousel-scanning.

4. **Application**

   * scan orchestration,
   * checkpoints,
   * resume,
   * analyseflow.

5. **Persistence**

   * SQLite,
   * transaktioner,
   * lifecycle,
   * events og audit.

6. **Analysis**

   * regelmotor,
   * dubletsammenligning,
   * PvP-regler,
   * anbefalinger.

7. **CLI/UI**

   * start,
   * resume,
   * status,
   * rapportgenerering.

---

# 8. Centrale begreber

| Begreb                 | Definition                                                                  |
| ---------------------- | --------------------------------------------------------------------------- |
| Observation            | Data aflæst fra én Pokémon på ét tidspunkt                                  |
| Pokémon-record         | Systemets aktuelle samlede repræsentation af en Pokémon                     |
| Scan-run               | Én konkret scanning eller genoptagelse                                      |
| Fingerprint            | Visuelt eller semantisk kendetegn anvendt til progression og dubletkontrol  |
| Checkpoint             | Gemte oplysninger, der gør det muligt at genoptage scanning                 |
| Evidence               | Screenshot, hash og analyseoplysninger bag et felt eller en beslutning      |
| Strict recommendation  | Konservativ anbefaling fra den sikre policy-motor                           |
| Comparative suggestion | Rådgivende sammenligning mellem sandsynlige dubletter                       |
| Cleanup manifest       | En godkendt liste over planlagte oprydningshandlinger                       |
| Canonical close        | Pokémon GOs visuelle, nederste, centrerede lukkeknap                        |
| Appraisal-carousel     | Gennemgang af flere Pokémon ved at holde Appraise åbent og swipe mellem dem |

---

# 9. Samlet end-to-end-flow

Systemets normale proces skal være:

```text
Start
→ forbind til telefon
→ identificér nuværende skærm
→ normalisér kendt spiltilstand
→ GameplayMap
→ åbn menu
→ åbn Pokémon Inventory
→ anvend scan-query
→ åbn første Pokémon
→ gem første Details-observation
→ åbn Appraise én gang
→ aflæs art, CP og IV
→ gem observation og checkpoint
→ swipe til næste Pokémon i Appraise
→ gentag
→ registrér filterafslutning eller chunkgrænse
→ luk Appraise én gang
→ returnér til GameplayMap
→ luk og genåbn database
→ analyser databaseindhold
→ generér rapporter
```

---

# 10. Krav til autonom opstart

## AUT-001 – Ingen manuel skærmklargøring

Programmet skal kunne startes, uanset om Pokémon GO står på:

* GameplayMap,
* MainMenu,
* Inventory,
* filtreret Inventory,
* Pokémon Details,
* Pokémon-menu,
* Appraisal Intro,
* Appraisal Bars,
* kendt informationspopup,
* kendt confirmation-dialog.

## AUT-002 – Canonical close

Systemet skal visuelt identificere Pokémon GOs kanoniske nederste lukkeknap.

Den må ikke identificeres alene ud fra en fast koordinat.

Identifikationen skal mindst bruge:

* forventet lower-centre-zone,
* cirkulær knapform,
* krydsende diagonale linjer,
* størrelse,
* kontrast,
* stabilitet på tværs af flere frames.

Systemet skal afvise:

* søgefeltets ryd-×,
* tag-fjernelses-×,
* små topplacerede lukkeknapper,
* vilkårlige kryds i grafikken,
* affirmative confirmation-knapper.

## AUT-003 – Ét input ad gangen

Efter hvert input skal systemet:

1. tage nye screenshots,
2. klassificere skærmen,
3. kontrollere postcondition,
4. og først derefter autorisere næste handling.

## AUT-004 – Appraisal exit

Da Appraisal ikke altid har den kanoniske lukkeknap, skal systemet have en særskilt, navngivet og visuelt autoriseret Appraisal-exit-operation.

## AUT-005 – Ukendt skærm

En ukendt skærm må ikke udløse tilfældige tap eller swipes.

Systemet må:

* observere yderligere frames,
* genklassificere,
* eller stoppe sikkert med evidence.

---

# 11. Krav til navigation og handlinger

## NAV-001 – Navngivne operationer

Alle input skal udføres gennem navngivne operationer, eksempelvis:

* `open-main-menu`
* `open-inventory`
* `open-first-pokemon`
* `open-details-menu`
* `open-appraisal`
* `continue-appraisal-intro`
* `appraisal-next-pokemon-swipe`
* `exit-appraisal`
* `close-canonical-screen`

Rå ADB-input må ikke kaldes direkte fra orchestration-kode.

## NAV-002 – Precondition

Før et input skal følgende være dokumenteret:

* forventet state,
* visuel target-lokation,
* ingen konfliktende state,
* ingen uautoriseret confirmation,
* frisk screenshot umiddelbart før input.

## NAV-003 – Postcondition

Efter et input skal systemet bevise, at den forventede state eller progression er nået.

## NAV-004 – Ingen blind retry

Systemet må ikke sende samme tap eller swipe igen, blot fordi første handling ikke straks kunne observeres.

Der skal først etableres en ny stabil state.

## NAV-005 – Recovery-budget

Recovery skal være bounded.

Konfigurerbare grænser skal findes for:

* antal recovery-incidents pr. run,
* antal inputs pr. incident,
* antal observationer,
* timeout,
* loop-detection.

---

# 12. Krav til scanning

## SCAN-001 – Vedvarende Appraisal-carousel

Appraise skal åbnes én gang for en sekvens.

For `N` Pokémon skal normalflowet være:

* én åbning af Appraise,
* `N-1` swipes i Appraise,
* én afslutning af Appraise.

Systemet må ikke lukke Appraise og åbne det igen for hver Pokémon.

## SCAN-002 – Første Pokémon

Den første Pokémon skal først registreres fra Details, så der eksisterer et baseline-record, før Appraise åbnes.

## SCAN-003 – Efterfølgende Pokémon

Efterfølgende Pokémon må registreres direkte fra stabile Appraisal-frames.

## SCAN-004 – Persistens før progression

Alle tilgængelige data for den aktuelle Pokémon skal være gemt og checkpointet, før næste swipe udføres.

## SCAN-005 – Unikke ordinals

Hver observation i et scan-run skal have et unikt og fortløbende ordinal.

## SCAN-006 – Filterafslutning

Filterafslutning må kun erklæres, når:

* præcis ét swipe er sendt,
* postframes er stabile,
* fingerprint ikke ændrer sig,
* og ingen transition blev observeret.

## SCAN-007 – Wraparound

Systemet skal opdage, hvis carousel bevæger sig tilbage til en tidligere Pokémon.

Tidligere sete fingerprints må ikke gemmes som nye observationer.

## SCAN-008 – Chunking

En scanning skal kunne opdeles i chunks, eksempelvis:

* 20,
* 50,
* 200,
* 500 Pokémon.

Chunkstørrelse skal være konfigurerbar.

## SCAN-009 – Fuldt inventory

En fuld scanning skal bruge en dokumenteret scan-plan, så hele det ønskede inventory dækkes.

Scan-planen skal kunne anvende:

* brede søgefiltre,
* aldersintervaller,
* artspartitioner,
* tags,
* eller andre ikke-overlappende partitioner.

**Krav om stabile partitioner:** Relative filtre som `age0-1825` forskyder sig fra dag til dag. En fuld scanning tager ved 15 s/Pokémon × 10.000 ≈ 42 timer og strækker sig derfor over flere dage. Scan-planer, der spænder over mere end én kalenderdag, skal derfor bruge absolutte/stabile partitioner (fx `year2023`, artspartitioner, tags) — ikke relative age-intervaller. Age-filtre er kun tilladt til enkeltdags-chunks, og partitionens faktiske dato-interval skal gemmes sammen med scan-planen. Samme regel gælder for før/efter-scanninger under LIFE-001, hvor scope skal være reproducerbart.

Systemet skal dokumentere:

* inkluderet scope,
* ekskluderet scope,
* overlap,
* dubletter,
* forventet og faktisk antal.

---

# 13. Krav til resume og selvreparation

## RES-001 – Checkpoint efter hver Pokémon

Checkpoint skal gemmes efter hver persisteret Pokémon.

Checkpointet skal mindst indeholde:

* scan-run,
* query/segment,
* seneste ordinal,
* seneste fingerprint,
* allerede sete fingerprints,
* aktuel carousel-position,
* tidspunkt,
* database-path,
* forventet næste handling.

## RES-002 – Genoptagelse

Efter programstop skal brugeren kunne starte samme scan med `Resume`.

Systemet skal:

1. åbne databasen,
2. validere checkpoint,
3. normalisere telefonens state,
4. finde den relevante scan-query,
5. navigere tilbage til carousel,
6. springe allerede registrerede fingerprints over,
7. fortsætte ved første ukendte Pokémon.

## RES-003 – ADB-afbrydelse

Ved ADB-afbrydelse skal systemet:

* stoppe alle input,
* bevare database og checkpoint,
* forsøge bounded reconnect,
* fortsætte uden dubletter, når forbindelsen er stabil.

## RES-004 – App-genstart

Hvis Pokémon GO er lukket eller genstartet, må systemet genåbne appen, når dette kan ske gennem en navngivet og godkendt operation.

Login-, konto- eller mandatory-update-skærme er hard stops.

## RES-005 – Recovery fra kendte states

Systemet skal kunne genoptage fra:

* Appraisal,
* Details,
* Inventory,
* GameplayMap.

## RES-006 – Loop detection

Samme state/action-par må ikke gentages uden visuel progression.

---

# 14. Dataindsamling

## 14.1 Obligatoriske datafelter

Hver Pokémon-observation skal kunne indeholde:

| Felt                  | Type                        |
| --------------------- | --------------------------- |
| Run ID                | Tekst                       |
| Lokal Pokémon-ID      | Tekst                       |
| Ordinal               | Heltal                      |
| Observationstidspunkt | Timestamp                   |
| Art                   | Tekst/Unknown               |
| Nickname              | Tekst/Unknown               |
| CP                    | Heltal/Unknown              |
| Attack IV             | 0–15/Unknown                |
| Defense IV            | 0–15/Unknown                |
| HP IV                 | 0–15/Unknown                |
| Total IV              | 0–45/Unknown                |
| Form                  | Tekst/Unknown               |
| Costume               | Tekst/Unknown               |
| Shiny                 | True/False/Unknown          |
| Shadow                | True/False/Unknown          |
| Purified              | True/False/Unknown          |
| Lucky                 | True/False/Unknown          |
| Dynamax               | True/False/Unknown          |
| Gigantamax            | True/False/Unknown          |
| Background            | Tekst/Unknown               |
| Favorite              | True/False/Unknown          |
| Special move          | True/False/Unknown          |
| XXL                   | True/False/Unknown          |
| XXS                   | True/False/Unknown          |
| Catch date/age        | Dato/Unknown                |
| Catch location        | Tekst/Unknown               |
| Eksisterende tags     | Liste/Unknown               |
| Appraisal fingerprint | Hash                        |
| Screenshots           | Liste                       |
| Screenshot hashes     | Liste                       |
| Identity confidence   | Tal/enum                    |
| Protection confidence | Tal/enum                    |
| Observation status    | Complete/Partial/Unresolved |

## 14.2 Evidence source

Hvert felt skal have en kilde:

* `Automated`
* `QueryDerived`
* `EvidenceReviewed`
* `Unknown`

Et felt må ikke markeres `Automated`, hvis det reelt er manuelt aflæst.

## 14.3 Unknown-semantik

Unknown betyder ikke False.

Ukendt shiny-status må eksempelvis ikke behandles som “ikke shiny”.

---

# 15. Art- og CP-analyse

## VIS-001 – Art må ikke udledes af en bred query

En query som:

```text
age0-1825
```

må aldrig gemmes som Pokémon-art.

Art må kun være `QueryDerived`, når queryen dokumenteret indeholder præcis én art.

## VIS-002 – Header-analyse

Systemet skal have en permanent `PokemonHeaderAnalyzer`, der kan analysere:

* Pokémon Details,
* Appraisal Bars.

Output skal mindst omfatte:

* Species,
* CP,
* Nickname,
* confidence,
* source screen,
* evidence hash,
* failure reasons.

## VIS-003 – Multi-frame consensus

Art og CP skal normalt kræve mindst to overensstemmende resultater blandt tre kompatible frames.

## VIS-004 – CP-validering

CP skal:

* komme fra den definerede CP-ROI,
* være et positivt heltal,
* være inden for et gyldigt Pokémon GO-interval,
* være stabilt på tværs af frames.

## VIS-005 – OCR-fejl

Usikre tegn må ikke automatisk “rettes” til et sandsynligt resultat uden evidence.

## VIS-006 – UI-labels

Ord som følgende må ikke kunne accepteres som art:

* CP,
* Appraise,
* Attack,
* Defense,
* HP,
* Cancel,
* Done.

## VIS-007 – Omdøbte Pokémon (nickname skjuler art)

Header viser nickname i stedet for art, når en Pokémon er omdøbt. Da beskyttelsesreglerne netop omfatter trade-nicknames, vil en væsentlig del af inventaret være omdøbt.

Krav:

1. Systemet skal detektere, at header-teksten sandsynligvis er et nickname (tekst matcher ikke den kendte artsliste, jf. afsnit 21A).
2. For omdøbte Pokémon skal art bestemmes ad anden vej, i prioriteret rækkefølge:
   * arts-tekst andetsteds på skærmen (fx Details-typelinje), hvis tilgængelig,
   * sprite-/silhuet-klassifikation af Pokémon-modellen,
   * ellers `Species = Unknown` → recommendation `REVIEW`.
3. Nickname og art skal altid gemmes som separate felter; header-tekst må aldrig gemmes som art uden validering mod artslisten.
4. 95 %-coverage-kravet (afsnit 4) måles separat for omdøbte og ikke-omdøbte Pokémon, så et lavt tal for omdøbte ikke skjules i gennemsnittet.

---

# 16. IV-analyse

## IV-001 – Appraisal-bars

Attack, Defense og HP-IV skal aflæses fra Pokémon GOs Appraisal-bars.

## IV-002 – Resultater

IV-analyse skal returnere:

* Complete,
* Partial,
* Unavailable.

## IV-003 – Partial må ikke stoppe scanning

Manglende sikker IV-aflæsning må ikke slette baseline-recordet eller nødvendigvis stoppe carousel.

## IV-004 – Evidence

Hver IV-værdi skal være knyttet til:

* screenshot,
* ROI,
* analyseprofil,
* confidence,
* hash.

---

# 17. Fingerprinting og identitet

Pokémon GO stiller ikke en stabil intern Pokémon-ID til rådighed via brugerfladen. Systemet skal derfor være konservativt.

## ID-001 – Run-scoped identity

Hver Pokémon skal have et sikkert run-scoped ID:

```text
<run-id>:<ordinal>
```

## ID-002 – Appraisal fingerprint

Fingerprint skal baseres på stabile relevante områder, eksempelvis:

* arts-/navneområde,
* CP-område,
* appraisal-bars,
* appraisal stars,
* stabile header-elementer.

Animerede områder skal undgås, hvor det er muligt.

## ID-003 – Ikke permanent identitetsgaranti

Et visuelt fingerprint må ikke uden videre betragtes som en permanent Pokémon-ID på tværs af alle fremtidige scanninger.

## ID-004 – Cross-run matching

Match på tværs af runs skal have confidence og kan bruge:

* art,
* variant,
* IV,
* CP,
* nickname,
* catch date,
* location,
* tags,
* tidligere fingerprints.

Ved tvivl skal poster forblive separate eller markeres til review.

## ID-005 – Semantisk identitetsnøgle

Ud over det visuelle fingerprint skal systemet beregne en semantisk identitetsnøgle pr. Pokémon, sammensat af stabile, aflæste felter, eksempelvis:

* art,
* form/variant (shiny, shadow, purified, costume, background),
* Attack/Defense/HP IV,
* CP,
* nickname (når til stede),
* catch date (når til stede).

Den semantiske nøgle er primær ved cross-run-matching; det visuelle fingerprint er sekundært og run-internt (progression, wraparound, dubletkontrol i samme run).

Kollisioner (flere Pokémon med identisk semantisk nøgle) skal registreres eksplicit og må ikke auto-merges — de forbliver separate records med `ReviewRequired` ved cross-run-match.

## ID-006 – Målt re-identifikationsrate (kritisk)

Cross-run-identitet må ikke antages — den skal måles.

Krav:

1. Der skal findes en dedikeret acceptance-test: samme scope (mindst 200 Pokémon) scannes to gange med programgenstart imellem, uden ændringer i inventaret.
2. Mindst 99 % af Pokémon fra første kørsel skal re-matches til samme record i anden kørsel.
3. Ingen falske merges: to forskellige Pokémon må aldrig matches til samme record (0 tolerance).
4. Re-match-raten skal rapporteres pr. kørsel i `scan-coverage.json`.

Hele oprydningsworkflowet (tagging, cleanup-manifest, `DeletedConfirmed`) er blokeret, indtil dette krav er bevist grønt. Falder raten under målet, skal arkitekturen revurderes (mere vægt på semantisk nøgle, mindre på billedhash), før der bygges videre.

---

# 18. Databasekrav

## 18.1 Databaseteknologi

SQLite anvendes som lokal database i første produktionsversion.

## 18.2 Krævede tabeller

Databasen skal mindst indeholde:

### ScanRuns

* RunId
* SearchQuery
* ScanPlanId
* StartedAt
* CompletedAt
* Status
* StopReason
* RequestedItems
* CapturedItems
* Device
* SourceDirectory

### PokemonRecords

* LocalPokemonId
* CurrentLifecycleState
* aktuelle semantiske værdier
* confidence
* seneste observation
* nuværende recommendation
* recommendation reason
* comparator
* evidence-referencer

### Observations

* RunId
* LocalPokemonId
* Sequence
* observationstidspunkt
* alle observerede felter
* fingerprint
* evidence
* status

### InventoryEvents

Eksempelvis:

* Observed
* AppraisalEnriched
* SemanticEnriched
* RecommendationGenerated
* Tagged
* MissingObserved
* CleanupApproved
* DeletedConfirmed

### Checkpoints

* run,
* segment,
* ordinal,
* fingerprint,
* state,
* resume-data.

### Evidence

* path,
* hash,
* type,
* field,
* source,
* tidspunkt.

### TagAssignments

* Pokémon-ID,
* tag,
* status,
* tidspunkt,
* run.

### CleanupManifests

* manifest-ID,
* kandidater,
* approval,
* policy-version,
* status.

## 18.3 Transaktioner

Hver Pokémon skal gemmes transaktionelt.

En observation må ikke delvist opdatere databasen.

## 18.4 Database reopen

Analyse og rapporter skal genereres efter:

1. skriveforbindelsen er lukket,
2. databasen er genåbnet gennem en ny serviceinstans,
3. data er læst tilbage fra SQLite.

Rapporter må ikke alene baseres på objekter, der stadig ligger i hukommelsen.

## 18.5 Integritet

Hver kørsel skal kontrollere:

```sql
PRAGMA integrity_check;
```

Resultatet skal være `ok`.

---

# 19. Lifecycle

Foreslåede lifecycle-states:

* `Active`
* `Observed`
* `CleanupCandidate`
* `ReviewRequired`
* `TransferPlanned`
* `MissingOnce`
* `DeletedConfirmed`
* `UnexpectedMissing`
* `SupersededObservation`

## LIFE-001 – DeletedConfirmed

En Pokémon må kun markeres `DeletedConfirmed`, når:

1. der findes en komplet før-scanning,
2. Pokémon indgår i et godkendt cleanup-manifest,
3. der findes en komplet efter-scanning med samme scope,
4. kandidaten ikke længere findes,
5. de forventede keep-Pokémon stadig findes,
6. match-confidence er tilstrækkelig.

Fravær i én ufuldstændig scanning er ikke bevis for sletning.

---

# 20. Regelmotor

Regelmotoren skal være versionsstyret og konfigurerbar.

## 20.1 Beslutningskategorier

### KEEP

Pokémon skal bevares.

### REVIEW

Data eller regler er ikke tilstrækkelige til sikker oprydning.

### DELETE-CANDIDATE

Pokémon er en sikker oprydningskandidat efter strict policy.

### LIKELY_DELETE_SUGGESTION

Rådgivende sammenligningsresultat, som kræver kontrol af manglende beskyttelsesfelter.

---

# 21. Beskyttelsesregler

Følgende skal som udgangspunkt beskyttes eller sendes til review:

* 4*/15-15-15.
* Shiny.
* Mythical.
* Legendary.
* Ultra Beast.
* Costume.
* Background/location card.
* Shadow.
* Purified.
* Lucky.
* Dynamax.
* Gigantamax.
* Favorite.
* Special/legacy move.
* XXL.
* XXS.
* Gamle Pokémon før konfigureret dato.
* Pokémon med Trade-tag.
* Pokémon med trade-relateret nickname.
* Pokémon med lang catch-distance.
* Pokémon med ukendt kritisk beskyttelsesdata.
* Pokémon med ukendt variantidentitet.
* PvP-kandidater.
* Arter med særlige udviklings- eller handelsbehov.

Regler skal kunne tilpasses uden kodeændring.

---

# 21A. Referencedata

Regelmotoren og valideringen kræver eksterne spildata, som skal indgå som versioneret leverance:

* komplet artsliste (til OCR-validering af art og nickname-detektion, jf. VIS-007),
* klassifikation pr. art: Legendary, Mythical, Ultra Beast,
* gyldige CP-intervaller (globalt og pr. art, hvor muligt),
* legacy-/special-move-lister, hvor relevant,
* evolutionslinjer (til PvP- og udviklingsregler).

Krav:

1. Datakilden skal være en dokumenteret, lokalt cachet dump (fx GameMaster/PogoAPI-eksport) — ingen live-kald under scanning.
2. Dumpens version/dato skal gemmes sammen med hver kørsel og hver policy-evaluering.
3. Manglende opslag (art ikke i listen) skal behandles som Unknown → REVIEW, aldrig som "ikke beskyttet".
4. Opdatering af referencedata skal kunne ske uden kodeændring.

---

# 22. Dubletanalyse

## ANA-001 – Sammenligningsgruppe

Pokémon må kun sammenlignes som dubletter, når de tilhører samme kompatible gruppe:

* samme art,
* samme form,
* samme costume,
* samme shiny-status,
* samme shadow/purified-status,
* samme background,
* samme relevante specialvariant.

## ANA-002 – Rangering

Standardrangering:

1. Total IV.
2. CP.
3. Konfigureret rolle eller formål.
4. Stabil ordinal som sidste tie-breaker.

## ANA-003 – Minimumsbeholdning

Policy skal definere et minimumsantal, der altid beholdes pr. gruppe.

## ANA-004 – Strictly better comparator

En oprydningskandidat skal have en navngivet comparator, som er strengt bedre efter policyens kriterier.

## ANA-005 – Sidste eksemplar

Det sidste kendte eksemplar i en gruppe må ikke markeres til sletning.

---

# 23. PvP-analyse

Systemet skal som minimum kunne lave en foreløbig PvP-beskyttelse baseret på:

* lav Attack IV,
* høj Defense IV,
* høj HP IV.

En senere version bør beregne:

* Great League-rang,
* Ultra League-rang,
* Little Cup-rang,
* relevante evolutionsformer,
* nødvendige levels og CP-lofter.

Indtil fuld PvP-beregning findes, skal sandsynlige kandidater markeres `REVIEW`, ikke slettes.

---

# 24. Oprydningsworkflow

## 24.1 Fase 1 – Analyse

Systemet genererer:

* KEEP,
* REVIEW,
* DELETE-CANDIDATE,
* advisory suggestions.

Ingen ændringer udføres i spillet.

## 24.2 Fase 2 – Tagging

Efter separat godkendelse kan systemet anvende tags som:

* `AI-Keep`
* `AI-Review`
* `AI-Delete`
* `Trade`
* `PvP`
* `Old`
* `Distance`

Tagging skal være reversibel og auditeret.

### TAG-001 – In-game-genfinding før tag

Før et tag anvendes, skal systemet lokalisere præcis dét eksemplar i spillet, som recorden i databasen refererer til. Genfinding skal ske via et sammensat, maksimalt indsnævrende søgefilter afledt af recordens semantiske nøgle (jf. ID-005), eksempelvis art + CP-interval + IV-stjernefilter (`4*`/`3*` osv.) kombineret efter behov.

### TAG-002 – Visuel verifikation før tag

Efter genfinding og åbning af kandidaten skal systemet, før tagget anvendes, verificere at den åbnede Pokémon matcher recorden på:

* art (eller nickname),
* CP,
* IV (via Appraisal eller stjerneindikator),
* relevante variantfelter.

Ved mismatch eller flertydighed (flere eksemplarer matcher filteret og kan ikke skelnes) skal tagging af den record springes over og registreres som `TAG_AMBIGUOUS` — aldrig gættes.

### TAG-003 – Tag-reconciliation

Efter en tagging-kørsel skal systemet verificere resultatet ved at søge på hvert anvendt tag og afstemme antal taggede mod manifestet. Afvigelser rapporteres; manglende eller overskydende tags må ikke rettes automatisk uden ny godkendelse.

## 24.3 Fase 3 – Cleanup-manifest

Systemet kan generere en fast liste over Pokémon, der foreslås ryddet op.

Manifestet skal indeholde:

* Pokémon-ID,
* observation,
* art,
* CP,
* IV,
* comparator,
* årsag,
* alle beskyttelseschecks,
* evidence,
* policy-version.

## 24.4 Automatisk transfer

Automatisk transfer skal være deaktiveret som standard.

Det må kun udvikles og aktiveres efter en særskilt kravspecifikation og acceptance, som blandt andet kræver:

* eksakt identity,
* fuld beskyttelsesdata,
* godkendt manifest,
* tørkørsel,
* separat brugerautorisation,
* confirmation-verifikation,
* efterfølgende inventory-reconciliation.

---

# 25. Rapporter

Systemet skal generere:

* `cleanup-proof.sqlite`
* `captured-observations.json`
* `semantic-review.json`
* `db-roundtrip.json`
* `strict-recommendations.csv`
* `comparative-cleanup-suggestions.csv`
* `recommendations.md`
* `proof-summary.md`
* `checkpoint.json`
* `recovery-events.json`
* `scan-coverage.json`
* `scan-errors.json`

## 25.1 Rapportindhold

Rapporterne skal mindst vise:

* scan-scope,
* antal forventede og scannede Pokémon,
* coverage,
* art/CP/IV-coverage,
* Complete/Partial/Unresolved,
* dubletter,
* strict recommendations,
* comparative suggestions,
* comparatorer,
* manglende beskyttelsesfelter,
* recovery-incidents,
* databaseintegritet,
* sikkerhedstællere,
* endelig telefonstate.

---

# 26. Betjeningskrav

Der skal findes en enkel launcher, eksempelvis:

```powershell
.\scripts\run-inventory-scan.ps1 `
    -Query "age0-1825" `
    -Limit 200 `
    -OutputDirectory "C:\Data\PokemonGo\runs\scan-001" `
    -Resume
```

Der skal mindst findes kommandoer til:

* start scan,
* resume scan,
* scan status,
* validate database,
* analyze existing evidence,
* generate reports,
* apply approved tags,
* inspect blockers.

Brugeren skal ikke kende interne C#-projekter eller lange `dotnet run`-kommandoer.

---

# 27. Performancekrav

## PERF-001 – Langvarig drift

Systemet skal kunne køre unattended i mindst otte timer.

## PERF-002 – Gennemløb

Målsætningen er:

* median højst 15 sekunder pr. almindelig Pokémon,
* 95-percentil højst 30 sekunder uden recovery,
* ingen unødvendig Details/Appraise-navigation mellem Pokémon.

## PERF-003 – Hukommelse

Systemet må ikke holde hele inventory-evidensen i hukommelsen.

Screenshots og databaseposter skal skrives løbende.

## PERF-004 – Diskplads

Evidence-retention skal være konfigurerbar:

* alle frames,
* kun accepterede frames,
* kun fejl og repræsentative frames,
* komprimeret arkiv efter run.

---

# 28. Sikkerhedskrav

Følgende tællere skal altid kunne rapporteres:

* Raw ADB inputs.
* Blind retries.
* Blind second swipes.
* Wrong-state actions.
* Affirmative confirmation inputs.
* Transfer actions.
* Delete actions.
* Power Up actions.
* Evolve actions.
* Purify actions.
* Purchase actions.
* Favorite changes.
* Tag mutations.
* Calcy actions.

For en read-only scanning skal alle ændrende tællere være nul.

## SAFE-001 – Confirmation-dialoger

På en destruktiv confirmation-dialog må kun en visuelt identificeret cancel/close-handling autoriseres.

## SAFE-002 – Ingen skjulte defaults

Programmet må ikke automatisk tolke en timeout som succes.

## SAFE-003 – Audit

Alle input skal registreres med:

* operation,
* state før,
* screenshot-hash,
* target,
* input,
* state efter,
* resultat.

---

# 29. Testkrav

## 29.1 Enhedstests

Skal dække:

* state detection,
* locators,
* canonical close,
* appraisal analysis,
* species/CP extraction,
* fingerprints,
* query classification,
* policy-regler,
* dubletgrupper,
* databaseoperationer,
* checkpoint/resume.

## 29.2 Syntetiske vision-tests

CI må ikke afhænge af private lokale telefonfiler.

Syntetiske fixtures skal bruges til deterministiske sikkerhedstests.

## 29.3 Evidence-regression

Godkendte real-phone-evidencepakker må anvendes lokalt til regression, men må ikke være nødvendige for almindelig CI.

## 29.4 Integrationstests

Skal bevise:

* persistens før swipe,
* database reopen,
* stop og resume,
* duplicate prevention,
* wraparound detection,
* recovery-budget,
* ingen destruktiv executor i read-only flow.

## 29.5 Real-phone acceptance

Følgende acceptance-trin anbefales:

### A – Navigation

* Inventory → Map.
* Details → Inventory → Map.
* Appraisal → Details → Inventory → Map.

### B – Carousel

* 20 Pokémon.
* Én Appraisal-open.
* 19 swipes.
* Én Appraisal-exit.
* Ingen Details-swipes.

### C – Semantik

* 50 Pokémon.
* Mindst 95 % art.
* Mindst 95 % CP.
* Mindst 95 % komplet IV.

### D – Resume

* 200 Pokémon.
* Kontrolleret programstop efter mindst 50.
* Resume uden dubletter.
* Korrekt fortsat ordinal.

### E – Lang scanning

* Minimum otte timers unattended drift.
* Recovery fra mindst én kendt afvigelse.

### F – Fuld read-only inventory

* Hele det valgte inventory-scope.
* Dokumenteret coverage.
* Ingen wraparound.
* Ingen dubletter.
* Databaseintegritet `ok`.

### H – Re-identifikation (kritisk, skal ligge tidligt)

* Samme scope (mindst 200 Pokémon) scannes to gange med programgenstart imellem.
* Mindst 99 % re-match til samme record (ID-006).
* Nul falske merges.
* Re-match-rate dokumenteret i `scan-coverage.json`.
* Dette trin skal være grønt, før trin F (fuld inventory), G (tagging) og hele oprydningsworkflowet påbegyndes.

### G – Tagging

* Separat testkonto eller begrænset scope.
* Kun reversible tags.
* Manifestbaseret.
* Genfinding og visuel verifikation pr. eksemplar (TAG-001/TAG-002).
* Tag-reconciliation mod manifest (TAG-003).
* Ingen transfer.

---

# 30. Accept af fuld inventory-scanning

Systemet må først erklæres klar til hele inventaret, når følgende er opfyldt:

1. Appraisal-carousel er stabil.
2. Art, CP og IV har tilstrækkelig coverage.
3. Resume er bevist med kontrolleret afbrydelse.
4. Dubletter på tværs af chunks forhindres.
5. Wraparound opdages.
6. Databaseintegritet er dokumenteret.
7. Analyse bruger genåbnede SQLite-data.
8. Ingen destruktive handlinger er tilgængelige i scan-flowet.
9. Telefonen kan selv normaliseres fra kendte states.
10. Et 200-item acceptance-run er grønt.

---

# 31. Fejlhåndtering

Fejl skal klassificeres som:

## Recoverable

* kortvarig Unknown,
* ustabil animation,
* ADB-reconnect,
* Appraisal lukket,
* Details åbent,
* Inventory åbent,
* kendt popup,
* proces genstartet.

## Safe stop

* ukendt skærm uden verificeret handling,
* modstridende evidence,
* recovery-loop,
* checkpoint-korruption,
* databasefejl,
* manglende postcondition,
* mandatory update,
* login/account-skærm.

## Fatal

* SQLite-integritetsfejl,
* forkert device,
* uautoriseret input,
* destruktiv handling,
* manglende audit,
* irreversible datauoverensstemmelser.

Ved stop skal alle allerede gemte observationer bevares og rapporter genereres, hvor det er muligt.

---

# 32. Logging og observability

Systemet skal have strukturerede logs med:

* timestamp,
* run ID,
* ordinal,
* state,
* operation,
* resultat,
* elapsed time,
* screenshot hash,
* database transaction,
* recovery incident.

Der skal kunne genereres en kompakt status som:

```text
Run: scan-2026-07-21
State: AppraisalBars
Captured: 438 / 1000
Current ordinal: 439
Last checkpoint: 8 seconds ago
Recovery incidents: 1
SQLite integrity: last checked ok
Destructive actions: 0
```

---

# 33. Konfiguration

Konfiguration skal kunne styre:

* ADB-path.
* Device serial.
* Automation timing.
* Screenshot settle time.
* Frame-count.
* Confidence thresholds.
* Recovery budgets.
* Scan queries.
* Chunk size.
* Evidence retention.
* Policy-version.
* Minimum copies pr. gruppe.
* Old Pokémon cutoff.
* Trade-tags.
* Trade-nickname-fragmenter.
* PvP-thresholds.
* Rapportoutput.

Konfiguration skal versioneres sammen med resultatet.

---

# 34. Compliance og driftsrisiko

Automatiseret styring af Pokémon GO kan være i konflikt med spillets brugsbetingelser og kan indebære kontorisiko.

Derfor skal systemet:

* undgå unaturligt høje inputrater,
* bruge bounded timing,
* undgå samtidige input,
* være transparent omkring alle handlinger,
* kunne køre i read-only mode,
* og kræve eksplicit aktivering af enhver ændrende funktion.

Dette ændrer ikke de tekniske krav, men skal indgå i brugerens driftsbeslutning.

---

# 35. Leverancer

Projektleverancen skal mindst bestå af:

1. Kildekode i GitHub.
2. Build- og testscript.
3. CLI.
4. PowerShell-launcher.
5. SQLite-schema og migrationer.
6. Vision- og OCR-komponenter.
7. Appraisal-carousel.
8. Recovery- og resume-motor.
9. Regelmotor.
10. Rapportgenerator.
11. Dokumenteret konfiguration.
12. Arkitekturbeskrivelse.
13. Sikkerhedsmodel.
14. Acceptance-rapporter.
15. Eksempel på database og rapporter uden private credentials.
16. Driftsvejledning.
17. Fejlsøgningsvejledning.
18. Handover-dokument.
19. Versioneret referencedata-dump (artsliste, klassifikationer, CP-grænser, jf. afsnit 21A).

---

# 36. Faseplan

## Fase 1 – Fundament

* Repository og projekter.
* Device transport.
* Vision.
* State detection.
* Navngivne operationer.
* CI.

## Fase 2 – Sikker navigation

* Inventory.
* Details.
* Appraisal.
* Canonical close.
* Destructive confirmation interlock.

## Fase 3 – Read-only scanning

* Appraisal-carousel.
* Persistens før swipe.
* 20-item acceptance.
* Evidence.

## Fase 4 – Semantisk datakvalitet

* Art.
* CP.
* IV.
* Variantdata.
* Query-semantik.
* 50-item acceptance.

## Fase 4A – Identitets-spike (skal ligge før videre udbygning)

* Semantisk identitetsnøgle (ID-005).
* Dobbeltscannings-test af samme scope (ID-006 / acceptance-trin H).
* Målt re-match-rate ≥ 99 %, nul falske merges.
* Ved rødt resultat: arkitekturbeslutning om identitetsstrategi, før Fase 5+ fortsættes.

Begrundelse: hele oprydningsproduktet (tagging, manifest, DeletedConfirmed) hviler på cross-run-identitet. Den antagelse skal bevises billigt nu — ikke opdages dyrt i Fase 8.

## Fase 5 – Resume og skalering

* Checkpoint.
* Resume.
* Chunking.
* Duplicate prevention.
* Wraparound.
* 200-item acceptance.

## Fase 6 – Komplet inventar

* Scan-plan.
* Coverage.
* Cross-run reconciliation.
* Lang unattended kørsel.

## Fase 7 – Analyse

* Policy.
* Dubletgrupper.
* PvP.
* Trade.
* Old/distance.
* Rapporter.

## Fase 8 – Reversibel oprydning

* Tags.
* Cleanup-manifest.
* Efterkontrol.

## Fase 9 – Eventuel transferautomation

Kun efter særskilt beslutning, kravspecifikation og sikkerhedsaccept.

---

# 37. Endelig definition af “færdig”

Systemet er færdigt som sikkert inventory- og analyseprodukt, når:

* hele inventaret kan scannes unattended,
* scanning kan stoppes og genoptages,
* art, CP og IV er pålideligt registreret,
* database og evidence er konsistente,
* dubletgrupper er korrekte,
* beskyttelsesregler håndhæves,
* konkrete oprydningsforslag kan forklares,
* kandidater kan markeres reversibelt,
* og ingen Pokémon kan ændres eller slettes uden en separat, eksplicit autoriseret proces.

Det centrale slutprodukt er ikke blot en telefonrobot. Det er et **auditérbart inventory-system**, hvor telefonautomation, database, evidence, policy og oprydningsbeslutninger hænger sammen fra første screenshot til den endelige anbefaling.
