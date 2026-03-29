# Full Deep Bughunt – Romulus Tracker

> **Datum:** 2026-03-29
> **Status:** Bedingt release-fähig – 6 Blocker offen (inkl. 2 API-Paritäts-Blocker)
> **Hinweis:** Neue Findings werden fortlaufend in diesem Dokument ergänzt.

---

## Release-Blocker (P1)

- [ ] **BUG-01** – ConversionGraph: Nicht-deterministischer Pfad bei gleichen Kosten
  - Datei: `ConversionGraph.cs:50-67`
  - Impact: Preview ↔ Execute Divergenz, CLI/GUI Inkonsistenz
  - Fix: Sekundären Tie-Breaker einführen (Tool-Name, Ziel-Extension)
  - [ ] TGAP-01: `ConversionGraph_EqualCostPaths_ReturnsDeterministicResult()`

- [ ] **BUG-03** – MovePipelinePhase: Set-Member-Move nicht atomar
  - Datei: `MovePipelinePhase.cs:90-130`
  - Impact: Orphaned BIN/TRACK files nach partiellem Fehler (Datenverlust-Risiko)
  - Fix: Preflight-Check ob alle Members moveable, dann Move, bei Fehler Rollback
  - [ ] TGAP-02: `MovePipelinePhase_SetMember_PartialFailure_RollsBackDescriptor()`

- [ ] **BUG-04** – ConsoleSorter: `__DUP` alphabetische Sortierung bricht bei ≥10
  - Datei: `ConsoleSorter.cs:334-341`
  - Impact: Rollback findet falsches File
  - Fix: Numerischen Comparer verwenden (DUP-Suffix als int parsen)
  - [ ] TGAP-03: `FindActualDestination_10PlusDuplicates_ReturnsHighestNumber()`

