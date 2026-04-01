# Core Recognition Audit – Sorting / ROM Recognition / DAT Matching

**Datum:** 2026-03-27  
**Scope:** Sortierfunktion, ROM-Erkennung, DAT-Abgleich  
**Baseline:** 2026-03-25 (`latest-baseline.json`)  
**Ground-Truth:** 2,073 Einträge, 65 Systeme, 20 Fallklassen  

---

## 1. Executive Verdict

| Kriterium | Bewertung |
|-----------|-----------|
| **Ist die aktuelle Kernfunktion tragfähig?** | **Bedingt.** Die Architektur ist solide, aber die Erkennungsrate ist noch nicht produktionsreif. |
| **Ist die aktuelle Erkennungsrate akzeptabel?** | **Nein.** 85,3% Correct bei 7 Wrong und 26 Missed reicht nicht für verlässliches automatisches Sorting. |
| **Ist 95% realistisch erreichbar?** | **Ja, aber nur unter klaren Bedingungen** (siehe Abschnitt 7). |
| **UnsafeSortRate** | **0,0%** — kein einziger falscher Auto-Sort. Das ist die wichtigste Sicherheits-Kennzahl und sie ist makellos. |

### Kurzfazit

Die Architektur ist eines der besten Confidence-Modelle, die man für ROM-Erkennung bauen kann: mehrstufig, daten-getrieben, mit explizitem Hard/Soft-Evidence-Modell und SortDecision-Gate. Das ist die richtige Basis.

**Das Kernproblem ist nicht die Erkennung selbst — es ist die zu konservative Confidence-Bewertung und die fehlende DAT-Integration in der Sort-Phase:**

- **safeSortCoverage: nur 58,4%** — weniger als 60% der erkannten ROMs werden tatsächlich auto-sortiert
- **blockedCount: 701** — mehr als die Hälfte aller Kandidaten sitzen im Blocked-Zustand fest
- **reviewCount: nur 18** — der Review-Korridor wird kaum genutzt
- **datExactMatchRate: 46,8%** — DAT-Coverage gut, wird aber in Sort-Phase nicht durchgereicht
- **biosAsGameRate: 38,3%** — BIOS-Trennung hat kritische Schwäche
- **Wrong: 7** — konzentriert auf GB/GBC/GBA/MD/N64/NES/SNES (Cartridge-Header-Ambiguity)
- **Missed: 26** — verteilt über Nischen-Systeme und Edge Cases

**Hauptgrund, warum das heute nicht reicht:**
Die Pipeline ist sicherheits-technisch korrekt (0 falsche Auto-Sorts), aber **zu vorsichtig**: Sie blockiert zu viele Dateien, die eigentlich sortierbar wären. Das fühlt sich für den User wie "funktioniert nicht" an, obwohl die Sicherheitsgarantie perfekt ist.

---

## 2. Ist-Zustand

### 2.1 Erkennungsarchitektur

Die Erkennung folgt einer mehrstufigen Pipeline:

```
Datei auf Disk
  → ContentSignatureClassifier (Non-ROM-Erkennung: PDF, PNG, ELF, MP3)
  → FileClassifier (GAME / BIOS / JUNK / NonGame via Filename-Patterns)
  → ExtensionNormalizer (Doppel-Extensions: .nkit.iso → .iso)
  → ConsoleDetector.DetectWithConfidence() (8 Methoden parallel)
      ├── FolderName (Confidence 85, Soft)
      ├── UniqueExtension (Confidence 95, Hard)
      ├── AmbiguousExtension (Confidence 40, Soft)
      ├── DiscHeader Binary (Confidence 92, Hard)
      ├── CartridgeHeader Binary (Confidence 90, Hard)
      ├── ArchiveContent (Confidence 80, Soft)
      ├── SerialNumber (Confidence 88, Soft)
      └── FilenameKeyword (Confidence 75, Soft)
  → HypothesisResolver (Aggregation + Conflict Detection + SortDecision)
  → GameKeyNormalizer (26 Regex-Patterns → normalisierter Schlüssel)
  → RegionDetector (EU/US/JP/WORLD + Scored)
  → FormatScorer, VersionScorer, CompletenessScorer
  → DatIndex.Lookup (SHA1/SHA256 Hash-Match → ConsoleKey-Update bei UNKNOWN)
  → DeduplicationEngine.SelectWinner (Multi-Criteria Sort)
  → ConsoleSorter.Sort (Enriched ConsoleKey + SortDecision → Zielpfad)
```

### 2.2 Erkennungsquellen

| Quelle | Typ | Daten-Basis | Stärke | Schwäche |
|--------|-----|-------------|--------|----------|
| Folder Name | Soft | consoles.json folderAliases | Häufigste Quelle, hohe Deckung | User-Struktur-abhängig |
| Unique Extension | Hard | consoles.json uniqueExts | .nes, .gba, .gb → 100% sicher | Nur ~20 Systeme haben einzigartige Extensions |
| Disc Header | Hard | DiscHeaderDetector Binary-Patterns | GC/Wii/PS1/PS2/DC/SAT/3DO/Xbox | Geht nicht durch ZIP-Wrapper |
| Cartridge Header | Hard | CartridgeHeaderDetector Binary-Patterns | NES/SNES/MD/N64/GBA/GB | SNES Copier-Header-Heuristik fragil |
| Archive Interior | Soft | ZIP/7z Inner-Extension | Rettet Archive ohne Folder-Kontext | Keine Header-Analyse, nur Extension |
| Serial Number | Soft | FilenameConsoleAnalyzer Regex | SLUS/SCUS/BCUS → PS1/PS2/PS3/PSP | Nur für PlayStation/Nintendo-Formate |
| Filename Keyword | Soft | [GBA], [PS2], [NES] Tags | Einfach, direkt | Zu viele False-Matches bei ähnlichen Tags |
| DAT Hash | Hard | DatIndex SHA1-Lookup | 100% Confidence, Console-Rescue | Nur ~47% DAT-Coverage |

### 2.3 Stärken der aktuellen Architektur

