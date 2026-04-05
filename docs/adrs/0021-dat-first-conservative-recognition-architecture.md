# ADR-0021: DAT-First Conservative Recognition Architecture

## Status
Proposed

## Datum
2026-04-05

## Entscheidungstreiber
- Zu viele False Positives bei heuristischer Erkennung ohne DAT-Absicherung
- PlatformFamily wird angereichert, aber nicht fuer Routing, Konflikterkennung oder DAT-Strategien genutzt
- Sort / Review / Blocked / Unknown Grenzen sind uniform statt familienbasiert
- Intra-Family-Konflikte (PS1 vs PS2, NES vs SNES) werden nicht priorisiert behandelt
- Cross-Family-Konflikte fuehren nur zu Review statt Blocked

---

## 1. Ist-Zustand

### Pipeline-Fluss

```
File → Classification (Game/Bios/Junk)
     → DAT-Lookup #1 (cross-console, hash-basiert)
     → [Miss] → ConsoleDetector.DetectWithConfidence (8 Methoden parallel)
              → HypothesisResolver (gewichtete Aggregation + Konflikt-Check)
              → DecisionResolver (Tier → Sort/Review/Blocked/Unknown)
     → DAT-Lookup #2 (mit detektiertem ConsoleKey, narrowed)
     → BIOS-Resolution
     → RomCandidate mit Decision + Evidence
     → ConsoleSorter: Sort→ConsoleKey/, Review→_REVIEW/, Blocked→stays, Junk→_TRASH_JUNK/
```

### Evidence-Tier-Hierarchie

| Tier | Quellen | Vertrauen |
|------|---------|-----------|
| Tier0 | ExactDatHash | Absolut (100) |
| Tier1 | DiscHeader, CartridgeHeader, SerialNumber | Strukturell (88-92) |
| Tier2 | UniqueExtension, ArchiveContent | Starke Heuristik (80-95) |
| Tier3 | FolderName, FilenameKeyword, AmbiguousExtension | Schwache Heuristik (40-85) |
| Tier4 | Kein Signal | Unknown |

### HypothesisResolver-Logik (aktuell)

- Gruppiert Hypothesen nach ConsoleKey, summiert Confidence
- Hoechste Gesamtconfidence gewinnt (alphabetischer Tiebreak)
- Source-Priority: DatHash(5) > Structural(4) > UniqueExt(3) > Serial/Archive(2) > Folder/Keyword(1) > AmbigExt(0)
- AMBIGUOUS: Wenn Top-2 beide ≥60, gleiche Source-Priority, Ratio ≥0.7 → Blocked
- Single-Source-Cap + Soft-Only-Cap begrenzen maximale Confidence
- Conflict-Penalty: -5 bis -20 je nach Runner-up-Staerke

### PlatformFamily (vorhanden, ungenutzt)

6 Families in `consoles.json` und `PlatformFamily`-Enum:
- **NoIntroCartridge** (~30 Konsolen): NES, SNES, GB, GBA, MD, N64, ...
- **RedumpDisc** (~20 Konsolen): PS1, PS2, Saturn, DC, GC, Wii, ...
- **Arcade** (~5 Systeme): ARCADE, NEOGEO, AWAVE, CHI
- **ComputerTOSEC** (~8 Systeme): A800, AMIGA, C64, DOS, MSX, ...
- **FolderBased** (1 System): PS3
- **Hybrid** (~5 Systeme): 3DS, NDS, PSP, SWITCH, VITA, WIIU

Family wird in `RomCandidate.PlatformFamily` gespeichert und in Dashboard/CLI angezeigt, aber:
- **Nicht** fuer DAT-Strategie-Auswahl verwendet
- **Nicht** fuer Konflikteskalation verwendet
- **Nicht** fuer Decision-Thresholds verwendet
- **Nicht** fuer Sorting-Routing verwendet

---

## 2. Hauptursachen fuer Misses und False Positives

### False Positives (Prio 1)