- [ ] **BUG-12** – API: OnlyGames/KeepUnknownWhenOnlyGames Validierung invertiert
  - Datei: `Program.cs:322-325`
  - Impact: Ungültige Konfigurationen akzeptiert, gültige rejected
  - Fix: `if (!request.OnlyGames && request.KeepUnknownWhenOnlyGames)` → Error
  - [ ] TGAP-07: `Api_OnlyGamesFalse_KeepUnknownTrue_Returns400()`
  - ⚠️ **Korrekturnotiz (Bughunt #5):** Logik ist tatsächlich korrekt — siehe Analyse in Bughunt #5

- [ ] **BUG-52** – API: PreferRegions-Reihenfolge divergiert von RunConstants
  - Datei: `RunLifecycleManager.cs:112`
  - Impact: JP/WORLD vertauscht → andere Dedupe-Ergebnisse als CLI/WPF
  - Fix: `RunConstants.DefaultPreferRegions` statt hardcoded Array
  - [ ] TGAP-42: `Api_DefaultPreferRegions_MatchRunConstants()`

- [ ] **BUG-53** – API: EnableDatAudit/EnableDatRename nicht in RunRecord propagiert
  - Datei: `RunLifecycleManager.cs:104-130`
  - Impact: DAT-Audit/Rename via API unmöglich; Fingerprint-Widerspruch
  - Fix: `EnableDatAudit = request.EnableDatAudit, EnableDatRename = request.EnableDatRename` in TryCreateOrReuse
  - [ ] TGAP-43: `Api_EnableDatAudit_PropagatedToRunRecord()`

---

## Hohe Priorität (P2)

- [ ] **BUG-14** – CSV-Report: Formula-Injection via Prefix statt Quoting
  - Datei: `AuditCsvParser.cs:54-73`
  - Impact: Security (OWASP CSV-Injection)
  - Fix: Felder mit `=`, `+`, `-`, `@` in RFC-4180-Quotes wrappen
  - [ ] TGAP-08: `SanitizeCsvField_FormulaPrefix_WrappedInQuotes()`

- [ ] **BUG-06** – RunReportWriter: Invarianten-Check übersprungen ohne DedupeGroups
  - Datei: `RunReportWriter.cs:82-88`
  - Impact: Accounting-Fehler in ConvertOnly-Mode bleiben stumm
  - Fix: Guard ändern zu `if (projection.TotalFiles > 0)`
  - [ ] TGAP-05: `ReportWriter_ConvertOnlyRun_ValidatesAccountingInvariant()`

- [ ] **BUG-05** – ParallelHasher: CancellationToken im Single-Thread-Pfad ignoriert
  - Datei: `ParallelHasher.cs:46-54`
  - Impact: Cancel wird bei ≤4 Dateien nicht respektiert
  - Fix: CancellationToken an `HashFilesSingleThread()` durchreichen
  - [ ] TGAP-04: `HashFilesAsync_SingleThread_RespectsCancellation()`

- [ ] **BUG-15** – Rollback DryRun vs Execute: Unterschiedliche Zählung
  - Datei: `AuditSigningService.cs:393-410`
  - Impact: Preview/Execute zeigen unterschiedliche Zahlen
  - Fix: Unified Counter-Semantik
  - [ ] TGAP-09: `Rollback_DryRun_Execute_SameCountSemantics()`

- [ ] **BUG-17** – ToolInvokerAdapter: Kein Timeout bei Tool-Aufruf
  - Datei: `ToolInvokerAdapter.cs:64-85`
  - Impact: Hängender Tool-Prozess blockiert Pipeline unbegrenzt
  - Fix: Timeout-Parameter für `InvokeProcess()` implementieren

- [ ] **BUG-13** – API: ApprovedReviewPaths nicht Thread-Safe
  - Datei: `Program.cs:554`
  - Impact: Parallele POST-Requests können `List<string>` korrumpieren
  - Fix: `ConcurrentBag<string>` oder `lock()` verwenden

---

## Mittlere Priorität (P3)

- [ ] **BUG-10** – CLI: Naive CSV-Parsing in `DeriveRootsFromAudit()`
  - Datei: `Program.cs:340-343`
  - Impact: Root-Pfade mit Komma werden abgeschnitten
  - Fix: `AuditCsvParser` verwenden statt manuelles `IndexOf(',')`
  - [ ] TGAP-06: `DeriveRootsFromAudit_PathWithComma_ExtractsFullPath()`

- [ ] **BUG-11** – CLI: `GetAwaiter().GetResult()` in `UpdateDats()`
  - Datei: `Program.cs:237`
  - Impact: Deadlock-Risiko (gering im CLI, aber Anti-Pattern)
  - Fix: Methode async machen oder `.Result` mit `ConfigureAwait(false)`

- [ ] **BUG-09** – GUI: `async void` Public Method `RefreshReportPreview()`
  - Datei: `LibraryReportView.xaml.cs:26`
  - Impact: Exceptions können unobserved bleiben
  - Fix: Return-Type auf `async Task` ändern, Caller anpassen

- [ ] **BUG-08** – Audit-Action-Strings: Inkonsistente Großschreibung
  - Datei: `MovePipelinePhase.cs:96` vs `AuditSigningService.cs:262-268`
  - Impact: Audit-Trail inkonsistent (funktional mitigiert durch OrdinalIgnoreCase)
  - Fix: Zentrale Action-Constants einführen (`AuditActions.Move`, etc.)

- [ ] **SEC-01** – API JSON Deserialization ohne TypeInfo
  - Impact: Security (niedrig)

---

## Niedrige Priorität (P4)

- [ ] **BUG-07** – DatRenamePipelinePhase: TOCTOU Race Condition
  - Datei: `DatRenamePipelinePhase.cs:42-52`
  - Impact: Defensiv abgesichert durch `RenameItemSafely`
  - Fix: Zielexistenz-Check in `RenameItemSafely()` atomar implementieren

- [ ] **BUG-16** – RegionDetector: Stille Unknown-Rückgabe ohne Diagnostik
  - Datei: `RegionDetector.cs:118-145`
  - Impact: Debugging von False-Unknown schwierig
  - Fix: Diagnostische Info (z.B. `out string? diagnosticInfo`) zurückgeben

- [ ] **BUG-02** – CompletenessScorer: Hardcodierte Set-Descriptor-Extensions
  - Datei: `CompletenessScorer.cs:25`
  - Impact: Neue Descriptor-Formate nicht erkannt
  - Fix: Extension-Liste aus Config oder zentrale Konstante

- [ ] **SEC-02** – SSE Stream ohne Max-Concurrency
  - Impact: DoS (niedrig)

- [ ] **SEC-03** – TrustForwardedFor Doku fehlt
  - Impact: Security (gering)

---

## Neue Findings – Fokus Recognition / Classification / Sorting (2026-03-29)

### Priorität 1 (P1)

- [ ] **BUG-18** – BIOS-Varianten werden über Region hinweg dedupliziert
  - Dateien: `src/RomCleanup.Core/Classification/CandidateFactory.cs`, `src/RomCleanup.Core/GameKeys/GameKeyNormalizer.cs`
  - Impact: BIOS (z. B. USA/Japan) kann fälschlich zusammengeführt werden
  - Ursache: BIOS-Key basiert auf normalisiertem `gameKey` ohne Region (`__BIOS__{gameKey}`)
  - Fix: BIOS-Key um Region erweitern oder BIOS aus Dedupe-Gruppierung ausnehmen
  - [ ] TGAP-11: `BiosVariants_DifferentRegions_AreNotDeduplicated()`

- [ ] **BUG-19** – DAT-Hash-Match überschreibt Junk-Kategorie nicht
  - Datei: `src/RomCleanup.Infrastructure/Orchestration/EnrichmentPipelinePhase.cs`
  - Impact: DAT-verifizierte Spiele können trotz Hash-Match in `_TRASH_JUNK` landen
  - Ursache: DAT-Authority aktualisiert Confidence/SortDecision, aber nicht `category`
  - Fix: Bei echtem DAT-Hash-Match Kategorie auf `Game` anheben (Name-only Match ausgenommen)
  - [ ] TGAP-12: `DatHashMatch_JunkTag_IsRecoveredToGameCategory()`

### Priorität 2 (P2)

- [ ] **BUG-20** – AmbiguousExtension kann Review-Schwelle nie erreichen
  - Dateien: `src/RomCleanup.Core/Classification/DetectionHypothesis.cs`, `src/RomCleanup.Core/Classification/HypothesisResolver.cs`
  - Impact: AmbiguousExtension-Pfad ist praktisch tot (immer Blocked)
  - Ursache: `SingleSourceCap(AmbiguousExtension)=40` bei `ReviewThreshold=55`
  - Fix: Cap anheben oder Pfad explizit entfernen/dokumentieren
  - [ ] TGAP-13: `AmbiguousExtension_SingleSource_CanReachReview()`

- [ ] **BUG-21** – ZIP-Inhaltsdetektion ist bei Gleichstand nicht deterministisch
  - Datei: `src/RomCleanup.Core/Classification/ConsoleDetector.cs`
  - Impact: Gleiche ZIP-Inhalte können je nach Entry-Reihenfolge unterschiedliche ConsoleKeys liefern
  - Ursache: Largest-file-Heuristik ohne stabilen Secondary-Tie-Break
  - Fix: Sekundären Sortschlüssel (`Entry.FullName`) ergänzen
  - [ ] TGAP-14: `ArchiveDetection_EqualSizeEntries_IsDeterministic()`

- [ ] **BUG-22** – Size-TieBreak für Switch-Formate bevorzugt fälschlich kleinere Dateien
  - Datei: `src/RomCleanup.Core/Scoring/FormatScorer.cs`
  - Impact: Bei `nsp/xci` kann unvollständiger Dump gewinnen
  - Ursache: Switch-Formate sind nicht in `DiscExtensions`
  - Fix: `nsp/xci` (ggf. `nsz/xcz`) in Disc-TieBreak-Logik aufnehmen
  - [ ] TGAP-15: `SwitchPackages_SizeTieBreak_PrefersLargerCanonicalDump()`

- [ ] **BUG-23** – DAT-Match bei UNKNOWN-Konsole wird nicht sauber auf DatVerified gehoben
  - Datei: `src/RomCleanup.Infrastructure/Orchestration/EnrichmentPipelinePhase.cs`
  - Impact: Echte DAT-Treffer können im Review/Blocked-Korridor bleiben
  - Ursache: DAT-Authority ist an Guard `consoleKey != UNKNOWN` gekoppelt
  - Fix: DAT-Authority auch bei UNKNOWN anwenden, wenn DAT-Konsole eindeutig ist
  - [ ] TGAP-16: `UnknownConsole_DatHashMatch_UpgradesToDatVerified()`

### Priorität 3 (P3)

- [ ] **BUG-24** – SNES Copier-Header-Bypass nur über Dateigröße
  - Datei: `src/RomCleanup.Core/Classification/CartridgeHeaderDetector.cs`
  - Impact: False Positives bei Dateien mit `size % 1024 == 512`
  - Ursache: Header-Skip ohne zusätzliche SNES-Header-Validierung
  - Fix: Checksum/Complement-Validierung oder zusätzliche Magic-Prüfung
  - [ ] TGAP-17: `SnesHeaderSkip_RequiresValidHeaderConsistency()`

- [ ] **BUG-25** – Regex-Timeouts in Keyword-Detection werden still geschluckt
  - Datei: `src/RomCleanup.Core/Classification/ConsoleDetector.cs`
  - Impact: Diagnose schwierig bei fehlerhaften/teuren Patterns
  - Ursache: Leerer Catch bei `RegexMatchTimeoutException`
  - Fix: mind. Warn-Logging/Telemetry bei Timeout
  - [ ] TGAP-18: `KeywordDetection_RegexTimeout_IsLoggedAndNonFatal()`

---

## Neue Findings – Fokus Conversion Engine (2026-03-29)

### Executive Verdict

Die Conversion-Engine ist architektonisch solide (Planner→Graph→Executor→Invoker Kette, SEC-CONV-01..07 Guards,
atomisches Multi-CUE-Rollback). Keine akuten Datenverlust-Bugs. Aber: **SavedBytes ist im Execute-Modus
systematisch 0** (P1), **3 Metriken-Counter permanent 0** (P2), **Legacy-Pfad hat keine Lossy→Lossy-Blockade**
(P2), und die **PsxtractInvoker-Verify prüft falsches Format** (P2). In Summe 3 P1, 6 P2, 4 P3 Findings.

### Datenintegritätsrisiken

| Risiko | Stelle | Schutzstatus |
|---|---|---|
| Source vor Verify löschen | ConversionPhaseHelper L82-101 | ✅ Verify VOR Move — korrekt |
| Partial Outputs | ConversionExecutor finally-Block | ✅ Intermediate-Cleanup korrekt |
| Partial Outputs bei Fehler-TargetPath=null | ToolInvokerSupport→ConversionPhaseHelper | ⚠️ Lücke: SEC-CONV-05 greift nicht |
| Lossy→Lossy im Graph-Pfad | ConversionGraph L107-108 | ✅ Geblockt |
| Lossy→Lossy im Legacy-Pfad | FormatConverterAdapter Legacy-Methoden | ❌ Nicht geprüft |
| Multi-CUE Atomizität | ConvertMultiCueArchive | ✅ Rollback bei Teilfehler |
| ZIP-Slip / Zip-Bomb | ExtractZipSafe SEC-CONV-01..04 | ✅ Guards vorhanden |

### Priorität 1 (P1)

- [ ] **BUG-26** – SavedBytes ist im Execute-Modus systematisch 0
  - Datei: `src/RomCleanup.Infrastructure/Orchestration/RunOrchestrator.PreviewAndPipelineHelpers.cs` (L381-392)
  - Impact: CLI, API, GUI und Reports zeigen Conversion Savings als 0
  - Reproduktion: Beliebige erfolgreiche Conversion ausführen → SavedBytes = 0
  - Erwartetes Verhalten: `SavedBytes = SourceSize - TargetSize`
  - Tatsächliches Verhalten: `ApplyConversionReport` prüft `sourceInfo.Exists` auf Original-Pfad, der bereits nach `_TRASH_CONVERTED` verschoben wurde → `Exists == false` → kein Savings-Delta
  - Ursache: Source wird in `ProcessConversionResult` (L97-101) in Trash verschoben BEVOR `ApplyConversionReport` die Dateigröße liest
  - Fix: Source-Größe im `ConversionResult` als `SourceSizeBytes`-Property speichern (z.B. vor dem Move), oder aus Trash-Pfad ablesen
  - [ ] TGAP-19: `ConversionSavedBytes_AfterSuccessfulConversion_IsPositive()`

- [ ] **BUG-27** – LossyWarning/VerifyPassed/VerifyFailed Counter permanent 0
  - Dateien: `src/RomCleanup.Infrastructure/Orchestration/RunOrchestrator.PreviewAndPipelineHelpers.cs`, `RunResultBuilder.cs` (L31-33)
  - Impact: CLI, API, GUI, Reports zeigen immer 0 für Lossy-Warnungen und Verify-Statistik
  - Reproduktion: Beliebige Conversion mit Verify → ConvertVerifyPassedCount bleibt 0
  - Erwartetes Verhalten: Counter werden aus ConversionResults berechnet
  - Tatsächliches Verhalten: Properties `ConvertLossyWarningCount`, `ConvertVerifyPassedCount`, `ConvertVerifyFailedCount` werden **nirgends zugewiesen**
  - Ursache: Fehlende Zuweisungslogik in `ApplyConversionReport`
  - Fix: In `ApplyConversionReport` berechnen aus `results`:
    - `LossyWarning = results.Count(r => r.SourceIntegrity == Lossy && r.Outcome == Success)`
    - `VerifyPassed = results.Count(r => r.VerificationResult == Verified)`
    - `VerifyFailed = results.Count(r => r.VerificationResult == VerifyFailed)`
  - [ ] TGAP-20: `ConversionMetrics_LossyAndVerify_ArePopulated()`

- [ ] **BUG-28** – Multi-CUE ConversionResult gibt nur ersten Output zurück
  - Datei: `src/RomCleanup.Infrastructure/Conversion/FormatConverterAdapter.cs` (L653)
  - Impact: Bei Multi-Disc-Archiv (z.B. 3-Disc PS1 ZIP) wird nur Disc 1 als TargetPath gespeichert → Disc 2+3 CHDs existieren aber werden nicht auditiert/getrackt
  - Reproduktion: ZIP mit 3 CUE-Dateien konvertieren → `ConversionResult.TargetPath = disc1.chd` nur
  - Erwartetes Verhalten: Alle erzeugten CHDs müssen im Result oder einem TargetPaths-Array referenziert sein
  - Tatsächliches Verhalten: `outputs[0]` als einziger TargetPath, Disc 2+3 ungetrackt
  - Ursache: `ConversionResult` hat nur ein `TargetPath`-Feld, Multi-Output nicht modelliert
  - Fix: `ConversionResult` um `AdditionalTargetPaths` erweitern oder Multi-CUE als separate ConversionResults modellieren
  - [ ] TGAP-21: `MultiCueArchive_AllOutputs_AreTrackedInResult()`

### Priorität 2 (P2)

- [ ] **BUG-29** – PsxtractInvoker.Verify prüft CHD-Magic statt ISO
  - Datei: `src/RomCleanup.Infrastructure/Conversion/ToolInvokers/PsxtractInvoker.cs` (L57-80)
  - Impact: psxtract PBP→ISO erzeugt gültige ISO, aber Verify schlägt fehl wegen CHD-Magic-Check → Error-Counter statt Converted
  - Ursache: Verify-Methode sucht "MComprHD" in Bytes 0-7 — das ist CHD-Format, nicht ISO
  - Fix: ISO-Verify durch Dateigröße > 0 + ggf. ISO-9660-Magic (`CD001` at offset 0x8001) ersetzen
  - [ ] TGAP-22: `PsxtractVerify_ValidIsoOutput_ReturnsVerified()`

- [ ] **BUG-30** – Legacy-Pfad hat keine Lossy→Lossy-Blockade
  - Datei: `src/RomCleanup.Infrastructure/Conversion/FormatConverterAdapter.cs` (Legacy-Methoden ConvertWithChdman/DolphinTool/SevenZip/Psxtract)
  - Impact: CSO→CHD oder NKit→RVZ (beide Lossy) können im Legacy-Pfad durchrutschen
  - Ursache: Nur der Graph-Pfad hat die Lossy→Lossy-Blockade (ConversionGraph L107-108). Der Legacy-Pfad (`Convert()`, `ConvertLegacy()`) prüft SourceIntegrity nicht
  - Fix: SourceIntegrity-Check in `Convert()`/`ConvertLegacy()` vor Tool-Aufruf einbauen
  - [ ] TGAP-23: `LegacyConversion_LossyToLossy_IsBlocked()`

- [ ] **BUG-31** – Partial-Output-Cleanup greift nicht bei TargetPath=null
  - Dateien: `src/RomCleanup.Infrastructure/Conversion/ToolInvokers/ToolInvokerSupport.cs` (L69), `src/RomCleanup.Infrastructure/Orchestration/ConversionPhaseHelper.cs` (L120-123)
  - Impact: Tool crasht mit partieller Datei → `ToolInvocationResult.OutputPath=null` → SEC-CONV-05 Guard prüft `convResult.TargetPath` aber dieses ist `null` → Cleanup wird übersprungen → partielle Datei bleibt auf Disk
  - Ursache: Bei `Success=false` setzt `FromToolResult` OutputPath auf null. SEC-CONV-05 kennt den tatsächlichen Pfad nicht mehr
  - Fix: TargetPath auch bei Fehler im ToolInvocationResult setzen (als `AttemptedOutputPath`), oder Cleanup im ConversionExecutor anhand von BuildOutputPath
  - [ ] TGAP-24: `ToolFailure_PartialOutput_IsCleanedUp()`

- [ ] **BUG-32** – CancellationToken wird nicht an InvokeProcess durchgereicht
  - Dateien: `src/RomCleanup.Infrastructure/Conversion/ToolInvokers/PsxtractInvoker.cs` (L33,48), `ChdmanInvoker.cs`, `DolphinToolInvoker.cs`, `SevenZipInvoker.cs`
  - Impact: Cancel-Request während laufendem Tool-Prozess hat keine Wirkung — Tool läuft bis zum Ende
  - Ursache: Token wird nur vor dem Aufruf geprüft (`ThrowIfCancellationRequested`), aber `InvokeProcess` hat keinen CT-Parameter
  - Fix: `IToolRunner.InvokeProcess` um CancellationToken erweitern, bei Cancel den Prozess killen
  - [ ] TGAP-25: `ToolInvocation_Cancellation_KillsProcess()`

- [ ] **BUG-33** – SourceIntegrityClassifier: CHD/RVZ/NKit als Unknown statt korrekt klassifiziert
  - Datei: `src/RomCleanup.Core/Conversion/SourceIntegrityClassifier.cs`
  - Impact: CHD (.chd) und RVZ (.rvz) sind Lossless-Kompressionsformate, werden aber als `Unknown` klassifiziert → bei Unknown+Lossy-Step wird Conversion geblockt obwohl sie sicher wäre
  - Ursache: `LosslessExtensions` enthält `.chd` und `.rvz` NICHT
  - Fix: `.chd`, `.rvz`, `.gcz`, `.wia`, `.nsp`, `.xci` in LosslessExtensions aufnehmen
  - [ ] TGAP-26: `SourceIntegrity_Chd_IsLossless()`

- [ ] **BUG-34** – ConversionOutcome.Success ≠ counters.Converted (Report-Inkonsistenz)
  - Dateien: `src/RomCleanup.Infrastructure/Orchestration/ConversionPhaseHelper.cs` (L75-113), `RunOrchestrator.PreviewAndPipelineHelpers.cs` (L397-406)
  - Impact: `ConversionReport.Results` enthält Einträge mit `Outcome==Success` die intern als `Errors` gezählt werden (Verify-Failed). Wer direkt Results zählt bekommt andere Zahlen als die Counter
  - Ursache: ConversionPhaseHelper re-klassifiziert `Success→Error` bei Verify-Failure, aber das Outcome im Result bleibt `Success`
  - Fix: Bei Verify-Failure das Outcome im ConversionResult auf `Error` updaten, oder ein separates `FinalOutcome`-Feld einführen
  - [ ] TGAP-27: `ConversionReport_CounterVsOutcome_AreConsistent()`

### Priorität 3 (P3)

- [ ] **BUG-35** – ConversionPhaseHelper hat keine DryRun-Absicherung
  - Datei: `src/RomCleanup.Infrastructure/Orchestration/ConversionPhaseHelper.cs`
  - Impact: Wenn ein Caller versehentlich `ConvertSingleFile` im Preview-Modus aufruft, wird echte Conversion + Move ausgeführt
  - Ursache: Keine `options.DryRun`-Prüfung in dieser Helper-Klasse
  - Fix: Guard `if (options.DryRun) return null;` am Anfang von `ConvertSingleFile`
  - [ ] TGAP-28: `ConvertSingleFile_DryRun_SkipsConversion()`

- [ ] **BUG-36** – kein Timeout für Tool-Prozesse
  - Dateien: `src/RomCleanup.Infrastructure/Conversion/ToolInvokerAdapter.cs`, alle ToolInvokers
  - Impact: Hängender chdman/dolphintool/psxtract/7z-Prozess blockiert Pipeline unbegrenzt
  - Ursache: `IToolRunner.InvokeProcess` hat keinen Timeout-Parameter
  - Fix: Konfigurierbare Timeouts pro Tool (z.B. chdman=30min, 7z=10min), Process.Kill bei Überschreitung
  - (bereits als BUG-17 separat getrackt, hier für Conversion-Kontext referenziert)

- [ ] **BUG-37** – ConversionRegistryLoader: Doppelte Console-Keys werden still überschrieben
  - Datei: `src/RomCleanup.Infrastructure/Conversion/ConversionRegistryLoader.cs` (L208)
  - Impact: Bei duplizierten Keys in consoles.json gewinnt der letzte Eintrag ohne Warnung
  - Ursache: `policies[key] = policy` ohne Duplikat-Check
  - Fix: Duplikat-Detection + Warn-Log oder Exception
  - [ ] TGAP-29: `RegistryLoader_DuplicateConsoleKey_FailsOrWarns()`

- [ ] **BUG-38** – ToolInvokerAdapter.BuildArguments: chdman CD/DVD-Heuristik dupliziert
  - Dateien: `src/RomCleanup.Infrastructure/Conversion/ToolInvokerAdapter.cs` (L131-137), `ChdmanInvoker.cs` (L47-51), `FormatConverterAdapter.cs` (L451-461)
  - Impact: Die "createdvd→createcd bei CD-Image"-Heuristik ist an 3 Stellen implementiert mit leicht unterschiedlichen Schwellwerten
  - Ursache: Legacy-Pfad, Adapter, und spezialisierter Invoker alle mit eigener Kopie
  - Fix: Zentralisieren in `ToolInvokerSupport.ResolveEffectiveChdmanCommand()`
  - [ ] TGAP-30: `ChdmanCommand_CdDvdHeuristic_IsCentralized()`

### Invarianten, die aktuell verletzt werden

1. **SavedBytes-Invariante**: `ConvertSavedBytes > 0` wenn mindestens eine erfolgreiche Compression stattfand → **verletzt** (immer 0)
2. **Counter-Vollständigkeit**: `LossyWarning + VerifyPassed + VerifyFailed > 0` wenn Conversions stattfanden → **verletzt** (immer 0)
3. **Outcome-Counter-Parität**: `Results.Count(Outcome==Success) == ConvertedCount` → **verletzt** (Verify-Failed Success ≠ Error-Counter)
4. **Lossy→Lossy überall blockiert**: Graph hat Guard, Legacy-Pfad nicht → **verletzt**
5. **Multi-Output-Tracking**: Alle erzeugten Dateien müssen als TargetPaths im Result stehen → **verletzt** (Multi-CUE nur outputs[0])
6. **Cleanup-Vollständigkeit**: Jeder fehlgeschlagene Conversion muss partielle Outputs aufräumen → **verletzt** (TargetPath=null Gap)

---

## Fehlende Tests (TGAP)

| ID | Test | Bug | Status |
|---|---|---|---|
| [ ] TGAP-01 | `ConversionGraph_EqualCostPaths_ReturnsDeterministic()` | BUG-01 | offen |
| [ ] TGAP-02 | `MovePipelinePhase_SetMember_PartialFailure_RollsBack()` | BUG-03 | offen |
| [ ] TGAP-03 | `FindActualDestination_10PlusDuplicates_ReturnsHighest()` | BUG-04 | offen |
| [ ] TGAP-04 | `HashFilesAsync_SingleThread_RespectsCancellation()` | BUG-05 | offen |
| [ ] TGAP-05 | `ReportWriter_ConvertOnly_ValidatesInvariant()` | BUG-06 | offen |
| [ ] TGAP-06 | `DeriveRootsFromAudit_PathWithComma_FullPath()` | BUG-10 | offen |
| [ ] TGAP-07 | `Api_OnlyGames_KeepUnknown_ValidationMatrix()` | BUG-12 | offen |
| [ ] TGAP-08 | `SanitizeCsvField_Formula_QuotedCorrectly()` | BUG-14 | offen |
| [ ] TGAP-09 | `Rollback_DryRun_Execute_SameCountSemantics()` | BUG-15 | offen |
| [ ] TGAP-10 | `Rollback_MissingDestFile_CountsCorrectly()` | — | offen |
| [ ] TGAP-11 | `BiosVariants_DifferentRegions_AreNotDeduplicated()` | BUG-18 | offen |
| [ ] TGAP-12 | `DatHashMatch_JunkTag_IsRecoveredToGameCategory()` | BUG-19 | offen |
| [ ] TGAP-13 | `AmbiguousExtension_SingleSource_CanReachReview()` | BUG-20 | offen |
| [ ] TGAP-14 | `ArchiveDetection_EqualSizeEntries_IsDeterministic()` | BUG-21 | offen |
| [ ] TGAP-15 | `SwitchPackages_SizeTieBreak_PrefersLargerCanonicalDump()` | BUG-22 | offen |
| [ ] TGAP-16 | `UnknownConsole_DatHashMatch_UpgradesToDatVerified()` | BUG-23 | offen |
| [ ] TGAP-17 | `SnesHeaderSkip_RequiresValidHeaderConsistency()` | BUG-24 | offen |
| [ ] TGAP-18 | `KeywordDetection_RegexTimeout_IsLoggedAndNonFatal()` | BUG-25 | offen |
| [ ] TGAP-19 | `ConversionSavedBytes_AfterSuccessfulConversion_IsPositive()` | BUG-26 | offen |
| [ ] TGAP-20 | `ConversionMetrics_LossyAndVerify_ArePopulated()` | BUG-27 | offen |
| [ ] TGAP-21 | `MultiCueArchive_AllOutputs_AreTrackedInResult()` | BUG-28 | offen |
| [ ] TGAP-22 | `PsxtractVerify_ValidIsoOutput_ReturnsVerified()` | BUG-29 | offen |
| [ ] TGAP-23 | `LegacyConversion_LossyToLossy_IsBlocked()` | BUG-30 | offen |
| [ ] TGAP-24 | `ToolFailure_PartialOutput_IsCleanedUp()` | BUG-31 | offen |
| [ ] TGAP-25 | `ToolInvocation_Cancellation_KillsProcess()` | BUG-32 | offen |
| [ ] TGAP-26 | `SourceIntegrity_Chd_IsLossless()` | BUG-33 | offen |
| [ ] TGAP-27 | `ConversionReport_CounterVsOutcome_AreConsistent()` | BUG-34 | offen |
| [ ] TGAP-28 | `ConvertSingleFile_DryRun_SkipsConversion()` | BUG-35 | offen |
| [ ] TGAP-29 | `RegistryLoader_DuplicateConsoleKey_FailsOrWarns()` | BUG-37 | offen |
| [ ] TGAP-30 | `ChdmanCommand_CdDvdHeuristic_IsCentralized()` | BUG-38 | offen |

---

## Positiv-Befunde (bestätigt ✓)

- [x] HTML-Reports: Konsequentes `WebUtility.HtmlEncode()`
- [x] XXE-Protection: `DtdProcessing.Prohibit`
- [x] Tool-Invocation: `ArgumentList` statt String-Concat
- [x] API-Auth: Fixed-Time-Comparison
- [x] FileSystem: Root-Validation mit NFC-Normalisierung
- [x] RunOrchestrator: Saubere Phase-Error-Propagation
- [x] ConversionExecutor: Intermediate-Cleanup auf allen Fehler-Pfaden
- [x] ConversionExecutor: Path-Traversal-Guard für Output-Pfade
- [x] ConversionExecutor: Contiguous-Step-Order-Validierung
- [x] ConversionExecutor: Safe-Extension-Validierung
- [x] ZIP-Extraktion: Zip-Slip + Zip-Bomb + Reparse-Point Guards (SEC-CONV-01..07)
- [x] 7z-Extraktion: Post-Extraction Path-Traversal + Reparse-Point Validierung
- [x] ConversionGraph: Lossy→Lossy Blockade im Graph-Pfad
- [x] ConversionGraph: Depth-Limit (max 5 Steps)
- [x] Multi-CUE: Atomisches Rollback bei Teilfehler
- [x] CUE-Selektion: Deterministische alphabetische Sortierung
- [x] ConversionPhaseHelper: Verify VOR Source-Move (korrekte Reihenfolge)
- [x] ConversionPhaseHelper: Counter-Partitionierung ohne Double-Counting
- [x] Set-Member-Move: Root-Validierung (SEC-MOVE-06)
- [x] PBP-Encryption-Detection: Saubere Read-Only-Analyse
- [x] ConversionConditionEvaluator: Safe IOException-Handling für FileSizeProvider

---

## Neue Findings – Fokus GUI / UX / WPF (Bughunt #4)

**Datum:** 2026-06-30
**Scope:** WPF Entry Point, ViewModels, Settings-Persistenz, State Machine, Projections, Code-Behind, XAML Bindings, Threading
**Methode:** Deep Code Reading aller ViewModels, Services, Code-Behind-Dateien, XAML-Bindings; gezielte Grep-Analyse auf Persistenz-Lücken, Threading-Patterns, CanExecute-Logik, Dispatcher-Nutzung

### Executive Verdict

Die GUI-Schicht ist architektonisch solide aufgebaut: MVVM wird konsequent eingehalten, CommunityToolkit.Mvvm wird korrekt verwendet, Projections sind immutabel, und der RunStateMachine-FSM ist sauber implementiert. Der Thread-sichere AddLog-Pattern und die Fingerprint-basierte Move-Gate-Logik sind vorbildlich.

Jedoch bestehen **zwei P1-Befunde** (toter Code mit Divergenz-Risiko), **sechs P2-Befunde** (3× fehlende Settings-Persistenz, 1× Rollback ohne Integrity-Check, 1× tote Konsolen-Filter, 1× unvollständiger Property-Sync), und **fünf P3-Befunde** (UX-Klarheit, Wartbarkeit, async void).

Die kritischsten Risiken: Ein Entwickler könnte versehentlich die toten Rollback-Stacks oder CTS in RunViewModel statt der echten in MainViewModel nutzen, und die Persistenz-Lücken erzeugen bei jedem Neustart Rücksetzungen von MinimizeToTray, IsSimpleMode und SchedulerIntervalMinutes.

---

### BUG-39 · Duplicate Rollback Stacks — Dead Code in RunViewModel

| Feld | Wert |
|------|------|
| **Schweregrad** | P1 — Datenverlust-Risiko bei versehentlicher Nutzung |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/RunViewModel.cs` L90–130, `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.RunPipeline.cs` L195–245 |
| **Reproduktion** | RunViewModel enthält `_rollbackUndoStack`, `_rollbackRedoStack`, `PushRollbackUndo()`, `PopRollbackUndo()`, `PopRollbackRedo()`. Identische Kopien existieren in MainViewModel.RunPipeline.cs. Nur MainVM's Kopien werden tatsächlich aufgerufen. |
| **Erwartetes Verhalten** | Eine einzige Rollback-Stack-Implementierung existiert an einem Ort. |
| **Tatsächliches Verhalten** | Zwei parallele Implementierungen — RunVM's Kopien sind Dead Code. |
| **Ursache** | Halbfertiger Refactor: Rollback-Logik wurde nach MainVM.RunPipeline verschoben, RunVM-Kopie nicht entfernt. |
| **Fix** | Rollback-Stacks aus RunViewModel entfernen. |
| **Testabsicherung** | TGAP-31: Bestehende Rollback-Tests müssen weiter grün bleiben nach Deletion. |

---

### BUG-40 · Duplicate CancellationTokenSource — Dead Code in RunViewModel

| Feld | Wert |
|------|------|
| **Schweregrad** | P1 — Nicht-abbrechbarer Prozess bei versehentlicher Nutzung |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/RunViewModel.cs` L368–385, `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.RunPipeline.cs` (top) |
| **Reproduktion** | RunViewModel hat eigene `_cts`, `_ctsLock`, `CreateRunCancellation()`, `CancelRun()`. MainViewModel.RunPipeline.cs hat identische Felder. Nur MainVM's CTS wird in `ExecuteRunAsync()` verwendet. |
| **Erwartetes Verhalten** | Eine CTS-Instanz, ein Cancel-Pfad. |
| **Tatsächliches Verhalten** | Zwei parallele CTS-Implementierungen — RunVM's ist Dead Code. |
| **Ursache** | Halbfertiger Refactor: CTS-Management nach MainVM verschoben, RunVM nicht bereinigt. |
| **Fix** | CTS-Logik aus RunViewModel entfernen. |
| **Testabsicherung** | TGAP-32: Cancel-Tests müssen nach Deletion weiter grün bleiben. |

---

### BUG-41 · Rollback ohne Trash-Integrity-Preflight

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — Datenverlust-Risiko (stiller Teilfehler) |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.RunPipeline.cs` L607–640, `src/RomCleanup.Infrastructure/Audit/RollbackService.cs` L47 |
| **Reproduktion** | `OnRollbackAsync()` prüft nur `File.Exists(LastAuditPath)`, ruft dann direkt `RollbackService.Execute()` auf. `RollbackService.VerifyTrashIntegrity()` existiert (L47), wird aber nie vor dem Rollback aufgerufen. |
| **Erwartetes Verhalten** | Vor dem Rollback wird `VerifyTrashIntegrity()` aufgerufen. Bei fehlenden Trash-Dateien wird der User gewarnt und kann abbrechen. |
| **Tatsächliches Verhalten** | Rollback wird ohne Integritätsprüfung gestartet. Manuell gelöschte Trash-Dateien führen zu stillen Fehlern (SkippedMissingDest im Result). |
| **Ursache** | VerifyTrashIntegrity wurde implementiert, aber nie in den UI-Rollback-Flow integriert. |
| **Fix** | Vor `RollbackService.Execute()` erst `VerifyTrashIntegrity()` aufrufen und Ergebnis im Confirm-Dialog anzeigen. |
| **Testabsicherung** | TGAP-33: `Rollback_WithMissingTrashFiles_ShowsIntegrityWarning()` |

---

### BUG-42 · MinimizeToTray wird nicht persistiert

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — Settings werden still zurückgesetzt |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.Settings.cs` L31 (AutoSavePropertyNames), `src/RomCleanup.UI.Wpf/Services/SettingsDto.cs`, `src/RomCleanup.UI.Wpf/Services/SettingsService.cs` |
| **Reproduktion** | 1) User aktiviert MinimizeToTray. 2) Neustart. 3) MinimizeToTray ist deaktiviert. |
| **Erwartetes Verhalten** | MinimizeToTray überlebt Neustarts. |
| **Tatsächliches Verhalten** | Property ist in `AutoSavePropertyNames` (Debounce-Timer triggert `_settingsDirty`), aber `SettingsDto` hat kein `MinimizeToTray`-Feld und `SettingsService.SaveFrom()`/`Load()` enthält es nicht. |
| **Ursache** | Property wurde zur AutoSave-Liste hinzugefügt, aber nie zum DTO und Service propagiert. |
| **Fix** | `MinimizeToTray` zu SettingsDto hinzufügen, in SettingsService.SaveFrom (ui-Section) und Load/ApplyToViewModel aufnehmen. |
| **Testabsicherung** | TGAP-34: `Settings_MinimizeToTray_RoundTrip()` |