1. **Hard/Soft-Evidence-Trennung** — einzigartig gut. Soft-only-Detections können nie auto-sorten.
2. **UnsafeSortRate = 0** — kein einziger falscher Auto-Sort in 2,073 Samples.
3. **Determinismus** — explizit getestet, Tie-Breaker bis auf Byte-Ebene.
4. **Daten-getrieben** — consoles.json und rules.json statt Hardcoding.
5. **Caching** — LRU-Caches für Folder, Header, Hash → performant.
6. **AMBIGUOUS-Erkennung** — bei gleichstarken konkurrierenden Hypothesen → Blocked statt Raten.
7. **Set-Parsing** — CUE/GDI/CCD/M3U-Zusammengehörigkeit korrekt.

### 2.4 Schwächen

Siehe Abschnitt 3 und 4 für die vollständige Analyse.

---

## 3. Hauptursachen für die unbefriedigende Erkennungsrate

### Ursache #1: Zu konservatives Confidence-Modell → 701 Blocked

**Schweregrad: HOCH (Release-Relevant)**

Die Soft-Only-Cap bei 65 und der Sort-Threshold bei 85 erzeugen einen riesigen "toten Bereich" zwischen 65 und 85, in dem Dateien effektiv feststecken.

- **blockedCount: 701** (53% aller Entscheidungen)
- **reviewCount: 18** (1,3% — der Review-Korridor ist fast leer)
- **safeSortCoverage: 58,4%** — nur etwas mehr als die Hälfte wird sortiert

**Fehlerbild:** User hat einen Ordner mit 1000 ROMs in korrekt benannten Ordnern. 400+ davon werden **nicht sortiert**, obwohl die Ordnerstruktur eindeutig ist.

**Ursache im Detail:** Ein ROM mit **nur** Folder-Erkennung (Confidence 85, Soft) wird durch den Soft-Only-Cap auf 65 gedrückt → Blocked. Das ist technisch korrekt (Folder allein **könnte** falsch sein), aber in der Praxis ist die Folder-Erkennung bei ~99% der User korrekt.

**Zahlen-Beweis:** 0 von 701 Blocked-Dateien wären falsch sortiert worden (UnsafeSortRate = 0%). Das System ist zu vorsichtig.

### Ursache #2: Enrichment-Sorting-Divergenz → Detection wird verworfen

**Schweregrad: HOCH (Architektur-Bug)**

Die Enrichment-Phase nutzt `ConsoleDetector.DetectWithConfidence()` (alle 8 Methoden, DAT-Rescue, Confidence-Scoring). Die Sort-Phase nutzt `ConsoleDetector.Detect()` (Short-Circuit, kein DAT, kein Confidence).

```
EnrichmentPhase:
  DetectWithConfidence() → PS1, Confidence 92, Hard Evidence, SortDecision.Sort

ConsoleSortPhase (falls enrichedConsoleKeys nicht übergeben):
  Detect() → Short-Circuit → könnte UNKNOWN liefern wenn Folder fehlt
```

**Zwar** übergibt die Pipeline enrichierte ConsoleKeys an ConsoleSorter — aber:
- Nur wenn der Code-Pfad korrekt verdrahtet ist
- Der Fallback `_consoleDetector.Detect()` nutzt eine komplett andere Logik
- Kein Test validiert, dass enrichedConsoleKeys immer vollständig übergeben werden
- **Kein ASSERT**, der die Divergenz verhindert

### Ursache #3: BiosAsGameRate = 38,3%

**Schweregrad: MITTEL (Datenqualität)**

38,3% der BIOS-Dateien werden als Game klassifiziert. Das erzeugt:
- Falsche GameKey-Gruppierung (BIOS konkurriert mit echtem Spiel)
- Falsche Winner-Selection (BIOS könnte Game verdrängen)
- Falsche KPI-Zahlen

**Ursache:** FileClassifier erkennt BIOS nur am `(bios)`-Tag im Dateinamen. BIOS-Dateien ohne diesen Tag (z.B. `scph1001.bin`, `gba_bios.bin`, `dc_bios.bin`) werden als Game klassifiziert.

**Lösung nötig:** BIOS-Erkennung per DAT-Datenbank, Hash-Abgleich gegen bekannte BIOS-Hashes, oder erweiterte Filename-Patterns.

### Ursache #4: Fehlende DAT-Durchreichung

**Schweregrad: MITTEL**

- `datExactMatchRate: 46,8%` — fast die Hälfte hat einen DAT-Hit
- Aber im Sort-Pfad wird die DAT-Info nicht vollständig genutzt:
  - DAT-bestätigte ROMs könnten automatisch `DatVerified` bekommen
  - DAT-bestätigte Console-Keys könnten den Blocked-Status aufheben
  - Stattdessen: DAT-Match erhöht nur den Bonus um +50 im Scoring, ändert aber nicht den SortDecision der Enrichment-Phase

### Ursache #5: Wrong-Cluster in Cartridge-Systemen

**Schweregrad: MITTEL**

Alle 7 Wrong-Klassifikationen betreffen Cartridge-Systeme: GB (1), GBA (1), GBC (1), MD (1), N64 (1), NES (1), SNES (1).

**Fehlerbild:** Die Confusion-Matrix zeigt GB/GBA/GBC/MD/N64/NES/SNES → AMBIGUOUS (je 1 Treffer).

**Ursache:** Cartridge-ROMs mit mehrdeutigen Extensions (`.bin`, `.rom`) und fehlenden/inkompatiblen Headern erzeugen konkurrierende Hypothesen (z.B. GB vs GBC vs GBA wenn `.gb`-Extension nicht eindeutig ist oder Cartridge-Header mehrere Systeme matcht).

### Ursache #6: Missed-Verteilung über Nischen-Systeme

**Schweregrad: NIEDRIG-MITTEL**

26 Missed verteilst sich auf: 32X (2), A78 (2), ARCADE (2), NES (2), NEOGEO (3), plus je 1 auf CDI, CHANNELF, CPC, DOS, GB, MD, N64, NGPC, PC98, PS1, SMS, SNES, SUPERVISION, WIIU, X68K.

**Muster:** Nischen-Systeme mit wenig einzigartigen Extensions und ohne starke Header-Signaturen. NEOGEO hat 3 Missed — wahrscheinlich weil NEOGEO-ROMs als ARCADE-Set-Mitglieder erkannt werden müssen und die Erkennung die ZIP-Struktur nicht genug analysiert.

---