| # | Problem | Beispiel | Ursache |
|---|---------|----------|---------|
| FP1 | **Heuristik-Sort ohne DAT-Absicherung** | Tier1+Conf≥85 reicht fuer Sort, auch wenn DAT-Index geladen aber kein Hash-Match | DecisionResolver gibt Sort bei Structural+HighConf, DAT-Miss wird ignoriert |
| FP2 | **Cross-Family-Match nicht eskaliert** | Detector sagt PS1, Cross-Console-DAT findet Vita-Hash → Review statt Blocked | `TryCrossConsoleDatLookup` setzt nur `detectionConflict=true`, keine Family-Pruefung |
| FP3 | **Intra-Family-Konflikte unterschaetzt** | PS1 vs PS2 (gleich RedumpDisc), NES vs Famicom → Review statt gezielte Eskalation | AMBIGUOUS prueft nur Top-2 gleiche Priority, nicht Family-Zugehoerigkeit |
| FP4 | **Folder-Hint allein kann Sort ermoeglichen** | Folder=PS2 + UniqueExt(.iso)=Ambig → Summe ≥85 → Sort | Multi-Source-Bonus hebt Soft-Evidence ueber Sort-Threshold |
| FP5 | **DAT-Name-Match bei Disc-Images** | CHD ohne Hash → Name-Match mit Confidence 85 → DatVerified-nah | Name-Match ist fragiler als Hash, wird aber aehnlich behandelt |

### Misses / UNKNOWN (Prio 2)

| # | Problem | Beispiel | Ursache |
|---|---------|----------|---------|
| M1 | **Kein DAT geladen fuer Konsole** | ARCADE-ROMs ohne FBNeo-DAT → kein Tier0 moeglich | BridgeDatSourceAliases deckt nicht alle Faelle ab |
| M2 | **Headerless-Hash nicht fuer alle Cart-Familien** | Einige Cartridge-Konsolen nutzen container-sha1 statt headerless | hashStrategy in consoles.json nicht durchgehend familienbasiert |
| M3 | **Archive-Content-Analyse begrenzt** | 7z-Archive mit verschachtelten Ordnern → nur Top-Level-Extensions geprueft | ArchiveContent-Methode scannt nur direkte Eintraege |
| M4 | **ComputerTOSEC hat keine Header-Signatur** | Amiga/C64/DOS-Disks haben keine standardisierten Magic Bytes | Kein Tier1-Weg fuer diese Familie, nur Tier2/3 |
| M5 | **FolderBased (PS3) nicht speziell behandelt** | PS3-Ordner mit PARAM.SFO nicht erkannt wenn nicht als Ordner gescannt | Scan-Modus ist Datei-basiert, nicht Ordner-basiert |

### Schwaechen im Conflict-Handling (Prio 2)

| # | Problem | Impact |
|---|---------|--------|
| C1 | **AMBIGUOUS nur bei 2 starken Top-Hypothesen** | 3+ plausible Konsolen (PS1/PS2/PSP) → nur Top-2 geprueft |
| C2 | **Kein Within-Family vs Cross-Family Unterschied** | PS1↔PS2 Konflikt (intra-family, lösbar per Header) gleich behandelt wie PS1↔VITA (cross-family, echtes Problem) |
| C3 | **Conflict-Penalty nicht Family-aware** | Runner-up aus gleicher Family wird gleich bestraft wie aus fremder Family |

---

## 3. Zielarchitektur: Conservative DAT-First mit Family Pipelines

### Kernprinzipien

1. **DAT-Match ist koenig**: Nur Tier0 (ExactDatHash) darf ohne Einschraenkung Sort produzieren
2. **Heuristik allein maximal Review**: Ohne DAT-Absicherung ist Sort nicht erlaubt, egal wie hoch die Confidence
3. **Family bestimmt Strategie**: DAT-Hash-Typ, Conflict-Eskalation und Decision-Gates familienspezifisch
4. **Intra-Family-Konflikte eskalieren gezielt**: PS1↔PS2 braucht DiscHeader, nicht nur Folder-Hint
5. **Cross-Family-Konflikte sind Blocked**: Unterschiedliche Families → kein automatisches Sort

### Decision-Matrix (Ziel)

| Tier | Family-Match | Conflict | → Decision |
|------|-------------|----------|-----------|
| Tier0 (DAT-Hash) | Egal | Nein | **DatVerified** (Sort) |
| Tier0 (DAT-Hash) | Egal | Ja (intra-family) | **Review** |
| Tier0 (DAT-Hash) | Cross-Family-Conflict | Ja | **Blocked** |
| Tier1 (Structural) | DAT geladen, kein Match | Nein | **Review** (nicht Sort!) |
| Tier1 (Structural) | DAT nicht geladen | Nein, Conf≥90 | **Sort** (Ausnahme: kein DAT verfuegbar) |
| Tier1 (Structural) | DAT nicht geladen | Ja | **Review** |
| Tier2 (StrongHeuristic) | Egal | Egal | **Review** |
| Tier3 (WeakHeuristic) | Egal | Egal | **Blocked** |
| Tier4 (Unknown) | Egal | Egal | **Unknown** |