---

### BUG-43 · IsSimpleMode wird nicht persistiert

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — Settings werden still zurückgesetzt |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.Settings.cs` L466–472, `src/RomCleanup.UI.Wpf/ViewModels/SetupViewModel.cs` L182–188, `src/RomCleanup.UI.Wpf/Services/SettingsDto.cs`, `src/RomCleanup.UI.Wpf/Services/SettingsService.cs` |
| **Reproduktion** | 1) User wechselt in Expert-Modus. 2) Neustart. 3) App startet immer in Simple-Modus (default `true`). |
| **Erwartetes Verhalten** | IsSimpleMode überlebt Neustarts. |
| **Tatsächliches Verhalten** | Identische Property existiert in MainViewModel UND SetupViewModel (Duplikat), weder in SettingsDto noch in SettingsService enthalten. |
| **Ursache** | Property wurde als UI-State betrachtet, nicht als persistierbare Einstellung. Zusätzlich: Duplikat in zwei ViewModels. |
| **Fix** | 1) `IsSimpleMode` zu SettingsDto und SettingsService hinzufügen. 2) Duplikat in SetupViewModel entfernen, stattdessen an MainViewModel delegieren. |
| **Testabsicherung** | TGAP-35: `Settings_IsSimpleMode_RoundTrip()`, TGAP-36: `SetupVM_IsSimpleMode_DelegatesToMainVM()` |

---

### BUG-44 · Unvollständige Main→Setup Property-Synchronisation

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — UI-Inkonsistenz |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.Settings.cs` L65–107, L689–705 |
| **Reproduktion** | 1) Ändere ToolDolphin im Setup-Tab → wird korrekt zu MainVM propagiert (OnSetupPropertyChanged via Reflection). 2) Ändere ToolDolphin programmatisch auf MainVM (z.B. via Settings-Load) → Setup-Tab zeigt alten Wert. |
| **Erwartetes Verhalten** | Alle Tool-Pfade werden bidirektional synchronisiert. |
| **Tatsächliches Verhalten** | `SyncToSetup()` wird nur für `TrashRoot` (L65) und `ToolChdman` (L90) aufgerufen. ToolDolphin (L93), Tool7z (L96), ToolPsxtract (L99), ToolCiso (L102) rufen `SyncToSetup()` nicht auf. Reverse-Sync (Setup→Main) funktioniert für alle via Reflection. |
| **Ursache** | SyncToSetup-Aufrufe wurden bei der Erweiterung der Tool-Pfade nicht für alle neuen Properties hinzugefügt. |
| **Fix** | `SyncToSetup()` für alle Tool-Pfad-Properties im Setter aufrufen. |
| **Testabsicherung** | TGAP-37: `MainVM_ToolPathChange_PropagesToSetupVM(string toolProperty)` |