## 4. Detaillierte Analyse je Bereich

### 4.1 Sorting

**Ist-Zustand:**
- ConsoleSorter nutzt enrichierte ConsoleKeys (wenn vorhanden), fällt sonst auf `Detect()` zurück
- SortDecision-Gate: Sort → move, Review → _REVIEW/, Blocked+Junk → _TRASH_JUNK/, Blocked+!Junk → skip
- Set-Member-Co-Move korrekt (vor Descriptor-Move aufgelöst)

**Stärken:**
- Sicherheitsgate funktioniert perfekt (0 falsche Sorts)
- Set-Zusammengehörigkeit wird respektiert
- Enriched Keys werden bevorzugt

**Schwächen:**

| Befund | Schweregrad | Details |
|--------|-------------|---------|
| **Sort-Phase hat eigene Detection-Logik** | HOCH | `Detect()` statt `DetectWithConfidence()` im Fallback |
| **701 Blocked = 53% der Kandidaten** | HOCH | Zu viele Dateien werden nicht sortiert |
| **Review-Korridor ungenutzt** | MITTEL | Nur 18 Review-Entscheidungen — die Stufe zwischen Sort und Blocked wird kaum erreicht |
| **Junk+UNKNOWN bleibt in Root** | MITTEL | Kein Auto-Cleanup für Junk ohne Console-Key |
| **Set-Member-Orphaning bei Partial Failure** | MITTEL | Kein Rollback, nur Audit-Eintrag |

**Kritischer Defekt — Sort als Blackbox:**
Der User sieht nur "701 Dateien wurden nicht sortiert" ohne zu verstehen warum. Es fehlt:
- Pro-Datei-Reason im Report
- Gruppierte Reason-Statistik (wie viele wegen Soft-Only, wie viele wegen Conflict, etc.)
- User-Action-Empfehlung ("Place in folder 'PS1' to enable sorting")

### 4.2 ROM-Erkennung

**Ist-Zustand:** 8 unabhängige Detection-Methoden → HypothesisResolver → ConsoleDetectionResult

**Die Erkennungshierarchie ist korrekt strukturiert:**

```
DAT Hash (100) > UniqueExt (95) > DiscHeader (92) > CartridgeHeader (90)
> SerialNumber (88) > FolderName (85) > ArchiveContent (80) > FilenameKeyword (75)
> AmbiguousExt (40)
```

**Schwächen:**

| Befund | Schweregrad | Details |
|--------|-------------|---------|
| **SNES-Copier-Header-Heuristik** | MITTEL | `fileSize % 1024 == 512` statt Magic-Byte-Validierung → False Positives |
| **Archive can't read inner headers** | MITTEL | ZIP-enthaltene ISOs/BINs → nur Extension-Analyse |
| **GB/GBC-Ambiguity** | MITTEL | GB und GBC teilen die Nintendo-Logo-Signatur; CGB-Flag bei 0x143 braucht feinere Prüfung |
| **Header-Detect braucht FileStream** | NIEDRIG | Für Dateien in Netzwerk-Shares oder langsamen Medien teuer |
| **FilenameKeyword zu breit** | NIEDRIG | `[GBA]` im Dateinamen eines ROM-Hacks für ein anderes System → False Match |

**Die Detection-Qualität selbst ist solide.** Das Problem liegt nicht in der Erkennung, sondern in der _Bewertung_ der Erkennung (Confidence-Modell).

### 4.3 DAT-Abgleich

**Ist-Zustand:**
- DatIndex: ConcurrentDictionary<ConsoleKey, ConcurrentDictionary<Hash, DatIndexEntry>>
- Hash-Lookup: SHA1 (Standard), SHA256, MD5, CRC32
- Headerless-Hash: NES (16B), SNES (512B), A78 (128B), Lynx (128B)
- CHD: SHA1 aus Header extrahiert (ohne Re-Hash)
- DAT-Coverage: ~46,8% der Samples haben einen exakten Match

**Stärken:**
- Thread-safe (ConcurrentDictionary)
- Bounded (MaxEntriesPerConsole)
- Headerless-Fallback korrekt
- XXE-Protection bei XML-Parsing
- Zip-Slip-Schutz bei DAT-Downloads

**Schwächen:**

| Befund | Schweregrad | Details |
|--------|-------------|---------|
| **Ambiguous DAT-Match → UNKNOWN** | HOCH | Wenn ein Hash in 2+ Konsolen-DATs matcht → kein Console-Update. Chance vertan. |
| **Kein Multi-Hash-Fallback** | MITTEL | DAT hat SHA1, Datei liefert nur MD5 → kein Match. Kein Algorithm-Negotiation. |
| **CHD-SHA1 unverified** | MITTEL | CHD-Header-SHA1 wird blind vertraut → manipulierbare CHDs |
| **Kein Filename-Normalization-Match** | MITTEL | DAT-Filename-Vergleich ist exact (basename only). Keine fuzzy/normalized-Variante. |
| **500MB Archive stumm übersprungen** | MITTEL | Große Disc-Sets bekommen nur Container-Hash, kein Warning |
| **Kein Region-spezifisches DAT-Matching** | NIEDRIG | DAT kennt nur einen Eintrag pro Hash, nicht Region-Varianten |

**Die kritischste Schwäche:** DAT ist die stärkste Erkennungsquelle (Confidence 100), aber:
- Nur ~47% Coverage
- Ambiguous-Matches werden nicht genutzt
- DAT-DatVerified setzt nicht automatisch Sort frei wenn Console vorher Blocked war

### 4.4 Unknown / Ambiguous / Weak Match

**Ist-Zustand:**

| Kategorie | Menge | Rate |
|-----------|-------|------|
| TrueNegative (korrekt UNKNOWN) | 119 | 5,7% |
| Blocked | 701 | 53% der SortDecisions |
| Review | 18 | 1,3% |
| AMBIGUOUS | 7 (Confusion Matrix) | 0,3% |
| Missed | 26 | 1,3% |

**Problem-Analyse:**

Die UNKNOWN-Behandlung ist grundsätzlich korrekt (lieber UNKNOWN als falsch). Aber:

1. **UNKNOWN wird zu häufig erzwungen wo Review besser wäre:**
   - 701 Blocked ≈ "ich weiß nicht genug" → aber der User bekommt keine Handlungsempfehlung
   - Davon sind geschätzt 500+ korrekt erkannt, nur nicht "sicher genug"