**Zentrale Aenderung**: Tier1 + DAT geladen + kein DAT-Match → Review statt Sort.
Begruendung: Wenn ein DAT existiert und der Hash nicht drin ist, ist das ein negatives Signal.

### Family-Pipeline-Architektur

```
                            ┌─────────────────────────┐
                            │   EnrichmentPipeline     │
                            │  (wie bisher: DAT→Det)   │
                            └──────────┬──────────────┘
                                       │
                                       ▼
                            ┌─────────────────────────┐
                            │  FamilyPipelineSelector  │  ← NEU
                            │  (PlatformFamily→Strategy)│
                            └──────────┬──────────────┘
                                       │
            ┌──────────────────────────┼──────────────────────────┐
            ▼                          ▼                          ▼
  ┌─────────────────┐       ┌──────────────────┐       ┌─────────────────┐
  │ CartridgePipeline│       │  DiscPipeline    │       │  ArcadePipeline │
  │ (NoIntroCartridge)│       │ (RedumpDisc)     │       │  (Arcade)       │
  └─────────────────┘       └──────────────────┘       └─────────────────┘
  • headerless hash           • track-sha1              • set-archive check
  • per-header detection      • disc-header authority    • parent/clone DAT
  • unique-ext tiebreaker     • PS1/PS2 escalation       • MAME version gate
  • name-match disabled       • name-match erlaubt       • name-match erlaubt

            ┌──────────────────────────┼──────────────────────────┐
            ▼                          ▼                          ▼
  ┌─────────────────┐       ┌──────────────────┐       ┌─────────────────┐
  │ ComputerPipeline │       │ HybridPipeline   │       │ FolderPipeline  │
  │ (ComputerTOSEC)  │       │ (Hybrid)         │       │ (FolderBased)   │
  └─────────────────┘       └──────────────────┘       └─────────────────┘
  • TOSEC-naming validate    • format-dependent hash    • dir-level scan
  • strict naming → Sort     • container vs track       • PARAM.SFO detect
  • no-match → Review only   • per-console policy       • always DAT-gate
```

### Family-spezifische Conflict-Resolution

```csharp
// Pseudo-Logik im erweiterten HypothesisResolver
if (winnerFamily == runnerUpFamily)
{
    // Intra-Family: Eskaliere gezielt
    // PS1↔PS2 → erfordere DiscHeader oder DAT
    // NES↔Famicom → erfordere UniqueExt oder CartridgeHeader
    if (!HasStructuralEvidence(winner)) → Review (nicht Sort)
}
else
{
    // Cross-Family: Immer Blocked
    → Blocked (Familie widerspricht sich fundamental)
}
```

### Sorting-Routing (Ziel)

| Decision | Category | Ziel-Ordner |
|----------|----------|------------|
| DatVerified | Game | `{ConsoleKey}/` |
| DatVerified | BIOS | `{ConsoleKey}/_BIOS/` |
| Sort | Game | `{ConsoleKey}/` |
| Review | Any | `_REVIEW/{ConsoleKey}/` |
| Review (intra-family) | Any | `_REVIEW/{ConsoleKey}/` + Reason-Tag |
| Blocked (cross-family) | Not Junk | `_BLOCKED/{reason}/` |
| Blocked | Junk | `_TRASH_JUNK/{ConsoleKey}/` |
| Unknown | Not Junk | `_UNKNOWN/` |
| Unknown | Junk | `_TRASH_JUNK/` |

---

## 4. Phasenweiser Umsetzungsplan

### Phase 1: Conservative DAT Gate (Prio: Release-Blocker FP1)

**Ziel**: Heuristik allein darf nicht mehr Sort produzieren, wenn DAT vorhanden.

**Aenderungen**:
1. `DecisionResolver` erweitern um `datAvailable: bool` Parameter
2. Wenn `datAvailable && tier >= Tier1 && !datMatch` → maximal Review
3. Nur wenn `!datAvailable` (kein DAT geladen) darf Tier1+≥85 weiterhin Sort geben