---

### BUG-45 · Console-Filter haben keinen Pipeline-Effekt

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — Fehlbedienungsrisiko / irreführende UI |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.cs` L330–337, `src/RomCleanup.UI.Wpf/Services/RunService.cs` L143–183 |
| **Reproduktion** | 1) Deaktiviere "PS1" in Console-Filtern. 2) Starte DryRun. 3) PS1-ROMs werden trotzdem verarbeitet. |
| **Erwartetes Verhalten** | Console-Filter begrenzen, welche Konsolen im Pipeline verarbeitet werden, ODER die Filter sind klar als "Anzeige-Filter" gekennzeichnet. |
| **Tatsächliches Verhalten** | `GetSelectedConsoles()` existiert in MainViewModel (L336) und SetupViewModel (L284), wird aber von `ViewModelRunOptionsSource` NICHT gelesen. `IRunOptionsSource` hat kein Console-Filter-Feld. Die Pipeline verarbeitet alle Konsolen unabhängig von der UI-Auswahl. |
| **Ursache** | Console-Filter-Feature wurde in der UI implementiert, aber nie an die Pipeline angebunden. |
| **Fix** | Entweder: (A) Console-Filter in `IRunOptionsSource` / `RunOptions` aufnehmen und im Pipeline respektieren. Oder: (B) Console-Filter in der UI klar als "Anzeige-/Report-Filter" kennzeichnen, nicht als Pipeline-Steuerung. |
| **Testabsicherung** | TGAP-38: `RunOptions_ConsoleFilter_ExcludesConsoles()` oder `ConsoleFilter_LabelClearlyIndicatesDisplayOnly()` |

---

### BUG-46 · SchedulerIntervalMinutes wird nicht persistiert

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — Settings werden still zurückgesetzt |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.Settings.cs` L188–203, `src/RomCleanup.UI.Wpf/Services/SettingsDto.cs`, `src/RomCleanup.UI.Wpf/Services/SettingsService.cs` |
| **Reproduktion** | 1) User setzt Scheduler-Intervall auf 30 Minuten. 2) Neustart. 3) Intervall ist 0 (default). |
| **Erwartetes Verhalten** | SchedulerIntervalMinutes überlebt Neustarts. |
| **Tatsächliches Verhalten** | Property existiert in MainViewModel (L188), wird in RunPipeline gelesen (L1004), fehlt aber in SettingsDto und SettingsService. |
| **Ursache** | Feature wurde implementiert, DTO-/Service-Integration vergessen. |
| **Fix** | `SchedulerIntervalMinutes` zu SettingsDto und SettingsService hinzufügen. |
| **Testabsicherung** | TGAP-39: `Settings_SchedulerIntervalMinutes_RoundTrip()` |

---

### BUG-47 · Dashboard unterscheidet nicht zwischen Plan (DryRun) und Actual (Move)

| Feld | Wert |
|------|------|
| **Schweregrad** | P3 — UX-Klarheit |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.RunPipeline.cs` L1092–1185, `src/RomCleanup.Contracts/Models/DashboardProjection.cs` |
| **Reproduktion** | 1) DryRun → Dashboard zeigt "Winners: 42, Dupes: 18". 2) Move → Dashboard zeigt "Winners: 42, Dupes: 18" im selben Format. Kein visueller Unterschied, kein Vergleich DryRun-Vorhersage ↔ Move-Ergebnis. |
| **Erwartetes Verhalten** | DryRun-Ergebnisse sind als "(Plan)" / "(Vorschau)" markiert. Nach Move werden Plan und Actual verglichen. |
| **Tatsächliches Verhalten** | `DashboardProjection.From()` nutzt dieselbe Darstellung für beide Modi. `MarkProvisional()` existiert für Cancelled/Failed, aber nicht für DryRun. |
| **Ursache** | DashboardProjection unterscheidet nur `isPartial` (Cancelled/Failed), nicht `isDryRun`. |
| **Fix** | DryRun-KPIs mit "(Plan)" Suffix markieren. Optional: Nach Move Plan↔Actual Delta anzeigen. |
| **Testabsicherung** | TGAP-40: `DashboardProjection_DryRun_ShowsPlanMarker()` |

---

### BUG-48 · ErrorSummaryProjection trunciert bei 50 ohne Report-Link

| Feld | Wert |
|------|------|
| **Schweregrad** | P3 — UX |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.RunPipeline.cs` (ErrorSummaryProjection-Nutzung) |
| **Reproduktion** | Run mit 100+ Fehlern → Nur 50 angezeigt, "… und 50 weitere" Text, kein Klick-Link zum vollständigen Report. |
| **Erwartetes Verhalten** | Truncation-Hinweis enthält einen Link/Button zum vollständigen Report. |
| **Tatsächliches Verhalten** | Nur Texthinweis ohne Aktion. |
| **Ursache** | Feature unvollständig implementiert. |
| **Fix** | "Vollständigen Report öffnen" Link im Truncation-Hinweis ergänzen. |
| **Testabsicherung** | Kein dedizierter Test nötig — rein UI. |