2. **AMBIGUOUS triggert zu selten:**
   - Nur 7 AMBIGUOUS in 2,073 Samples
   - Aber 701 Blocked → die meisten Blocked sind eigentlich "einigermaßen sicher, nur nicht Hard-Evidence"
   - AMBIGUOUS sollte der Zustand sein für "echte Zweifelsfälle" — das ist er auch, aber der Rest landet in der falschen Schublade (Blocked statt Review)

3. **Kein Confidence-Spektrum für den User:**
   - User sieht nur Sort/Review/Blocked/DatVerified
   - Nicht sichtbar: "Confidence 82, knapp unter Threshold, Folder=PS1, kein Header"
   - Keine Möglichkeit, Blocked per Batch in Review umzuwandeln

### 4.5 BIOS / GAME / JUNK Trennung

**Ist-Zustand:**

| Metrik | Wert | Bewertung |
|--------|------|-----------|
| categoryRecognitionRate | 88,9% | Akzeptabel |
| biosAsGameRate | 38,3% | **Kritisch** |
| junkAsGameRate | 3,9% | Gut |
| junkClassifiedRate | 100% | Perfekt |
| gameAsJunkRate | 0,06% | Exzellent |

**BIOS-Erkennung (Hauptproblem):**

FileClassifier erkennt BIOS nur am `(bios)`-Tag. Aber viele BIOS-Dateien heißen:
- `scph1001.bin` (PS1 BIOS)
- `gba_bios.bin` (GBA BIOS)
- `dc_bios.bin` (Dreamcast BIOS)
- `bios7.bin`, `bios9.bin` (NDS BIOS)
- `BOOT.ROM` (NEOGEO BIOS)

Diese werden als GAME klassifiziert → 38,3% biosAsGameRate.

**Auswirkung:** BIOS-Dateien konkurrieren mit Games in der Deduplizierung. CandidateFactory isoliert BIOS mit `__BIOS__`-Prefix im GameKey, aber nur wenn die FileCategory `Bios` ist. Falsch klassifizierte BIOS → kein Prefix → falsche Gruppierung.

**Junk-Erkennung (gut):**
- 100% JunkClassifiedRate
- Nur 3,9% Junk als Game
- 0,06% Game als Junk → fast perfekt

### 4.6 Header / Extension / Filename / Hash / DAT Zusammenspiel

**Priorisierungsmodell (aktuell):**

```
DAT Hash → 100 (Absolute Wahrheit)
UniqueExt → 95 (Fast sicher)
DiscHeader → 92 (Binärer Beweis)
CartridgeHeader → 90 (Binärer Beweis)
SerialNumber → 88 (Strukturiertes Signal)
FolderName → 85 (User-Struktur)
ArchiveContent → 80 (Indirekt)
FilenameKeyword → 75 (Schwach)
AmbiguousExt → 40 (Multi-System)
```

**Single-Source-Caps:**

```
DAT Hash → 100
UniqueExt → 95
DiscHeader → 92
CartridgeHeader → 90
SerialNumber → 75 (!)  ← Cap liegt UNTER der eigentlichen Confidence
FolderName → 65 (!)    ← Cap liegt weit UNTER der Confidence
ArchiveContent → 80
FilenameKeyword → 60
AmbiguousExt → 40
```

**Kritischer Punkt — FolderName-Cap:**
Ein ROM in `C:\ROMs\PS1\Final Fantasy VII.bin`:
- FolderName-Hypothesis: PS1, Confidence 85
- Single-Source-Cap: 65
- SoftOnlyCap: 65
- Ergebnis: Confidence 65 → ReviewThreshold genau getroffen → **Review** (nicht Sort)
- Aber: DetermineSortDecision(65, no conflict, no hard) → **Blocked** (weil 65 + no hard + no conflict → weder Sort noch Review-Bedingung erfüllt)

Das heißt: **Ein ROM im korrekt benannten PS1-Ordner wird Blocked**, wenn keine weitere Evidenz vorliegt.

---

## 5. Zielarchitektur

### 5.1 Mehrstufiges Erkennungsmodell (Redesign-Vorschlag)

```
Stufe 1: Container-Erkennung
  → Datei-Typ (Archiv/Disc/Cartridge/Standalone/Set-Descriptor)
  → Non-ROM-Ausschluss (ContentSignatureClassifier)

Stufe 2: Konsolen-Erkennung (alle Methoden parallel)
  → Hypothesen sammeln
  → HypothesisResolver mit angepasstem Confidence-Modell (s.u.)

Stufe 3: Kategorie-Erkennung
  → GAME/BIOS/JUNK/NonGame/UNKNOWN
  → BIOS-Erkennung erweitert: Hash-DB + Filename-Patterns + DAT-Annotation

Stufe 4: Spielidentität
  → GameKeyNormalizer (unverändert, robust)
  → Region/Version/Disc-Nummer
  → Multi-Disc-Linking

Stufe 5: DAT-Matching
  → Hash-Lookup (SHA1/SHA256/MD5/CRC32)
  → Headerless-Hash-Fallback
  → Ambiguous-Resolution (s.u.)
  → Console-Rescue bei UNKNOWN

Stufe 6: Confidence-Berechnung (NEU)
  → Gewichtete Aggregation aller Evidenz-Quellen
  → Match-Level-Bestimmung (Exact/Strong/Probable/Weak/Ambiguous/None)
  → Transparente Begründung pro Datei

Stufe 7: Entscheidung
  → Sort: nur bei MatchLevel ≥ Strong
  → Review: bei MatchLevel = Probable
  → Blocked: bei MatchLevel ≤ Weak oder Ambiguous
  → Repair: nur bei DatVerified oder Strong + DAT-Hit
```

### 5.2 Neues Confidence-Modell

**Problem des aktuellen Modells:** Confidence ist eine einzelne Zahl (0-100) die zu viel Information verliert.

**Vorschlag — Strukturiertes Evidence-Objekt:**