**Aufwand**: Klein (1 Datei Core, 1 Datei Infra-Wiring, Tests anpassen)
**Risiko**: Niedrig — verschiebt Grenzfaelle von Sort nach Review

### Phase 2: Family-aware Conflict Escalation (Prio: FP2, FP3, C1-C3)

**Ziel**: Intra-Family vs Cross-Family Konflikte unterscheiden.

**Aenderungen**:
1. `HypothesisResolver.Resolve` erhaelt `Func<string, PlatformFamily>` Lookup
2. Cross-Family-Konflikte → immer Blocked
3. Intra-Family-Konflikte → erfordere Structural-Evidence fuer Sort, sonst Review
4. AMBIGUOUS-Check erweitern: alle Hypothesen-Familien pruefen, nicht nur Top-2

**Aufwand**: Mittel (Core: HypothesisResolver, DecisionResolver; Tests erweitern)
**Risiko**: Mittel — koennte Review-Rate temporaer erhoehen

### Phase 3: Family-stratified DAT Strategy (Prio: M1, M2)

**Ziel**: Hash-Strategie pro Family statt pro Konsole.

**Aenderungen**:
1. Neues Interface `IFamilyDatStrategy` in Contracts
2. Implementierungen: `CartridgeDatStrategy`, `DiscDatStrategy`, `ArcadeDatStrategy`, `ComputerDatStrategy`, `HybridDatStrategy`, `FolderDatStrategy`
3. `EnrichmentPipelinePhase.LookupDat` delegiert Hash-Berechnung an Family-Strategy
4. Family-Strategy bestimmt: Hash-Typ, Name-Match erlaubt?, Headerless?, Archive-Inner?

**Aufwand**: Mittel-Gross (Contracts: Interface, Infra: 6 Implementierungen, Tests)
**Risiko**: Mittel — muss rueckwaertskompatibel zu bestehenden consoles.json hashStrategy-Werten sein

### Phase 4: FamilyPipelineSelector (Prio: Architektur-Sauberkeit)

**Ziel**: Post-Enrichment Validation und Family-spezifische Nachbehandlung.

**Aenderungen**:
1. Neuer `FamilyPipelineSelector` Service in Infrastructure
2. Wird nach EnrichmentPipeline aufgerufen
3. Validiert: Passt PlatformFamily des Detectors zum DAT-Match?
4. Eskaliert: Cross-Family-DAT-Match → Blocked
5. Optimiert: Disc-Family + DiscHeader → hoehere Confidence

**Aufwand**: Mittel (Infra: neuer Service, Orchestrator-Wiring)
**Risiko**: Niedrig — reiner Post-Validation-Step, aendert keine bestehende Logik

### Phase 5: Enhanced Sorting Routing (Prio: Wartbarkeit)

**Ziel**: Sort/Review/Blocked/Unknown mit Reason-Tags und Family-Ordnern.

**Aenderungen**:
1. `SortDecision` erweitern um optionalen `Reason` string
2. ConsoleSorter: Blocked → `_BLOCKED/{Reason}/` statt kein Move
3. Unknown → `_UNKNOWN/` statt kein Move
4. Review + intra-family → Reason-Tag im Audit-Log

**Aufwand**: Klein-Mittel (Contracts: Model-Erweiterung, Infra: ConsoleSorter, Tests)
**Risiko**: Niedrig — additiv, keine Verhaltensaenderung fuer bestehende Flows

### Phase 6: Benchmark-Gates und Regression-Tests

**Ziel**: Qualitaetssicherung durch messbare Schwellwerte.

**Aenderungen**:
1. Benchmark-Testset um Family-stratifizierte Szenarien erweitern
2. Gates pro Family: Min-DatVerified%, Max-FalsePositive%, Max-Unknown%
3. Regressionstests fuer alle bekannten Grenzfaelle (PS1↔PS2, NES↔Famicom, etc.)
4. CI-Gate: Kein Merge bei Verschlechterung der Family-spezifischen Metriken

**Aufwand**: Mittel (Tests, Benchmark-Infra)
**Risiko**: Niedrig — reines Qualitaets-Netz

---

## 5. Betroffene Dateien

### Phase 1 (Conservative DAT Gate)