---

### BUG-49 · LibraryReportView: async void + fehlende Pfadvalidierung

| Feld | Wert |
|------|------|
| **Schweregrad** | P3 — Stabilität |
| **Dateien** | `src/RomCleanup.UI.Wpf/Views/LibraryReportView.xaml.cs` L26, L50 |
| **Reproduktion** | `RefreshReportPreview()` ist `async void` (kein Event Handler), ruft `Path.GetFullPath(vm.LastReportPath)` ohne vorige TryNormalizePath-Validierung auf. Bei ungültigem Pfad → unbehandelte Exception. |
| **Erwartetes Verhalten** | Methode ist `async Task` und der Aufrufer awaited. Pfad wird vor `GetFullPath` validiert. |
| **Tatsächliches Verhalten** | `async void` verschluckt Exception-Kontext. Pfad wird nur auf `IsNullOrEmpty` geprüft, nicht auf Gültigkeit. |
| **Ursache** | Quick-fix Implementierung ohne Robustifizierung. |
| **Fix** | Zu `async Task` ändern, `TryNormalizePath()` vor Pfad-Nutzung einsetzen. |
| **Testabsicherung** | TGAP-41: `LibraryReportView_InvalidPath_DoesNotThrow()` |

---

### BUG-50 · MissionControlViewModel unvollständig