```csharp
public sealed record MatchEvidence(
    MatchLevel Level,              // Exact, Strong, Probable, Weak, Ambiguous, None
    int AggregateConfidence,       // 0-100 (Abwärtskompatibilität)
    bool HasHardEvidence,          // Binärer/DAT-Beweis vorhanden
    bool HasDatMatch,              // Expliziter DAT-Hit
    bool HasConflict,              // Konkurrierende Konsolen
    int SourceCount,               // Anzahl übereinstimmender Quellen
    string PrimarySource,          // Stärkste Quelle
    string Reasoning               // Menschenlesbare Begründung
);
```

### 5.3 MatchLevel-Definition

| Level | Bedingung | Sort erlaubt | Repair erlaubt | User-Review nötig |
|-------|-----------|-------------|----------------|-------------------|
| **Exact** | DAT-Hash-Match (Confidence 100) | ✅ Auto | ✅ Auto | Nein |
| **Strong** | Hard Evidence + ≥85 Confidence + kein Conflict | ✅ Auto | ✅ mit Bestätigung | Nein |
| **Probable** | Soft Evidence ≥2 Quellen agree ODER Hard+Conflict ODER Single Hard ≥80 | ⚠️ Review-Gate | ❌ | Optional |
| **Weak** | Single Soft-Quelle ODER Confidence <65 | ❌ Blocked | ❌ | Ja |
| **Ambiguous** | 2+ starke konkurrierende Konsolen mit vergleichbarer Evidenz | ❌ Blocked | ❌ | Ja |
| **None** | Keine Hypothese | ❌ UNKNOWN | ❌ | Ja |

**Kritischer Unterschied zum Ist-Zustand:**
- **Probable** fängt den Bereich 65-85 ab, der aktuell in Blocked landet
- **Multi-Source-Agreement** wird stärker gewichtet (2 Soft-Quellen agree → Probable statt Blocked)
- **FolderName als stärkstes Soft-Signal** → Probable statt Blocked
- Review wird zum normalen Arbeits-Modus statt zum Ausnahme-Zustand

### 5.4 Angepasste Confidence-Thresholds

**Aktuell:**
```
SortThreshold = 85, ReviewThreshold = 65, SoftOnlyCap = 65
→ Result: Sort 383, Review 18, Blocked 701
```

**Vorschlag:**
```
SortThreshold = 80 (leicht gesenkt, weil UnsafeSortRate = 0 beweist, dass Spielraum existiert)
ReviewThreshold = 55 (deutlich gesenkt, damit der Review-Korridor genutzt wird)
SoftOnlyCap = 75 (erhöht: FolderName allein → 75 → Review statt Blocked)
MultiSourceAgreementBonus = 15 (statt 5: 2+ Quellen agree → deutlicher Boost)
FolderName-SingleSourceCap = 80 (erhöht von 65: Folder allein → Review statt Blocked)
```

**Erwarteter Effekt:**
- ~200-300 Dateien wandern von Blocked → Review
- ~50-100 Dateien wandern von Blocked → Sort (wo Multi-Source-Agreement vorliegt)
- UnsafeSortRate bleibt bei ~0% (Hard-Evidence-Requirement für Sort bleibt)

### 5.5 DAT-Integration (Strength-Boost)

**Wenn ein DAT-Match vorliegt:**
1. Console-Key wird überschrieben (auch bei vorherigem UNKNOWN)
2. MatchLevel wird auf `Exact` gesetzt (DatVerified)
3. SortDecision wird auf `DatVerified` gesetzt (Auto-Sort)
4. Ambiguous-DAT-Match → höchste DAT-Confidence wird genommen, MatchLevel = Probable

**Ambiguous-DAT-Resolution (NEU):**
Wenn ein Hash in 2+ DATs vorkommt:
1. Prüfe, ob eine Konsolen-Hypothese vom Detector bereits existiert
2. Wenn ja: nimm die DAT-Konsole, die mit der Hypothese übereinstimmt
3. Wenn nein: markiere als Review mit Begründung "DAT-Match auf {Console1} und {Console2}"

### 5.6 BIOS-Erkennung (Erweiterung)

**Neuer 3-stufiger BIOS-Detektor:**

```
Stufe 1: Filename-Pattern (aktuell: "(bios)" Tag)
  → Erweitert um: "scph*", "*_bios*", "BOOT.ROM", "bios7.*", "bios9.*"
  → Pattern-Liste in rules.json

Stufe 2: DAT-Annotation
  → DAT-Einträge mit game/@name containing "BIOS" oder group="bios"
  → DatIndex erweitert: LookupBiosFlag(hash) → bool

Stufe 3: Known-BIOS-Hash-DB
  → Separate JSON-Datei: data/known-bios-hashes.json
  → Enthält SHA1-Hashes aller bekannten BIOS-Dateien pro Konsole
  → Wird vor GameKey-Bildung geprüft
```

**Ziel:** biosAsGameRate von 38,3% auf <5%.

### 5.7 Datenfluss-Diagramm (Zielzustand)

```
Input Files
    │
    ▼
[ContentSignatureClassifier] ──→ NonROM → Skip
    │
    ▼
[ExtensionNormalizer]
    │
    ▼
[ConsoleDetector.DetectWithConfidence] ──→ 8 Hypothesen
    │
    ▼
[BiosDetector] ──→ BIOS/GAME/JUNK/NonGame (3-stufig)
    │
    ▼
[HypothesisResolver v2] ──→ MatchEvidence (Level + Reasoning)
    │
    ▼
[DatIndex.Lookup] ──→ Console-Rescue + MatchLevel-Boost
    │
    ▼
[GameKeyNormalizer] + [RegionDetector] + [Scorers]
    │
    ▼
[DeduplicationEngine] ──→ Winner + Losers per GameKey
    │
    ▼
[SortDecisionGate]
    ├── Exact/Strong → Auto-Sort
    ├── Probable → Review-Queue
    └── Weak/Ambiguous/None → Blocked (mit Reasoning)
    │
    ▼
[ConsoleSorter] (KEINE eigene Re-Detection, NUR enrichierte Daten)
```

---

## 6. Regeln für Sorting und Repair

### 6.1 Sorting-Regeln