| Datei | Aenderung |
|-------|-----------|
| `Core/Classification/DecisionResolver.cs` | + `datAvailable` Parameter, DAT-Gate-Logik |
| `Infrastructure/Orchestration/EnrichmentPipelinePhase.cs` | `datAvailable` an DecisionResolver durchreichen |
| `Tests/DetectionPipelineTests.cs` | Neue Tests: DAT-vorhanden → Tier1 = Review |
| `Tests/RunOrchestratorTests.cs` | Regressionstests fuer Sort-Rate-Aenderung |

### Phase 2 (Family-aware Conflict Escalation)

| Datei | Aenderung |
|-------|-----------|
| `Core/Classification/HypothesisResolver.cs` | Family-Lookup, Cross-Family → Blocked |
| `Core/Classification/DecisionResolver.cs` | Intra-Family-Gates |
| `Core/Classification/ConsoleDetector.cs` | `GetPlatformFamily()` bereits vorhanden, public machen |
| `Contracts/Models/RecognitionResult.cs` | + ConflictType (IntraFamily/CrossFamily/None) |
| `Tests/DetectionPipelineTests.cs` | Cross/Intra-Family-Testfaelle |

### Phase 3 (Family DAT Strategy)

| Datei | Aenderung |
|-------|-----------|
| `Contracts/Interfaces/IFamilyDatStrategy.cs` | NEU: Interface |
| `Infrastructure/Dat/CartridgeDatStrategy.cs` | NEU |
| `Infrastructure/Dat/DiscDatStrategy.cs` | NEU |
| `Infrastructure/Dat/ArcadeDatStrategy.cs` | NEU |
| `Infrastructure/Dat/ComputerDatStrategy.cs` | NEU |
| `Infrastructure/Dat/HybridDatStrategy.cs` | NEU |
| `Infrastructure/Dat/FolderDatStrategy.cs` | NEU |
| `Infrastructure/Orchestration/EnrichmentPipelinePhase.cs` | Delegate an Family-Strategy |
| `Infrastructure/Orchestration/RunEnvironmentBuilder.cs` | Strategy-Instanziierung |
| `Tests/DatStrategyTests.cs` | NEU: Tests pro Strategy |

### Phase 4 (FamilyPipelineSelector)

| Datei | Aenderung |
|-------|-----------|
| `Infrastructure/Orchestration/FamilyPipelineSelector.cs` | NEU: Post-Validation |
| `Infrastructure/Orchestration/EnrichmentPipelinePhase.cs` | Aufruf FamilyPipelineSelector |
| `Tests/FamilyPipelineSelectorTests.cs` | NEU |

### Phase 5 (Enhanced Sorting)

| Datei | Aenderung |
|-------|-----------|
| `Contracts/Models/SortDecision.cs` | + Reason-Feld |
| `Infrastructure/Sorting/ConsoleSorter.cs` | Reason-basiertes Routing |
| `Tests/ConsoleSorterTests.cs` | Neue Routing-Tests |

### Phase 6 (Benchmark)

| Datei | Aenderung |
|-------|-----------|
| `benchmark/gates.json` | Family-stratifizierte Schwellwerte |
| `Tests/Benchmark/` | Family-spezifische Benchmark-Tests |

---

## 6. Noetige Tests und Benchmarks

### Unit-Tests (Pflicht pro Phase)

**Phase 1 – DAT Gate:**
- `DecisionResolver_Tier1_DatAvailable_NoDatMatch_ReturnsReview`
- `DecisionResolver_Tier1_DatNotAvailable_HighConf_ReturnsSort`
- `DecisionResolver_Tier0_DatMatch_AlwaysDatVerified`
- `DecisionResolver_Tier1_DatAvailable_DatMatch_ReturnsDatVerified`

**Phase 2 – Family Conflict:**
- `HypothesisResolver_CrossFamilyConflict_ReturnsBlocked`
- `HypothesisResolver_IntraFamilyConflict_NoStructural_ReturnsReview`
- `HypothesisResolver_IntraFamilyConflict_WithStructural_ReturnsSortOrReview`
- `HypothesisResolver_ThreePlusFamilies_ReturnsBlocked`
- `HypothesisResolver_PS1vPS2_DiscHeader_ResolvesCorrectly`
- `HypothesisResolver_NESvFamicom_UniqueExt_ResolvesCorrectly`