| Feld | Wert |
|------|------|
| **Schweregrad** | P3 — Wartbarkeit |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MissionControlViewModel.cs` |
| **Reproduktion** | ViewModel hat nur 5 Properties, keine LastRun-Persistenz, SourceCount dupliziert Roots.Count Logik. |
| **Erwartetes Verhalten** | Entweder vollständig implementiert oder entfernt/als Stub gekennzeichnet. |
| **Tatsächliches Verhalten** | Halbfertiges ViewModel ohne klaren Nutzen. |
| **Ursache** | Feature-Entwicklung wurde nicht abgeschlossen. |
| **Fix** | Entweder fertigstellen oder als expliziten Stub markieren mit Tracking-Issue. |
| **Testabsicherung** | Kein dedizierter Test nötig. |

---

### BUG-51 · Duplicate IsSimpleMode/IsExpertMode in Main+Setup ViewModel

| Feld | Wert |
|------|------|
| **Schweregrad** | P3 — Doppelte Logik |
| **Dateien** | `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.Settings.cs` L466–472, `src/RomCleanup.UI.Wpf/ViewModels/SetupViewModel.cs` L182–188 |
| **Reproduktion** | Identischer Code in zwei ViewModels: `_isSimpleMode` Backing-Feld, Getter, Setter mit `SetProperty`, `IsExpertMode => !_isSimpleMode`. |
| **Erwartetes Verhalten** | Eine Single Source of Truth. |
| **Tatsächliches Verhalten** | Zwei unabhängige Kopien ohne Synchronisation. Änderung an einer ist für die andere unsichtbar. |
| **Ursache** | Copy-Paste bei ViewModel-Extraktion. |
| **Fix** | SetupViewModel.IsSimpleMode an MainViewModel delegieren (oder über Constructor-Parameter binden). |
| **Testabsicherung** | TGAP-36 (s. BUG-43). |

---

### Fehlbedienungsrisiken (Übersicht)

| # | Risiko | Betroffene Bugs |
|---|--------|----------------|
| 1 | Console-Filter suggerieren Pipeline-Kontrolle, haben aber keinen Effekt → User erwartet Einschränkung, die nicht stattfindet | BUG-45 |
| 2 | MinimizeToTray/IsSimpleMode/SchedulerInterval gehen bei Neustart verloren → User muss immer neu konfigurieren | BUG-42, BUG-43, BUG-46 |
| 3 | Rollback ohne Integrity-Check → stille Teilfehler wenn Trash manuell bereinigt wurde | BUG-41 |
| 4 | Dashboard-KPIs zeigen DryRun und Move identisch an → User kann Plan und Ergebnis nicht unterscheiden | BUG-47 |
| 5 | Setup-Tab zeigt ggf. veraltete Tool-Pfade nach programmatischem Settings-Load | BUG-44 |

---

### Zustands- und Paritätsprobleme

| # | Problem | Betroffene Bugs |
|---|---------|----------------|
| 1 | Dual-truth für Rollback-Stacks (RunVM vs. MainVM) — falscher Stack könnte benutzt werden | BUG-39 |
| 2 | Dual-truth für CancellationTokenSource (RunVM vs. MainVM) | BUG-40 |
| 3 | Dual-truth für IsSimpleMode (MainVM vs. SetupVM) | BUG-51 |
| 4 | Main→Setup Sync nur für 2 von 6 Tool-Pfaden implementiert | BUG-44 |
| 5 | Console-Filter-State existiert in UI, aber nicht in Pipeline-State | BUG-45 |

---

### Top 10 Fixes (priorisiert)

| # | Fix | Aufwand | Bugs |
|---|-----|---------|------|
| 1 | Dead Code entfernen: Rollback-Stacks + CTS aus RunViewModel | Klein | BUG-39, BUG-40 |
| 2 | MinimizeToTray in SettingsDto + SettingsService aufnehmen | Klein | BUG-42 |
| 3 | IsSimpleMode in SettingsDto + SettingsService aufnehmen + Duplikat in SetupVM entfernen | Klein | BUG-43, BUG-51 |
| 4 | SchedulerIntervalMinutes in SettingsDto + SettingsService aufnehmen | Klein | BUG-46 |
| 5 | SyncToSetup() für alle Tool-Pfade hinzufügen (ToolDolphin, Tool7z, ToolPsxtract, ToolCiso) | Klein | BUG-44 |
| 6 | VerifyTrashIntegrity() vor Rollback.Execute() aufrufen + Dialog | Mittel | BUG-41 |
| 7 | Console-Filter: Entweder Pipeline-Integration ODER klare "Anzeige-Filter" Kennzeichnung | Mittel | BUG-45 |
| 8 | DashboardProjection: DryRun-KPIs mit "(Plan)" markieren | Klein | BUG-47 |
| 9 | LibraryReportView.RefreshReportPreview → async Task + TryNormalizePath | Klein | BUG-49 |
| 10 | ErrorSummary: Report-Link bei Truncation ergänzen | Klein | BUG-48 |

---

### Positiv-Befunde GUI (bestätigt ✓)

- [x] MVVM konsequent: Keine Businesslogik im Code-Behind (MainWindow delegiert vollständig)
- [x] AddLog: Thread-sicherer Dispatcher-Pattern mit CheckAccess + InvokeAsync
- [x] RunStateMachine: 11-State FSM mit expliziter Transition-Validierung
- [x] Preview-Fingerprint: 23 Properties im Hash → robustes Move-Gate
- [x] ConfigChangedBanner (TASK-176): Korrekte Erkennung von Fingerprint-Divergenz
- [x] CanStartCurrentRun: Saubere Komposition aus IsBusy + Roots + Validation + Fingerprint
- [x] NotifyAllCommands: 13 Commands werden bei State-Change aktualisiert
- [x] XAML Bindings: Keine TwoWay-Bindings auf Read-Only-Properties gefunden
- [x] Path Traversal Guard: DAT-Import in FeatureCommandService korrekt geschützt
- [x] Settings Auto-Save: 2s Debounce + 5min Periodic → kein Datenverlust bei Crash
- [x] OnClosing: Sauberer busy-cancel-wait-reclose Pattern mit _isClosing Guard
- [x] INotifyDataErrorInfo: Tool-Pfade und Directories werden validiert (blocking vs warning)
- [x] Async Event Handler: OnClosing + OnRunRequested haben korrekte Exception-Handler

---

### Konsolidierte Test-Gap-Tabelle (GUI/UX/WPF)

| Status | ID | Test | Bug | Prio |
|--------|-----|------|-----|------|
| [ ] | TGAP-31 | `Rollback_AfterDeadCodeRemoval_StillWorks()` | BUG-39 | offen |
| [ ] | TGAP-32 | `Cancel_AfterDeadCodeRemoval_StillWorks()` | BUG-40 | offen |
| [ ] | TGAP-33 | `Rollback_WithMissingTrashFiles_ShowsIntegrityWarning()` | BUG-41 | offen |
| [ ] | TGAP-34 | `Settings_MinimizeToTray_RoundTrip()` | BUG-42 | offen |
| [ ] | TGAP-35 | `Settings_IsSimpleMode_RoundTrip()` | BUG-43 | offen |
| [ ] | TGAP-36 | `SetupVM_IsSimpleMode_DelegatesToMainVM()` | BUG-43, BUG-51 | offen |
| [ ] | TGAP-37 | `MainVM_ToolPathChange_PropagesToSetupVM(string toolProperty)` | BUG-44 | offen |
| [ ] | TGAP-38 | `ConsoleFilter_PipelineOrLabel_IsCorrect()` | BUG-45 | offen |
| [ ] | TGAP-39 | `Settings_SchedulerIntervalMinutes_RoundTrip()` | BUG-46 | offen |
| [ ] | TGAP-40 | `DashboardProjection_DryRun_ShowsPlanMarker()` | BUG-47 | offen |
| [ ] | TGAP-41 | `LibraryReportView_InvalidPath_DoesNotThrow()` | BUG-49 | offen |

---

## Bughunt #5 – CLI / API / Output-Parität

> **Scope:** CLI, API, Output-Modelle, RunOptions-Defaults, Preflight, Exit-Codes, SSE, Sidecar-Parität
> **Datum:** 2026-06
> **Methode:** Deep Code Reading aller Entry Points + field-by-field Vergleich CliDryRunOutput / ApiRunResult / RunProjection

### Executive Verdict

Die drei Entry Points (CLI, API, WPF) konvergieren architektonisch sauber auf RunOptionsFactory → RunOptionsBuilder.Normalize → RunOrchestrator. Die Projection-Ebene (RunProjectionFactory) ist vollständig geteilt. Kritische Parität ist bei den numerischen KPIs sicher. Aber: **zwei P1-Propagation-Bugs** verursachen fachlich falsche API-Ergebnisse, und die zentrale RunOptionsBuilder.Validate() ist toter Code.

### Kritische Paritätsfehler

| # | Bug | Prio | Entry Point | Impact |
|---|-----|------|-------------|--------|
| 1 | PreferRegions-Reihenfolge divergiert | P1 | API | JP/WORLD vertauscht → andere Dedupe-Ergebnisse |
| 2 | EnableDatAudit/EnableDatRename nicht propagiert | P1 | API | DAT-Audit/Rename via API unmöglich |
| 3 | RunOptionsBuilder.Validate() nie aufgerufen | P2 | Alle | WPF hat keinen OnlyGames-Guard; zentrale Validierung ist dead code |
| 4 | CLI DryRun JSON ohne PreflightWarnings | P2 | CLI | Stille Feature-Skips im DryRun |
| 5 | RunStatusDto fehlen EnableDatAudit/EnableDatRename | P2 | API | Client kann DAT-Settings nicht verifizieren |

---

### BUG-52 · PreferRegions-Reihenfolge in API divergiert von RunConstants

| Feld | Wert |
|------|------|
| **Schweregrad** | P1 — Preview/Execute Divergenz zwischen CLI/WPF und API |
| **Dateien** | `src/RomCleanup.Api/RunLifecycleManager.cs` L112, `src/RomCleanup.Contracts/RunConstants.cs` |
| **Reproduktion** | 1) POST /runs ohne `preferRegions` → API verwendet `["EU","US","WORLD","JP"]`. 2) CLI ohne `-Prefer` → verwendet `RunConstants.DefaultPreferRegions` = `["EU","US","JP","WORLD"]`. 3) Gleiche ROM-Sammlung liefert verschiedene Winner bei JP-WORLD-Tie. |
| **Erwartetes Verhalten** | Alle Entry Points verwenden `RunConstants.DefaultPreferRegions` = `["EU","US","JP","WORLD"]` als Default. |
| **Tatsächliches Verhalten** | `RunLifecycleManager.TryCreateOrReuse()` L112 hat hardcoded `new[] { "EU", "US", "WORLD", "JP" }` — JP und WORLD sind vertauscht. |
| **Ursache** | Hardcoded Array statt `RunConstants.DefaultPreferRegions`-Referenz bei API-Sonderlogik. |
| **Fix** | L112 ersetzen: `request.PreferRegions is { Length: > 0 } ? request.PreferRegions : RunConstants.DefaultPreferRegions` |
| **Testabsicherung** | TGAP-42: `Api_DefaultPreferRegions_MatchRunConstants()` |

---

### BUG-53 · EnableDatAudit und EnableDatRename werden nicht in RunRecord propagiert

| Feld | Wert |
|------|------|
| **Schweregrad** | P1 — Feature-Verlust in API |
| **Dateien** | `src/RomCleanup.Api/RunLifecycleManager.cs` L104–130 |
| **Reproduktion** | 1) POST /runs mit `{"enableDat": true, "enableDatAudit": true, "enableDatRename": true}`. 2) RunRecord hat `EnableDatAudit=false`, `EnableDatRename=false` (default). 3) RunRecordOptionsSource propagiert `false` → RunOptions hat DAT-Audit/Rename deaktiviert. 4) Fingerprint (L376-377) berücksichtigt die Flags korrekt → Idempotency-Widerspruch. |
| **Erwartetes Verhalten** | RunRecord übernimmt `request.EnableDatAudit` und `request.EnableDatRename`. |
| **Tatsächliches Verhalten** | Die Properties fehlen im RunRecord-Initializer bei `TryCreateOrReuse()`. Sie existieren in RunRequest (L198-199), RunRecord (L242-243) und RunRecordOptionsSource (L124-125), aber die Brücke in TryCreateOrReuse fehlt. |
| **Ursache** | Unvollständige Property-Übernahme bei Erweiterung des RunRequest-Modells. |
| **Fix** | In `TryCreateOrReuse()` L104-130 ergänzen: `EnableDatAudit = request.EnableDatAudit, EnableDatRename = request.EnableDatRename,` |
| **Testabsicherung** | TGAP-43: `Api_EnableDatAudit_PropagatedToRunRecord()`, `Api_EnableDatRename_PropagatedToRunRecord()` |

---

### BUG-54 · RunOptionsBuilder.Validate() nie in Produktionscode aufgerufen

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — WPF ohne OnlyGames-Guard; zentrale Validierung toter Code |
| **Dateien** | `src/RomCleanup.Infrastructure/Orchestration/RunOptionsBuilder.cs` L12-26, `src/RomCleanup.Infrastructure/Orchestration/RunOptionsFactory.cs` |
| **Reproduktion** | 1) Suche nach `RunOptionsBuilder.Validate` → 5 Treffer, alle in Tests oder Plan-Docs. 2) `RunOptionsFactory.Create()` ruft nur `Normalize()`, nicht `Validate()` auf. 3) WPF hat keine eigene OnlyGames-Validierung → `OnlyGames=false, KeepUnknown=false` kann zum Orchestrator gelangen. |
| **Erwartetes Verhalten** | `RunOptionsFactory.Create()` oder `RunOptionsBuilder.Normalize()` ruft `Validate()` auf und wirft bei Fehlern. CLI und API haben eigene Checks, WPF verlässt sich auf zentrale Validierung — die nie stattfindet. |
| **Tatsächliches Verhalten** | Zentrale Validierung ist dead code. CLI (CliArgsParser L350) und API (Program.cs L343) haben jeweils eigene, redundante Checks. WPF hat keinen. |
| **Ursache** | TASK-159 hat `Validate()` zentralisiert, aber nie in die Factory oder den Orchestrator verdrahtet. |
| **Fix** | `RunOptionsFactory.Create()` → nach Normalize() auch `Validate()` aufrufen und bei Errors eine `InvalidOperationException` werfen. CLI/API können redundante Checks behalten als defense-in-depth. |
| **Testabsicherung** | TGAP-44: `RunOptionsFactory_InvalidOptions_ThrowsFromValidate()` |

---

### BUG-55 · CLI DryRun JSON enthält keine PreflightWarnings

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — CLI-Automation erhält keine Warnung über stille Feature-Skips |
| **Dateien** | `src/RomCleanup.CLI/CliOutputWriter.cs` L213-261, `src/RomCleanup.CLI/Program.cs` L134-145 |
| **Reproduktion** | 1) `romulus -Roots "D:\Roms" -SortConsole` (DryRun default). 2) JSON-Output enthält kein `PreflightWarnings`-Feld. 3) `SortConsole` wird still übersprungen. 4) Gleicher Request via API → `PreflightWarnings: ["SortConsole is enabled but will be skipped in DryRun mode."]` |
| **Erwartetes Verhalten** | `CliDryRunOutput` enthält `PreflightWarnings`-Array wie `ApiRunResult`. |
| **Tatsächliches Verhalten** | `CliDryRunOutput` hat kein `PreflightWarnings`-Property. Der Orchestrator emittiert Warnings via `onProgress` → `SafeErrorWriteLine`, aber diese sind nur auf stderr, nicht im JSON. CI/CD-Pipelines parsen JSON, nicht stderr. |
| **Ursache** | `CliDryRunOutput` wurde ohne Warnings-Feld definiert; `RunResult.Preflight.Warnings` wird in `FormatDryRunJson` nicht ausgewertet. |
| **Fix** | 1) `CliDryRunOutput` um `string[] PreflightWarnings` ergänzen. 2) In `FormatDryRunJson` Parameter `RunResult result` ergänzen und `result.Preflight?.Warnings` mappen. |
| **Testabsicherung** | TGAP-45: `Cli_DryRunJson_IncludesPreflightWarnings()` |

---

### BUG-56 · RunStatusDto fehlen EnableDatAudit/EnableDatRename

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — API-Client kann akzeptierte DAT-Settings nicht verifizieren |
| **Dateien** | `src/RomCleanup.Api/RunManager.cs` L438-470 (RunStatusDto), L473-510 (ToDto) |
| **Reproduktion** | 1) POST /runs mit `enableDatAudit: true`. 2) GET /runs/{id} → Antwort enthält kein `enableDatAudit`-Feld. 3) Client kann nicht prüfen, ob sein Setting akzeptiert wurde. |
| **Erwartetes Verhalten** | `RunStatusDto` enthält `EnableDatAudit` und `EnableDatRename`, `ToDto()` mappt sie. |
| **Tatsächliches Verhalten** | Properties fehlen in `RunStatusDto` und `ToDto()`. Auch nach Fix von BUG-53 wären die Flags nicht im Status-DTO sichtbar. |
| **Ursache** | Unvollständige DTO-Erweiterung parallel zu RunRecord. |
| **Fix** | `RunStatusDto`: `bool EnableDatAudit` + `bool EnableDatRename` ergänzen. `ToDto()`: Mapping ergänzen. |
| **Testabsicherung** | TGAP-46: `RunStatusDto_IncludesAllRunRecordBooleanFlags()` |

---

### BUG-57 · ConvertOnly + DryRun produziert Leer-Output ohne Warnung

| Feld | Wert |
|------|------|
| **Schweregrad** | P2 — Sinnlose Option-Kombination wird still akzeptiert |
| **Dateien** | `src/RomCleanup.Infrastructure/Orchestration/RunOptionsBuilder.cs` L28-51 |
| **Reproduktion** | 1) `romulus -Roots "D:\Roms" -ConvertOnly` (DryRun default). 2) Kein Warning. ConvertOnly überspringt Dedupe, DryRun überspringt Conversion → Output zeigt 0 in allen Feldern. |
| **Erwartetes Verhalten** | `GetDryRunFeatureWarnings()` warnt: "ConvertOnly is enabled but conversion will be skipped in DryRun mode." |
| **Tatsächliches Verhalten** | `ConvertOnly` + DryRun wird nicht geprüft. Nur SortConsole, ConvertFormat und EnableDatRename werden gewarnt. |
| **Ursache** | `ConvertOnly` fehlt in der Warning-Liste. |
| **Fix** | In `GetDryRunFeatureWarnings()`: `if (options.ConvertOnly) warnings.Add("ConvertOnly is enabled but conversion will be skipped in DryRun mode. Use Mode=Move to apply.");` |
| **Testabsicherung** | TGAP-47: `DryRunWarnings_ConvertOnly_IsWarned()` |

---

### BUG-58 · API verwendet hardcoded Status-Strings statt RunConstants

| Feld | Wert |
|------|------|
| **Schweregrad** | P3 — Wartbarkeit / Konsistenzrisiko |
| **Dateien** | `src/RomCleanup.Api/RunManager.cs` L95-104 |
| **Reproduktion** | Status-Mapping in `ExecuteWithOrchestrator` verwendet `"completed"`, `"completed_with_errors"`, `"cancelled"`, `"failed"` als hardcoded Strings. `RunConstants` definiert `StatusOk="ok"`, `StatusBlocked="blocked"` etc. API remappt absichtlich (ok→completed, blocked→failed), aber ohne eigene benannte Konstanten. |
| **Erwartetes Verhalten** | API-Status-Strings sind als eigenes Konstanten-Set definiert (z.B. `ApiStatusCompleted = "completed"`). |
| **Tatsächliches Verhalten** | Magic Strings in switch-Expression. Gleiche Literale in `RunLifecycleManager.ExecuteRun()` L260-270 und SSE terminal event mapping. |
| **Ursache** | API führt eigene Status-Vokabeln ein (ok→completed, blocked→failed), aber ohne zentrale Definition. |
| **Fix** | Eigenes `ApiRunStatus`-Konstantenset im API-Projekt definieren. Alle Status-String-Literale ersetzen. |
| **Testabsicherung** | TGAP-48: `Api_StatusStrings_UseCentralConstants()` |

---

### BUG-59 · CLI DryRun JSON enthält Triple-Aliases für identische Metriken

| Feld | Wert |
|------|------|
| **Schweregrad** | P3 — API-Konsistenz / Consumer-Verwirrung |
| **Dateien** | `src/RomCleanup.CLI/CliOutputWriter.cs` L213-230 |
| **Reproduktion** | DryRun JSON enthält `Keep`, `Winners`, `Dupes`, `Losers`, `Duplicates` — wobei Keep=Winners und Dupes=Losers=Duplicates denselben Wert haben. API nutzt nur `Winners`/`Losers`. |
| **Erwartetes Verhalten** | Einheitliche Feldnamen über Entry Points. Aliase nur als dokumentierte Backward-Kompatibilität. |
| **Tatsächliches Verhalten** | 3 Aliase für 1 Metrik. Consumer, die `Dupes` verwenden, sehen andere Feldnamen als API-Consumer, die `Losers` verwenden. |
| **Ursache** | Historische Kompatibilität ohne Deprecation-Strategie. |
| **Fix** | In CliDryRunOutput die canonical Names (`Winners`, `Losers`) als Primärfeld markieren. Aliase mit `[Obsolete]` oder `[JsonPropertyName]` deprecaten. Langfristig entfernen. |
| **Testabsicherung** | TGAP-49: `Cli_DryRunJson_CanonicalFieldNames_MatchApi()` |

---

### BUG-60 · Artifact-Pfad-Divergenz: CLI collocated vs. API %APPDATA%-fixed

| Feld | Wert |
|------|------|
| **Schweregrad** | P3 — Architekturentscheidung, aber operationelles Risiko bei Service-Betrieb |
| **Dateien** | `src/RomCleanup.CLI/CliOptionsMapper.cs` L50-53, `src/RomCleanup.Api/RunLifecycleManager.cs` L235-237, `src/RomCleanup.Infrastructure/Paths/ArtifactPathResolver.cs`, `src/RomCleanup.Infrastructure/Audit/AuditSecurityPaths.cs` |
| **Reproduktion** | 1) CLI single-root: Audit landet neben ROM-Root (`D:\Roms\audits\`). 2) API: Audit landet immer in `%APPDATA%\RomCleanupRegionDedupe\audit\`. 3) Wenn API als Windows-Service unter anderem User läuft → `%APPDATA%` resolves zum Service-Account-Profil. |
| **Erwartetes Verhalten** | Einheitlicher Artifact-Lokalisierungsmechanismus oder dokumentierte Divergenz. |
| **Tatsächliches Verhalten** | CLI nutzt `ArtifactPathResolver.GetArtifactDirectory(roots)` (root-adjacent), API nutzt `AuditSecurityPaths.GetDefaultAuditDirectory()` (fixed %APPDATA%). Zwei verschiedene Audit-Pfade für identische fachliche Operationen. |
| **Ursache** | API wurde als Daemon-/Service-Modell designed (fester Pfad), CLI als User-Tool (root-relativ). |
| **Fix** | API sollte optional Root-basierte Artifact-Pfade unterstützen (via RunRequest.AuditPath). Alternativ: Divergenz dokumentieren. |
| **Testabsicherung** | TGAP-50: `Api_ArtifactPaths_DocumentedOrConfigurable()` |

---

### Korrekturnotiz zu BUG-12 (aus Bughunt #1)

BUG-12 beschreibt die API-OnlyGames-Validierung als "invertiert". Nach detaillierter Analyse ist die Logik **korrekt**:
- `!OnlyGames && !KeepUnknownWhenOnlyGames` → Error: "DropUnknown ohne GamesOnly ist semantisch ungültig"
- Der vorgeschlagene Fix `!OnlyGames && KeepUnknownWhenOnlyGames` wäre **falsch**: KeepUnknown=true ist Default → jeder Request ohne explizites OnlyGames würde rejected
- Der Check ist konsistent mit CLI (CliArgsParser L350) und RunOptionsBuilder.Validate()
- **Empfehlung:** BUG-12 als "kein Bug / false positive" markieren.

---

### Entry-Point-Divergenz-Matrix

| Aspekt | CLI | API | WPF | Konsistent? |
|--------|-----|-----|-----|-------------|
| PreferRegions Default | `RunConstants` (korrekt) | Hardcoded: JP↔WORLD vertauscht | `RunConstants` (korrekt) | **NEIN (BUG-52)** |
| EnableDatAudit propagiert | ✓ (via CliOptionsMapper) | ✗ (fehlt in TryCreateOrReuse) | ✓ (via ViewModelRunOptionsSource) | **NEIN (BUG-53)** |
| EnableDatRename propagiert | ✓ | ✗ | ✓ | **NEIN (BUG-53)** |
| OnlyGames Guard | CliArgsParser L350 | Program.cs L343 | ✗ KEINER | **NEIN (BUG-54)** |
| RunOptionsBuilder.Validate | Nicht aufgerufen | Nicht aufgerufen | Nicht aufgerufen | Toter Code |
| PreflightWarnings im Output | ✗ (nur stderr) | ✓ (ApiRunResult.PreflightWarnings) | ✓ (onProgress) | **NEIN (BUG-55)** |
| ConvertOnly+DryRun Warnung | ✗ | ✗ | ✗ | Fehlt überall (BUG-57) |
| Artifact-Pfade | Root-adjacent | %APPDATA%-fixed | Settings-basiert | Divergent (BUG-60) |
| DryRun JSON field naming | Keep/Winners, Dupes/Losers/Duplicates | Winners/Losers | N/A | Alias-Divergenz (BUG-59) |
| Status field name | `Status` | `OrchestratorStatus` | `Status` | Naming-Divergenz (BUG-58) |
| Structured Error in output | ✗ | ✓ (OperationError) | ✓ (ViewModel) | CLI-Lücke |
| PhaseMetrics | ✗ | ✓ | ✗ | API-only |
| Exit-Code-Semantik | 0/1/2/3 → documented | ExitCode im JSON | N/A | Konsistent |
| SSE Status ↔ RunRecord | — | ✓ (matching switch) | — | OK |
| Settings from %APPDATA% | ✓ (user-context) | ✗ (keine Settings geladen) | ✓ | OK (API ist self-contained) |

---

### Positiv-Befunde CLI/API (bestätigt ✓)

- [x] RunProjection als Single Source of Truth für KPIs über alle Entry Points
- [x] RunProjectionFactory.Create() zentral und kanalagnostisch
- [x] RunOptionsFactory → RunOptionsBuilder.Normalize() Pipeline identisch für CLI, API, WPF
- [x] IRunOptionsSource-Pattern sauber: 3 Implementierungen (CLI, API, WPF) ohne Schattenlogik
- [x] API Path-Traversal-Schutz: ValidatePathSecurity mit SafetyValidator, Reparse-Point-Check, Drive-Root-Block
- [x] API Rate-Limiting, API-Key-Auth mit FixedTimeEquals, Client-Binding-Isolation
- [x] SSE Event-Names sanitized gegen Injection (SanitizeSseEventName)
- [x] SSE Heartbeat gegen Proxy-Timeout (V2-H05)
- [x] SSE Terminal-Events konsistent mit RunRecord.Status
- [x] Correlation-ID Sanitization (nur printable ASCII, max 64 chars)
- [x] CLI Exit-Code-Normalisierung in dokumentierten Bereich [0-3]
- [x] CLI Ctrl+C zweistufig: grace cancel → force cancel
- [x] CLI JSONL Logging mit Rotation (JsonlLogRotation.Rotate)
- [x] API Emergency-Shutdown-Sidecar bei Timeout
- [x] Rollback-Endpoint: Default dryRun=true (Danger-Action Schutz)
- [x] Review-Queue: O(1) HashSet-Lookup für Pfad-Filter statt O(n) Contains

---

### Top 10 Fixes (priorisiert)

| # | Fix | Aufwand | Bugs |
|---|-----|---------|------|
| 1 | PreferRegions: `RunConstants.DefaultPreferRegions` in RunLifecycleManager verwenden | Klein | BUG-52 |
| 2 | EnableDatAudit/EnableDatRename in TryCreateOrReuse() propagieren | Klein | BUG-53 |
| 3 | RunOptionsFactory.Create() → Validate() nach Normalize() aufrufen | Klein | BUG-54 |
| 4 | CliDryRunOutput um PreflightWarnings erweitern | Klein | BUG-55 |
| 5 | RunStatusDto um EnableDatAudit/EnableDatRename ergänzen | Klein | BUG-56 |
| 6 | GetDryRunFeatureWarnings: ConvertOnly-Check ergänzen | Klein | BUG-57 |
| 7 | API-Status-Konstanten statt Magic Strings | Klein | BUG-58 |
| 8 | CLI DryRun Aliase deprecaten (Keep→Winners, Dupes→Losers) | Mittel | BUG-59 |
| 9 | BUG-12 als false positive schließen | Klein | BUG-12 |
| 10 | Artifact-Pfad-Divergenz dokumentieren oder konfigurierbar machen | Mittel | BUG-60 |

---

### Konsolidierte Test-Gap-Tabelle (CLI/API/Parität)

| Status | ID | Test | Bug | Prio |
|--------|-----|------|-----|------|
| [ ] | TGAP-42 | `Api_DefaultPreferRegions_MatchRunConstants()` | BUG-52 | offen |
| [ ] | TGAP-43 | `Api_EnableDatAudit_PropagatedToRunRecord()` | BUG-53 | offen |
| [ ] | TGAP-44 | `RunOptionsFactory_InvalidOptions_ThrowsFromValidate()` | BUG-54 | offen |
| [ ] | TGAP-45 | `Cli_DryRunJson_IncludesPreflightWarnings()` | BUG-55 | offen |
| [ ] | TGAP-46 | `RunStatusDto_IncludesAllRunRecordBooleanFlags()` | BUG-56 | offen |
| [ ] | TGAP-47 | `DryRunWarnings_ConvertOnly_IsWarned()` | BUG-57 | offen |
| [ ] | TGAP-48 | `Api_StatusStrings_UseCentralConstants()` | BUG-58 | offen |
| [ ] | TGAP-49 | `Cli_DryRunJson_CanonicalFieldNames_MatchApi()` | BUG-59 | offen |
| [ ] | TGAP-50 | `Api_ArtifactPaths_DocumentedOrConfigurable()` | BUG-60 | offen |

---

## Bughunt #6 – Reports / Audit / Metrics / Sidecars / Rollback / Forensik

> Scope: ReportWriter, ReportSummary, RunProjection, RunOrchestrator, Move/Junk/Sort/DatRename/Convert Phasen, AuditCsvStore, AuditSigningService, ArchiveHashing, Set-Parser
> Datum: 2026-03-29
> Methode: Deep Code Reading mit Feld-zu-Feld Vergleich zwischen Report, Projection, API, CLI und WPF

### Executive Verdict

Die Pipeline ist in den sicherheitskritischen Grundmechaniken stabil: Write-Ahead-Audit bei Moves, HMAC-Verifikation mit Constant-Time-Check, deterministische Archiv-Hash-Reihenfolge und deterministische Set-Parser.
Es bestehen jedoch mehrere Vertrauens- und Forensikprobleme in der Ergebnisdarstellung: ein Report-ErrorCounter mit Doppelzaehlung, unvollstaendige Failure-Aggregation bei Set-Member-Moves, fehlende Verify-Counter-Propagation in den RunResult-Metriken und ein Sidecar-Fehlerpfad, der still weiterlaeuft.

### Kritische Forensik- und Vertrauensprobleme

- [ ] BUG-61 (P1): ReportSummary.ErrorCount zaehlt Fehler doppelt
- [ ] BUG-62 (P2): Set-Member-Move-Fehler werden nicht in FailCount gezaehlt
- [ ] BUG-63 (P2): SavedBytes unterschlaegt Set-Member-Moves trotz MoveCount-Inkrement
- [ ] BUG-64 (P2): Sidecar-Schreibfehler werden geschluckt (null-Return), keine harte Eskalation
- [ ] BUG-65 (P2): ConvertVerifyPassed/Failed und LossyWarning bleiben in RunResult dauerhaft 0
- [ ] BUG-66 (P2): Verify-Fails beeinflussen hasErrors/HealthScore nicht
- [ ] BUG-67 (P3): AuditCsvStore.Rollback reduziert Detailergebnis auf Pfadliste (Forensikverlust)
- [ ] BUG-68 (P3): Report-Invariante wird fuer ConvertOnly/no-dedupe nicht geprueft

### Findings

#### BUG-61 - ReportSummary.ErrorCount doppelt gezaehlt

- Schweregrad: P1
- Impact: Report zeigt hoehere Fehlerzahl als API/CLI/Projection; KPI-Vertrauen bricht
- Betroffene Dateien:
  - src/RomCleanup.Infrastructure/Reporting/RunReportWriter.cs
  - src/RomCleanup.Infrastructure/Orchestration/RunProjection.cs
- Reproduktion:
  1) Run mit ConsoleSortFailed > 0 oder JunkFailCount > 0 ausfuehren.
  2) ReportSummary.ErrorCount mit RunProjection.FailCount vergleichen.
- Erwartet: ErrorCount entspricht derselben fachlichen Fehleraggregation wie FailCount.
- Tatsaechlich: ErrorCount = FailCount + JunkFailCount + ConsoleSortFailed, wobei JunkFailCount und ConsoleSortFailed bereits in FailCount enthalten sind.
- Ursache: additive Doppelaggregation in RunReportWriter.BuildSummary.
- Fix: ErrorCount = projection.FailCount setzen oder FailCount-Definition zentral als einzige Quelle verwenden.
- Testabsicherung:
  - [ ] TGAP-51: ReportSummary_ErrorCount_EqualsProjectionFailCount()

#### BUG-62 - Set-Member-Move-Fails fehlen in FailCount

- Schweregrad: P2
- Impact: Partial Failure wird unterschaetzt; Exit/Status kann zu optimistisch sein
- Betroffene Datei:
  - src/RomCleanup.Infrastructure/Orchestration/MovePipelinePhase.cs
- Reproduktion:
  1) CUE/GDI/CCD Descriptor mit mindestens einem gesperrten Member verschieben.
  2) MOVE_FAILED fuer SET_MEMBER erscheint im Audit.
  3) MovePhaseResult.FailCount pruefen.
- Erwartet: jeder fehlgeschlagene Set-Member-Move erhoeht FailCount.
- Tatsaechlich: FailCount erhoeht sich nur im Descriptor-Fehlerpfad, nicht im Set-Member-Fehlerpfad.
- Ursache: fehlendes failCount++ im else-Zweig von memberActual == null.
- Fix: Set-Member-Fehler in FailCount aggregieren.
- Testabsicherung:
  - [ ] TGAP-52: Move_SetMemberFailure_IncrementsFailCount()

#### BUG-63 - SavedBytes inkonsistent zu MoveCount bei Set-Members

- Schweregrad: P2
- Impact: SavedBytes KPI ist systematisch zu niedrig; KPI-Drift in Dashboards/Reports
- Betroffene Datei:
  - src/RomCleanup.Infrastructure/Orchestration/MovePipelinePhase.cs
- Reproduktion:
  1) Descriptor mit mehreren Set-Membern verschieben.
  2) moveCount steigt fuer Descriptor + Member.
  3) savedBytes steigt nur um loser.SizeBytes.
- Erwartet: SavedBytes beinhaltet alle erfolgreich verschobenen Dateien, die in MoveCount enthalten sind.
- Tatsaechlich: Set-Member-Groessen werden nicht addiert.
- Ursache: fehlende Byte-Aggregation im Set-Member-Success-Pfad.
- Fix: Dateigroesse der erfolgreich verschobenen Member addieren oder MoveCount semantisch auf primaries begrenzen.
- Testabsicherung:
  - [ ] TGAP-53: Move_SetMembers_AreCountedInSavedBytes()

#### BUG-64 - Sidecar-Schreibfehler laufen still weiter

- Schweregrad: P2
- Impact: Rollback-Trust sinkt; Run kann ohne gueltigen Sidecar als erfolgreich erscheinen
- Betroffene Dateien:
  - src/RomCleanup.Infrastructure/Audit/AuditSigningService.cs
  - src/RomCleanup.Infrastructure/Audit/AuditCsvStore.cs
- Reproduktion:
  1) Sidecar-Write-Fehler forcieren (z. B. Zugriffsproblem auf Zielpfad).
  2) WriteMetadataSidecar gibt null zurueck, Pipeline laeuft weiter.
- Erwartet: Sidecar-Fehler bei Move/Audit-kritischen Pfaden wird als harter Fehler propagiert.
- Tatsaechlich: Exception wird geloggt und in null umgewandelt.
- Ursache: catch-all in WriteMetadataSidecar mit return null.
- Fix: Fehler hochwerfen oder mindestens Status auf completed_with_errors erzwingen und expliziten Forensik-Fehler markieren.
- Testabsicherung:
  - [ ] TGAP-54: AuditSidecarWriteFailure_MarksRunAsError()

#### BUG-65 - Verify-Metriken werden nicht in RunResultBuilder befuellt

- Schweregrad: P2
- Impact: API/CLI/WPF zeigen ConvertVerifyPassedCount, ConvertVerifyFailedCount, ConvertLossyWarningCount faktisch immer 0
- Betroffene Dateien:
  - src/RomCleanup.Infrastructure/Orchestration/RunOrchestrator.PreviewAndPipelineHelpers.cs
  - src/RomCleanup.Infrastructure/Orchestration/RunResultBuilder.cs
- Reproduktion:
  1) Conversion mit validierbaren Erfolgen/Fehlern ausfuehren.
  2) Projection-Felder fuer Verify/Lossy pruefen.
- Erwartet: Counter aus ConversionResult-Liste berechnet und in Builder gesetzt.
- Tatsaechlich: ApplyConversionReport setzt nur ConvertReviewCount/ConvertSavedBytes/ConversionReport, aber keine Verify/Lossy-Counter.
- Ursache: unvollstaendige Aggregation in ApplyConversionReport.
- Fix: in ApplyConversionReport Counter fuer Verified/VerifyFailed/LossyWarning berechnen und auf Builder schreiben.
- Testabsicherung:
  - [ ] TGAP-55: ConversionVerifyAndLossyCounters_AreProjected()

#### BUG-66 - Verify-Fails zaehlen weder fuer hasErrors noch fuer HealthScore

- Schweregrad: P2
- Impact: fehlgeschlagene Verifikation kann als zu gesundes Ergebnis erscheinen
- Betroffene Dateien:
  - src/RomCleanup.Infrastructure/Orchestration/RunProjection.cs
  - src/RomCleanup.Infrastructure/Orchestration/RunOrchestrator.cs
- Reproduktion:
  1) Run mit VerifyFailed ohne ConvertError provozieren.
  2) hasErrors/FailCount/HealthScore beobachten.
- Erwartet: VerifyFailed beeinflusst mindestens FailCount oder separaten Error-Pfad.
- Tatsaechlich: hasErrors basiert auf ConvertErrorCount und weiteren Phasen-Fails; VerifyFailed geht nicht ein.
- Ursache: FailCount-Formel ohne ConvertVerifyFailedCount und hasErrors ohne VerifyFailed-Pruefung.
- Fix: ConvertVerifyFailedCount in FailCount und hasErrors integrieren oder separaten VerificationErrorCount mit Statuswirkung einfuehren.
- Testabsicherung:
  - [ ] TGAP-56: VerifyFailed_TriggersCompletedWithErrorsAndHealthPenalty()

#### BUG-67 - AuditCsvStore.Rollback verliert Detailergebnis

- Schweregrad: P3
- Impact: UI/CLI koennen keine differenzierte Forensik (SkippedUnsafe, Collision, Failed) ausgeben
- Betroffene Datei:
  - src/RomCleanup.Infrastructure/Audit/AuditCsvStore.cs
- Reproduktion:
  1) Rollback mit gemischten Outcomes ausfuehren.
  2) Rueckgabe von IAuditStore.Rollback pruefen.
- Erwartet: detaillierte Rueckgabe oder separater Detailzugriff.
- Tatsaechlich: nur RestoredPaths/PlannedPaths werden weitergereicht.
- Ursache: Port-Interface liefert nur Pfadliste.
- Fix: Vertragsupgrade auf AuditRollbackResult oder zusaetzliche Detail-API.
- Testabsicherung:
  - [ ] TGAP-57: RollbackApi_ExposesDetailedOutcomeCounters()

#### BUG-68 - Report-Invariante wird fuer no-dedupe/convert-only nicht geprueft

- Schweregrad: P3
- Impact: Accounting-Drift kann in ConvertOnly/Partial-Szenarien unentdeckt bleiben
- Betroffene Datei:
  - src/RomCleanup.Infrastructure/Reporting/RunReportWriter.cs
- Reproduktion:
  1) Run ohne DedupeGroups (ConvertOnly oder frueher Abbruch) erzeugen.
  2) BuildSummary-Invariant pruefen.
- Erwartet: Invariante fuer alle relevanten Runs mit TotalFiles > 0.
- Tatsaechlich: Invariant-Check nur wenn result.DedupeGroups.Count > 0.
- Ursache: zu enger Guard im Summary-Build.
- Fix: Guard auf projection.TotalFiles > 0 umstellen und ggf. statusbewusste Ausnahme fuer fruehen Cancel dokumentieren.
- Testabsicherung:
  - [ ] TGAP-58: ReportInvariant_AlsoValidatesConvertOnlyAndPartial()

### KPI- und Audit-Divergenzen

| Bereich | Projection/API/CLI | Report/WPF | Divergenz |
|---|---|---|---|
| Fehleraggregat | FailCount (zentral) | ErrorCount (eigene Formel) | Doppelzaehlung von JunkFail/ConsoleSortFailed (BUG-61) |
| Verify-Counter | Felder vorhanden | ReportSummary hat keine Verify-Felder | Sichtbarkeit fehlt im Report (Transparenzluecke) |
| Verify in Status | hasErrors ignoriert VerifyFailed | HealthScore nutzt FailCount | VerifyFailed kann ohne Statuswirkung bleiben (BUG-66) |
| Move KPI | moveCount inkl. Set-Member | savedBytes ohne Set-Member | interne KPI-Inkonsistenz (BUG-63) |
| Rollback Ergebnis | SigningService liefert Detailmodell | AuditStore-Port reduziert auf Pfadliste | Forensik-Details gehen verloren (BUG-67) |

### Top 10 Fixes (priorisiert)

| # | Fix | Aufwand | Bugs |
|---|-----|---------|------|
| 1 | ErrorCount im Report auf projection.FailCount normalisieren | Klein | BUG-61 |
| 2 | Set-Member-Fails in MovePhaseResult.FailCount aufnehmen | Klein | BUG-62 |
| 3 | Set-Member-Bytes in SavedBytes aggregieren (oder MoveCount-Semantik trennen) | Mittel | BUG-63 |
| 4 | Sidecar-Schreibfehler als Statusfehler propagieren | Mittel | BUG-64 |
| 5 | ApplyConversionReport um Verify/Lossy-Counter erweitern | Klein | BUG-65 |
| 6 | VerifyFailed in hasErrors und FailCount integrieren | Klein | BUG-66 |
| 7 | IAuditStore-Vertrag fuer detaillierte Rollback-Ergebnisse erweitern | Mittel | BUG-67 |
| 8 | Report-Invariant fuer ConvertOnly/no-dedupe aktivieren | Klein | BUG-68 |
| 9 | ReportSummary um ConvertVerifyPassed/Failed und LossyWarning erweitern | Klein | BUG-65, BUG-66 |
| 10 | KPI-Regressionstests fuer Kanal-Paritaet (Report/API/CLI/WPF) ergaenzen | Mittel | BUG-61, BUG-63, BUG-65 |

### Positiv-Befunde (Bughunt #6)

- [x] 7z/ZIP Hashing nutzt stabile Sortierung fuer deterministische Reihenfolge
- [x] CUE-Parser arbeitet deterministisch (lineares Parsing + deduplizierte Pfadliste)
- [x] hasErrors beruecksichtigt Move, JunkMove, DatRename und ConsoleSort Failures
- [x] ConsoleSorter schreibt Audit-Rows fuer Sort/Review/Junk-Routing
- [x] JunkRemoval und DatRename schreiben Audit-Rows und aggregieren Failures

---