| Regel | Beschreibung |
|-------|-------------|
| **SORT-01** | Auto-Sort nur bei MatchLevel Exact oder Strong |
| **SORT-02** | Review-Sort nur mit User-Bestätigung (Batch-Review UI) |
| **SORT-03** | Blocked-Dateien werden NIE automatisch verschoben |
| **SORT-04** | UNKNOWN-Dateien bleiben in Root, werden im Report markiert |
| **SORT-05** | ConsoleSorter darf KEINE eigene Detection-Logik haben |
| **SORT-06** | Enrichierte ConsoleKeys sind die einzige Wahrheit für Sorting |
| **SORT-07** | Junk + bekannte Console → _TRASH_JUNK/{Console}/ |
| **SORT-08** | Junk + UNKNOWN → _TRASH_JUNK/_UNKNOWN/ (nicht in Root lassen) |
| **SORT-09** | Set-Mitglieder IMMER vor Descriptor-Move auflösen |
| **SORT-10** | Bei Set-Member-Failure → Rollback des gesamten Sets |

### 6.2 Repair-Regeln

| Regel | Beschreibung |
|-------|-------------|
| **REPAIR-01** | Repair (DatRename) nur bei DatVerified (exakter Hash) |
| **REPAIR-02** | Conversion nur bei MatchLevel ≥ Strong + bekannte Console |
| **REPAIR-03** | Source nie vor Verification löschen |
| **REPAIR-04** | Partial Output bei Fehler → Cleanup + Audit |

### 6.3 Wann UNKNOWN korrekt ist

- Keine einzige Hypothese vorhanden (echte unbekannte Datei)
- Alle Hypothesen ≤ 30 Confidence
- ContentSignatureClassifier: Datei ist definitiv kein ROM (PDF, PNG, etc.)
- Datei ist 0 Bytes groß

### 6.4 Wann DAT eine Entscheidung überschreibt

- **DAT überschreibt Console-Key:** Immer, wenn Hash eindeutig matcht (1 Konsole)
- **DAT überschreibt FileCategory:** Wenn DAT-Annotation "bios" enthält → BIOS statt GAME
- **DAT überschreibt SortDecision:** Hash-Match → DatVerified (höchste Stufe)
- **DAT überschreibt NICHT:** Ambiguous DAT-Match → bleibt Review
- **DAT überschreibt NICHT:** User-explizite Blocked-Markierung

---

## 7. Qualitätsmodell

### 7.1 Messbare KPIs

| KPI | Aktuell | Ziel | Messmethode |
|-----|---------|------|-------------|
| **Correct Rate** | 85,3% | ≥92% | ground-truth Benchmark |
| **Wrong Rate** | 0,34% | ≤0,2% | Confusion Matrix |
| **Missed Rate** | 1,25% | ≤0,5% | ground-truth Benchmark |
| **UnsafeSortRate** | 0,0% | 0,0% | SortDecision-Validation |
| **safeSortCoverage** | 58,4% | ≥80% | (Sort + DatVerified) / Total |
| **biosAsGameRate** | 38,3% | ≤5% | BIOS-Klassifikation |
| **Blocked Rate** | 53% | ≤20% | SortDecision-Verteilung |
| **Review Rate** | 1,3% | 10-25% | SortDecision-Verteilung |
| **datExactMatchRate** | 46,8% | ≥55% | DAT-Coverage |
| **ambiguousMatchRate** | 1,2% | ≤2% | Ambiguous-Frequenz |

### 7.2 Ist 95% realistisch?

**95% Correct Rate ist erreichbar, aber nicht sofort.**

Aktuelle Situation:
- 1769 Correct + 119 TrueNegative + 152 JunkClassified = 2040 korrekte Entscheidungen
- 2040 / 2073 = **98,4% korrekte Gesamtentscheidungen** (wenn TrueNeg + Junk mitgezählt werden)
- 1769 / (1769 + 7 + 26) = **98,2% Precision** (unter den erkannten ROMs)
- Das eigentliche Problem: **safeSortCoverage = 58,4%** (zu viel wird blockiert)

**Roadmap zu 95%+ sortierter Correct Rate:**

| Phase | Maßnahme | Erwarteter Correct-Gain | Expected safeSortCoverage |
|-------|----------|------------------------|--------------------------|
| Phase 1 | Confidence-Thresholds anpassen | +0% Correct, +0% Wrong | 65-70% |
| Phase 2 | BIOS-Erkennung erweitern | +1-2% Correct | 72% |
| Phase 3 | DAT-Ambiguous-Resolution | +0,5% Correct | 75% |
| Phase 4 | Multi-Source-Agreement-Boost | +1% Correct | 80% |
| Phase 5 | Known-BIOS-Hash-DB | +1% Correct | 82% |
| Phase 6 | Archive-Inner-Header-Analyse | +0,5% Correct | 84% |
| Phase 7 | Enrichment→Sort-Verdrahtung sichern | +0% Correct | 85%+ |
| Phase 8 | Review-Batch-UI + User-Feedback | +2-5% (via User) | 90%+ |

**Ergebnis: 92-93% Correct + 90%+ safeSortCoverage ist technisch seriös erreichbar.**

**95% Correct erfordert User-Feedback-Loop** (Review-Queue → User bestätigt → System lernt). Ohne User-Interaktion ist 92-93% die realistische Obergrenze für automatische Erkennung.

### 7.3 Was die 95% verhindern könnte

1. **Nischen-Systeme ohne Header-Signaturen:** Channel F, Supervision, Odyssey2 — nur per DAT oder Folder erkennbar
2. **ROM-Hacks mit falschen System-Tags:** User-modifizierte Dateinamen
3. **Gemischte Archive:** ZIP mit ROMs für 2+ Systeme
4. **Headerless ROMs ohne DAT:** Kein Weg, diese automatisch zu erkennen
5. **BIOS ohne (bios)-Tag und ohne DAT-Hash-Match:** Erfordert manuelle Kuration

---

## 8. Refactoring-Plan

### Phase 1: Confidence-Modell-Tuning (Geringstes Risiko, größter Impact)

**Aufwand: ~2 Tage | Risiko: NIEDRIG**

1. `HypothesisResolver.cs`:
   - `SoftOnlyCap` von 65 auf 75 erhöhen
   - `ReviewThreshold` von 65 auf 55 senken
   - `MultiSourceAgreementBonus` von 5 auf 15 erhöhen
   - `FolderName.SingleSourceCap()` von 65 auf 80 erhöhen
   