**Phase 3 – Family DAT Strategy:**
- `CartridgeDatStrategy_UsesHeaderlessHash`
- `DiscDatStrategy_UsesTrackSha1`
- `ArcadeDatStrategy_ValidatesSetArchive`
- `ComputerDatStrategy_StrictNaming`
- `HybridDatStrategy_UsesContainerSha1`
- `FolderDatStrategy_ScansDirectory`

**Phase 4 – Post-Validation:**
- `FamilyPipelineSelector_CrossFamilyDatDetectorMismatch_Escalates`
- `FamilyPipelineSelector_DiscFamily_WithDiscHeader_BoostsConfidence`
- `FamilyPipelineSelector_ArcadeFamily_NoSetMatch_Blocks`

**Phase 5 – Sorting:**
- `ConsoleSorter_Blocked_NotJunk_MovesToBlockedFolder`
- `ConsoleSorter_Unknown_NotJunk_MovesToUnknownFolder`
- `ConsoleSorter_Review_HasReasonTag_InAudit`

### Regressionstests

- PS1 ↔ PS2 Disc-Image-Verwechslung (bekannter Grenzfall)
- NES ↔ Famicom Cartridge-Verwechslung
- ARCADE ↔ NEOGEO Set-Zuordnung
- Vita ↔ PSP Hybrid-Konflikt
- Amiga ↔ ST Computer-Verwechslung
- Cross-Console DAT-Hash-Treffer (gleicher Hash, verschiedene Konsolen)
- DAT vorhanden, Hash nicht drin → Sort-Blockade verifizieren

### Benchmark-Gates

| Family | Min DatVerified% | Max FalsePositive% | Max Unknown% |
|--------|------------------|--------------------|--------------|
| NoIntroCartridge | 85% | 2% | 5% |
| RedumpDisc | 80% | 3% | 8% |
| Arcade | 75% | 1% | 10% |
| ComputerTOSEC | 60% | 5% | 15% |
| Hybrid | 70% | 3% | 10% |
| FolderBased | 50% | 2% | 20% |

---

## 7. Risiken und Mitigationen

| Risiko | Impact | Mitigation |
|--------|--------|------------|
| Phase 1 erhoet Review-Rate signifikant | Nutzer sortieren mehr manuell | Graduelle Einfuehrung mit Opt-in-Flag `ConservativeDatGate` in Settings |
| Family-Strategie inkompatibel mit consoles.json hashStrategy | Bestehende Hash-Typen brechen | Family-Strategy als Override, consoles.json hashStrategy bleibt Fallback |
| Cross-Family Blocked-Rate zu hoch | Nutzer verlieren Vertrauen | Blocked-Ordner mit klarem Reason-Tag, GUI zeigt Loesungsvorschlag |
| Aufwand groesser als geplant | Release-Verzoegerung | Phasen 1+2 sind unabhaengig von 3-6, liefern bereits den groessten Nutzen |

---

## 8. Entscheidung

### Empfehlung: Phasen 1 + 2 sofort, Phasen 3-6 inkrementell

**Phase 1** (Conservative DAT Gate) ist der mit Abstand wichtigste Einzelschritt:
- Eliminiert die groesste False-Positive-Quelle (Heuristik-Sort trotz DAT-Miss)
- Kleine Aenderung (2 Dateien Core/Infra, Tests)
- Voellig rueckwaertskompatibel ueber Opt-in-Flag

**Phase 2** (Family Conflict Escalation) ist der zweitwichtigste:
- Eliminiert Cross-Family False Positives
- Verbessert Intra-Family-Handling (PS1↔PS2)
- Nutzt bereits vorhandene PlatformFamily-Infrastruktur

**Phasen 3-6** koennen inkrementell folgen und erfordern die Basis aus Phase 1+2.

## Konsequenzen

### Positiv
- Weniger False Positives bei heuristischer Erkennung
- Klare Trennung: DAT-abgesichert vs heuristisch
- Family-Awareness ermoeglicht gezielte Konflikterkennung
- Sort/Review/Blocked/Unknown Routing wird informativer
- Grundlage fuer spaetere Family-spezifische DAT-Strategien

### Negativ
- Review-Rate steigt initial (bewusste Entscheidung: lieber Review als False Positive)
- Komplexitaet im HypothesisResolver steigt leicht
- Neue Abstraktion (IFamilyDatStrategy) muss gewartet werden

### Neutral
- Bestehende Tests muessen angepasst werden (Sort→Review bei DAT-Gate)
- consoles.json-Schema bleibt unveraendert