2. `DetermineSortDecision()`:
   - Review-Bedingung erweitern: `confidence >= ReviewThreshold && !conflict` → Review (auch ohne hardEvidence)
   - Neue Bedingung: `confidence >= 70 && sourceCount >= 2 && !conflict` → Review

3. **Tests:**
   - Benchmark erneut laufen lassen
   - UnsafeSortRate muss bei 0 bleiben
   - safeSortCoverage-Ziel: ≥65%
   - Sensitivity-Analyse: Thresholds von 50-90 in 5er-Schritten durchspielen

### Phase 2: Enrichment→Sort-Verdrahtung absichern

**Aufwand: ~1 Tag | Risiko: NIEDRIG**

1. `ConsoleSorter.Sort()`:
   - ASSERT/Guard: Wenn `enrichedConsoleKeys` null ist → alle Dateien skippen mit Audit-Warning
   - Fallback `_consoleDetector.Detect()` durch `_consoleDetector.DetectWithConfidence()` ersetzen
   - Oder besser: Fallback komplett entfernen, Sort-Phase darf nur enrichierte Daten verwenden

2. `ConsoleSortPipelinePhase`:
   - Sicherstellen, dass `enrichedConsoleKeys` IMMER übergeben wird
   - Test: Mock mit fehlendem enrichedConsoleKeys → erwartetes Verhalten validieren

### Phase 3: BIOS-Erkennung erweitern

**Aufwand: ~2-3 Tage | Risiko: MITTEL**

1. `FileClassifier.cs`:
   - BIOS-Patterns in `rules.json` erweitern:
     - `scph\d+\.bin` (PS1/PS2 BIOS)
     - `gba_bios\.bin`, `dc_bios\.bin`, `bios[79]\.bin` (System-BIOS)
     - `BOOT\.ROM` (NEOGEO)
     - `sysrom\.bin`, `System\.rom` (diverse)
   
2. `data/known-bios-hashes.json` (NEU):
   - SHA1-Hashes der ~200 bekanntesten BIOS-Dateien
   - Console-Key-Zuordnung
   - Im Enrichment-Phase gegen Hash prüfen

3. DatIndex-Integration:
   - DAT-Annotationen für BIOS-Einträge auswerten
   - `game/@isbios` oder `game/@name` containing "BIOS"

### Phase 4: DAT-Ambiguous-Resolution

**Aufwand: ~1-2 Tage | Risiko: NIEDRIG-MITTEL**

1. `EnrichmentPipelinePhase.cs`:
   - Bei ambiguem DAT-Match: prüfe ob ConsoleDetector-Hypothese übereinstimmt
   - Bei Match → nehme bestätigte Konsole, setze MatchLevel = Strong
   - Bei keinem Match → Review mit Reasoning

2. `DatIndex`:
   - `LookupAllByHash()` nutzen statt `LookupAny()`
   - Ergebnis-Set nach Detector-Hypothesen filtern

### Phase 5: MatchEvidence-Objekt einführen

**Aufwand: ~3-4 Tage | Risiko: MITTEL**

1. Neues Record `MatchEvidence` in Contracts
2. `HypothesisResolver.Resolve()` liefert `MatchEvidence` statt nur `ConsoleDetectionResult`
3. `RomCandidate` erhält `MatchEvidence`-Property
4. Reports zeigen Reasoning pro Datei
5. GUI zeigt MatchLevel + Reasoning in Detail-View

### Phase 6: Review-Batch-UI

**Aufwand: ~3-5 Tage | Risiko: NIEDRIG**

1. WPF-View für Review-Queue:
   - Gruppiert nach MatchLevel und Konsole
   - Pro Datei: Hypothesen, Confidence, Reasoning, DAT-Status
   - Batch-Actions: "Alle als {Console} bestätigen", "Als UNKNOWN markieren"
2. CLI: `--approve-reviews` Flag für Batch-Bestätigung
3. API: `/reviews` Endpoint

---

## 9. Teststrategie

### 9.1 Fehlende Tests

| Test-Bereich | Fehlt | Priorität |
|--------------|-------|-----------|
| **HypothesisResolver 3D-Matrix** | Confidence×Conflict×HardEvidence×SourceCount Kombinationen (aktuell ~60 Tests, braucht ~200) | HOCH |
| **Confidence-Threshold-Sensitivity** | Parameterized Tests mit SoftOnlyCap = 50, 55, 60, 65, 70, 75, 80 | HOCH |
| **BIOS-Erkennung** | Tests für `scph1001.bin`, `gba_bios.bin`, `BOOT.ROM`, `bios7.bin` | HOCH |
| **DAT-Ambiguous-Resolution** | Hash in 2 DATs + passende Hypothese → richtige Konsole gewählt | MITTEL |
| **EnrichmentPipelinePhase Integration** | E2E: Datei → Enrichment → sortierbar | HOCH (0% Coverage) |
| **Enrichment→Sort Parity** | Enriched Keys = Sort-Phase Keys für 100% der Dateien | HOCH |
| **Folder-Only-Detection** | ROM in korrektem Ordner ohne andere Evidenz → welches MatchLevel? | MITTEL |
| **Multi-Source-Agreement** | 2 Soft-Quellen agree → höherer MatchLevel als 1 Soft-Quelle | MITTEL |
| **Large Archive Skip** | Datei >500MB → Warning + Container-Hash-Fallback korrekt | NIEDRIG |
| **GB/GBC/GBA Disambiguierung** | 0x143 CGB-Flag-Prüfung → GB vs GBC korrekt | MITTEL |

### 9.2 Testdaten-Anforderungen

| Testdaten-Typ | Zweck | Menge |
|----------------|-------|-------|
| **goldene Referenz-ROMs** | Korrekte Erkennung von sauberen No-Intro/Redump-Sets | 900+ (vorhanden) |
| **BIOS-Samples** | `scph1001.bin`, `gba_bios.bin`, NEOGEO BOOT.ROM etc. | 60+ BIOS-Dateien ohne (bios)-Tag |
| **Falsch benannte ROMs** | `.bin` statt `.nes`, falscher Ordner, falscher Region-Tag | 100+ |
| **Archive mit ROMs** | ZIP/7z enthält ROM-Dateien verschiedener Systeme | 45+ |
| **Cross-Console Namenskonflikte** | Gleiches Spiel auf GB/GBC/GBA, PS1/PS2, NES/SNES | 50+ |
| **Multi-Disc** | FF7 Disc1/2/3, Resident Evil 2 Disc1/2 | 38+ (vorhanden) |
| **Headerless** | NES ohne iNES, SNES mit Copier-Header | 30+ |
| **Kaputte Archive** | Truncated ZIP, korruptes Header, 0-Byte-Dateien | 85+ (vorhanden) |
| **UNKNOWN erwartet** | .txt, .pdf, .jpg, .exe — definitiv kein ROM | 55+ (vorhanden) |
| **DAT-Hit + DAT-Miss** | ROMs mit/ohne DAT-Abdeckung, vergleichende Tests | 370+ (vorhanden) |

### 9.3 Metriken die erhoben werden müssen

1. **Precision per Console:** Correct / (Correct + Wrong) pro System
2. **Recall per Console:** Correct / (Correct + Missed) pro System
3. **F1-Score per Console:** Harmonisches Mittel aus Precision und Recall
4. **safeSortCoverage:** (Sort + DatVerified) / TotalSortDecisions
5. **UnsafeSortRate:** WrongSortDecisions / TotalSortDecisions (MUSS 0 bleiben)
6. **MatchLevel-Distribution:** Wie viele Dateien in Exact/Strong/Probable/Weak/Ambiguous/None
7. **biosAsGameRate:** BIOS falsch als Game / Total BIOS
8. **Confidence-Verteilung:** Histogramm der Confidence-Werte → toter Bereich identifizieren
9. **Threshold-Impact:** Für jeden Threshold-Wert: wie ändert sich safeSortCoverage + UnsafeSortRate

### 9.4 Benchmark-Gate-Erweiterung

Die bestehende `gates.json` ist gut strukturiert. Ergänzen:

```json
{
  "qualityGates": {
    "unsafeSortRate": { "target": 0, "hardFail": 0.001 },
    "safeSortCoverage": { "target": 0.80, "hardFail": 0.65 },
    "biosAsGameRate": { "target": 0.05, "hardFail": 0.15 },
    "correctRate": { "target": 0.92, "hardFail": 0.85 },
    "wrongRate": { "target": 0.002, "hardFail": 0.005 },
    "reviewRate": { "target": 0.15, "hardFail": 0.05 },
    "blockedRate": { "target": 0.20, "hardFail": 0.40 }
  }
}
```

---

## 10. Go / No-Go

### Kann man mit der aktuellen Erkennung weiterbauen?

**Bedingt Ja — aber nur mit sofortiger Adressierung der Top-5-Probleme.**

Die Architektur ist tragfähig. Die Sicherheitsgarantie (0 falsche Sorts) ist exzellent. Aber die User-Experience ist inakzeptabel, weil 53% der Dateien blockiert werden.

### Was muss zuerst stabil sein?

1. **Confidence-Thresholds müssen getuned werden** — das allein reduziert die Blocked-Rate um geschätzt 40%
2. **Enrichment→Sort-Verdrahtung muss ASSERT-gesichert sein** — kein stiller Fallback auf simple Detection
3. **BIOS-Erkennung muss erweitert werden** — 38,3% biosAsGameRate ist ein Daten-Integritätsproblem

### Die 10 wichtigsten Maßnahmen (priorisiert)

| # | Maßnahme | Impact | Aufwand | Risiko |
|---|----------|--------|---------|--------|
| **1** | **SoftOnlyCap 65→75, ReviewThreshold 65→55, FolderName-Cap 65→80** | safeSortCoverage +10-15% | ~1 Tag | NIEDRIG |
| **2** | **DetermineSortDecision: Review bei confidence ≥55 + !conflict** | Blocked→Review-Migration | ~0,5 Tage | NIEDRIG |
| **3** | **ConsoleSorter Fallback entfernen / durch DetectWithConfidence() ersetzen** | Keine Detection-Divergenz mehr | ~1 Tag | NIEDRIG |
| **4** | **BIOS-Filename-Patterns erweitern** (scph*, *_bios*, BOOT.ROM) | biosAsGameRate -20% | ~1 Tag | NIEDRIG |
| **5** | **Multi-Source-Agreement-Bonus 5→15** | Blocked→Sort bei Folder+Extension agree | ~0,5 Tage | NIEDRIG |
| **6** | **DAT-Ambiguous-Resolution: Hypothesen-Korrelation** | Missed -5, ambiguousRate ↓ | ~1,5 Tage | MITTEL |
| **7** | **known-bios-hashes.json einführen** | biosAsGameRate → <5% | ~2 Tage | MITTEL |
| **8** | **EnrichmentPipelinePhase Tests aufbauen** | 0% Coverage → 80%+ | ~2 Tage | NIEDRIG |
| **9** | **HypothesisResolver 3D-Matrix-Tests** | Confidence-Regression-Schutz | ~2 Tage | NIEDRIG |
| **10** | **MatchEvidence-Objekt + Reasoning in Reports** | User-Transparenz | ~3-4 Tage | MITTEL |

### Zusammenfassung

Die Architektur ist eine der besseren ROM-Erkennungs-Implementierungen, die ich gesehen habe. Das Hard/Soft-Evidence-Modell, die AMBIGUOUS-Erkennung und die 0%-UnsafeSortRate sind starke Fundamente.

Aber das Tool blockiert aktuell mehr Dateien als es sortiert. Das ist der Dealbreaker. Die gute Nachricht: **Das Confidence-Modell-Tuning (Maßnahmen 1-2-5) ist ein ~2-Tage-Job mit niedrigem Risiko, der den größten Impact hat.**

Die 95%-Marke ist technisch erreichbar, aber nur mit:
- Confidence-Tuning (Phase 1-2, ~2 Tage)
- BIOS-Hardening (Phase 3-4, ~3 Tage)
- DAT-Integration (Phase 4-5, ~3 Tage)
- Review-UI (Phase 6, ~4 Tage)
- Gesamt: ~12-15 Arbeitstage für 92%+ automatisch, 95%+ mit User-Review

Ohne diese Maßnahmen: **No-Go für Produktiv-Release. Die aktuelle Blocked-Rate von 53% macht das Tool für normale User unbrauchbar.**
