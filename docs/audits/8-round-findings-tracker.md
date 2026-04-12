# Romulus – 8-Runden Audit Findings Tracker

> Erstellt: 2026-04-12  
> Quelle: 8 Audit-Runden (Entry Points, Safety, Data/Schema, DI, Error Handling, Hashing/Tools, Orchestration, Final Sweep + Sorting)  
> **Gesamt: 120 Findings**  
> Status-Legende: ✅ Fixed | ⚠️ Partial | ❌ Open

---

## Zusammenfassung

| Kategorie                | Gesamt | ✅ Fixed | ⚠️ Partial | ❌ Open |
|--------------------------|--------|---------|------------|--------|
| Entry Points / MVVM      | 15     | 15      | 0          | 0      |
| Safety / Security         | 10     | 8       | 1          | 1      |
| Data / Schema / Config    | 12     | 5       | 3          | 4      |
| DI / Startup              | 10     | 4       | 3          | 3      |
| Error Handling             | 9      | 2       | 3          | 4      |
| Hashing / Tools            | 13     | 6       | 3          | 4      |
| Orchestration (R7)         | 10     | 6       | 1          | 3      |
| Final Sweep (R8)           | 6      | 0       | 1          | 5      |
| Sorting / Move Pipeline    | 5      | 1       | 1          | 3      |
| **Dedup / Core Logic**     | 8      | 3       | 2          | 3      |
| **API Hardening**          | 8      | 4       | 2          | 2      |
| **Test Hygiene**           | 7      | 2       | 1          | 4      |
| **i18n / UX**              | 7      | 4       | 1          | 2      |
| **Summe**                  | **120**| **60**  | **22**     | **38** |

---

## 1. Entry Points / MVVM (15)

- [x] **EP-01** ✅ MVVM-Verstoß behoben: Safety-Filterlogik aus Code-Behind in ViewModel-Projektion verschoben  
  📁 `src/Romulus.UI.Wpf/Views/LibrarySafetyView.xaml.cs`, `src/Romulus.UI.Wpf/ViewModels/MainViewModel.RunPipeline.cs`

- [x] **EP-02** ✅ MVVM-Verstoß behoben: Report-Error-Mapping in ViewModel-Servicepfad zentralisiert  
  📁 `src/Romulus.UI.Wpf/Views/LibraryReportView.xaml.cs`, `src/Romulus.UI.Wpf/ViewModels/MainViewModel.RunPipeline.cs`

- [x] **EP-03** ✅ Hidden Coupling behoben: PropertyChanged-Watcher aus Code-Behind entfernt  
  📁 `src/Romulus.UI.Wpf/Views/LibrarySafetyView.xaml.cs`

- [x] **EP-04** ✅ Run-Result KPI Projection: `RunProjectionFactory` ist Single Source of Truth  
  📁 CLI(2x), API(1x), WPF(1x), Reporting(1x), Index(1x) nutzen alle RunProjectionFactory

- [x] **EP-05** ✅ DAT-Katalog-Scan vereinheitlicht: gemeinsame Infrastruktur-Scanlogik in DatCatalogStateService  
  📁 `src/Romulus.UI.Wpf/ViewModels/DatCatalogViewModel.cs:137` vs `src/Romulus.Api/DashboardDataBuilder.cs:110-170`

- [x] **EP-06** ✅ Error-Handling in Report-Preview gehärtet: typisierte Fallback-Logik und konsistente Warnpfade  
  📁 `src/Romulus.UI.Wpf/Views/LibraryReportView.xaml.cs:87-93` vs `src/Romulus.UI.Wpf/Services/RunService.cs:143`

- [x] **EP-07** ✅ Settings-Sync MainVM↔SetupVM: Reentrancy-Guard auf verschachtelungssichere Scope-Logik umgestellt  
  📁 `src/Romulus.UI.Wpf/ViewModels/MainViewModel.Settings.cs` (SetupSyncScope / Depth-Guard)

- [x] **EP-08** ✅ API Profile PUT: route id≠body.Id wird mit 400 validiert abgewiesen  
  📁 `src/Romulus.Api/Program.ProfileWorkflowEndpoints.cs`

- [x] **EP-09** ✅ API Rate-Limiting gehärtet: TrustForwardedFor nur bei loopback-only BindAddress aktiv  
  📁 `src/Romulus.Api/Program.cs`, `src/Romulus.Api/ProgramHelpers.cs`

- [x] **EP-10** ✅ DAT Sidecar Validation: FeatureCommandService.Dat nutzt jetzt ebenfalls strictSidecarValidation-Wiring  
  📁 `src/Romulus.UI.Wpf/Services/FeatureCommandService.Dat.cs:157,210,222`

- [x] **EP-11** ✅ FeatureCommandService nutzt DI für IFileSystem/IAuditStore; MainWindow erstellt Service nicht mehr manuell  
  📁 `src/Romulus.UI.Wpf/Services/FeatureCommandService.Collection.cs:78,99`

- [x] **EP-12** ✅ API Run-/Watch-Endpoints aus Program.cs extrahiert (Partial-Registrierung)  
  📁 `src/Romulus.Api/Program.cs:597+`

- [x] **EP-13** ✅ WebView2-Fallback: Control wird beim Fallback deterministisch disposed, Leak-Risiko entschärft  
  📁 `src/Romulus.UI.Wpf/Views/LibraryReportView.xaml.cs:98-120`

- [x] **EP-14** ✅ CLI Entry Point ist sauber: korrekte Delegation, Exit-Codes, Error-Handling  
  📁 `src/Romulus.CLI/Program.cs`

- [x] **EP-15** ✅ RunViewModel State Machine: `RunStateMachine.IsValidTransition()` korrekt enforce  
  📁 `src/Romulus.UI.Wpf/ViewModels/RunViewModel.cs:28-39`

---

## 2. Safety / Security (10)

- [x] **SEC-01** ✅ Path Traversal: SEC-PATH-01/02/03 – ADS, trailing dots, device names abgesichert  
  📁 `src/Romulus.Infrastructure/Safety/SafetyValidator.cs`

- [x] **SEC-02** ✅ Reparse Points: Blockierung auf File/Directory-Level mit Ancestry-Check  
  📁 `src/Romulus.Infrastructure/FileSystem/FileSystemAdapter.cs`

- [x] **SEC-03** ✅ ZIP-Slip: Per-Entry-Validierung mit `destPath.StartsWith()` vor Extraktion  
  📁 `src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs`

- [x] **SEC-04** ✅ ZIP-Bomb: Entry-Count (10k), Total-Size (10GB), Compression-Ratio (50x) Limits  
  📁 `src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs`

- [x] **SEC-05** ✅ Root Containment: `ResolveChildPathWithinRoot()` mit NFC-Normalisierung  
  📁 `src/Romulus.Infrastructure/FileSystem/FileSystemAdapter.cs`

- [x] **SEC-06** ✅ Collision Handling: `__DUP{n}` Suffix mit try/catch TOCTOU-Mitigation  
  📁 `src/Romulus.Infrastructure/FileSystem/FileSystemAdapter.cs:327`

- [x] **SEC-07** ✅ Trash-Operationen: Gehen via MoveItemSafely + ResolveChildPathWithinRoot  
  📁 `src/Romulus.Infrastructure/Orchestration/PipelinePhaseHelpers.cs`

- [x] **SEC-08** ✅ Profile-IDs: Regex-enforced `^[A-Za-z0-9._-]{1,64}$` zentral  
  📁 `src/Romulus.Infrastructure/Profiles/RunProfileValidator.cs`

- [ ] **SEC-09** ⚠️ File-Level TOCTOU: Akzeptiertes Risiko, dokumentiert, IOException-Fallback  
  📁 `src/Romulus.Infrastructure/FileSystem/FileSystemAdapter.cs:219-220`

- [ ] **SEC-10** ❌ Preflight Probe: `File.WriteAllText`/`File.Delete` nutzt bare File API statt IFileSystem  
  📁 `src/Romulus.Infrastructure/Orchestration/RunOrchestrator.cs:173`

---

## 3. Data / Schema / Config (12)

- [x] **DATA-01** ✅ Schemas ergänzt: format-scores.json, tool-hashes.json, ui-lookups.json  
  📁 `data/schemas/format-scores.schema.json`, `data/schemas/tool-hashes.schema.json`, `data/schemas/ui-lookups.schema.json`

- [x] **DATA-02** ✅ console-maps.json ist in der Startup-Validierung enthalten  
  📁 `src/Romulus.Infrastructure/Configuration/StartupDataSchemaValidator.cs`

- [ ] **DATA-03** ❌ builtin-profiles.json Schema-Mismatch: Daten=Array, Schema erwartet Object  
  📁 `data/builtin-profiles.json` vs `data/schemas/profiles.schema.json`

- [ ] **DATA-04** ❌ NKit-Widerspruch: `lossless=true` aber `resultIntegrity=Lossy`  
  📁 `data/conversion-registry.json:145-175`

- [ ] **DATA-05** ❌ `.rom` Extension unscored in format-scores.json aber in defaults.json gelistet  
  📁 `data/format-scores.json` (kein Eintrag) vs `data/defaults.json:3`

- [x] **DATA-06** ✅ SCAN-Region: Korrekt normalisiert → EU in RegionDetector  
  📁 `src/Romulus.Core/Regions/RegionDetector.cs:192`

- [x] **DATA-07** ✅ FR-i18n Branding korrigiert: "Romulus" statt "ROM Cleanup"  
  📁 `data/i18n/fr.json`

- [ ] **DATA-08** ⚠️ FormatScoringProfile/UiLookupData: Lazy-Singletons mit stillem Fallback auf Empty  
  📁 `src/Romulus.Infrastructure/Orchestration/FormatScoringProfile.cs:6`

- [ ] **DATA-09** ⚠️ ValidateSettingsStructure: Lehnt unbekannte Top-Level-Keys ab (kein Extensibility)  
  📁 `src/Romulus.Infrastructure/Configuration/SettingsLoader.cs:191-192`

- [ ] **DATA-10** ❌ RomulusSettings: Kein `rules`-Feld → User-Rules-Overrides werden ignoriert  
  📁 `src/Romulus.Contracts/Models/RomulusSettings.cs:9-18`

- [x] **DATA-11** ✅ StartupDataSchemaValidator erweitert (inkl. console-maps/format-scores/tool-hashes/ui-lookups)  
  📁 `src/Romulus.Infrastructure/Configuration/StartupDataSchemaValidator.cs`

- [ ] **DATA-12** ❌ i18n-Fallback ist "de" statt "en" – Französisch fällt auf Deutsch zurück  
  📁 `src/Romulus.UI.Wpf/Services/LocalizationService.cs:63-69`

---

## 4. DI / Startup (10)

- [ ] **DI-01** ❌ CLI: Kein DI-Container, alles manuell `new`  
  📁 `src/Romulus.CLI/Program.cs:246,368-723`

- [ ] **DI-02** ❌ API Rollback-Endpoint: `new AuditSigningService(...)` umgeht DI  
  📁 `src/Romulus.Api/Program.cs:488`

- [ ] **DI-03** ⚠️ API ProgramHelpers: Kein direkter RunEnvironmentFactory-Bypass gefunden  
  📁 `src/Romulus.Api/ProgramHelpers.cs` (nur Utility-Methoden)

- [ ] **DI-04** ⚠️ WPF RunService: Constructor-Fallback zu `new RunEnvironmentFactory()` (kommentiert als DI-BYPASS-JUSTIFIED)  
  📁 `src/Romulus.UI.Wpf/Services/RunService.cs:35-43`

- [ ] **DI-05** ❌ WPF FeatureCommandService.Collection: `new FileSystemAdapter()`, `new AuditCsvStore()`  
  📁 `src/Romulus.UI.Wpf/Services/FeatureCommandService.Collection.cs:78,99`

- [x] **DI-06** ✅ PersistedReviewDecisionService wird beim API-Shutdown explizit disposed  
  📁 `src/Romulus.Api/Program.cs`

- [x] **DI-07** ✅ ICollectionIndex wird beim API-Shutdown explizit disposed  
  📁 `src/Romulus.Api/Program.cs`

- [x] **DI-08** ✅ IReviewDecisionStore wird beim API-Shutdown explizit disposed  
  📁 `src/Romulus.Api/Program.cs`

- [x] **DI-09** ✅ ApiAutomationService wird beim API-Shutdown explizit disposed  
  📁 `src/Romulus.Api/Program.cs`, `src/Romulus.Api/ApiAutomationService.cs`

- [ ] **DI-10** ⚠️ API RunManager: Nicht IDisposable, besitzt aber RunLifecycleManager  
  📁 `src/Romulus.Api/RunManager.cs:18,42`

---

## 5. Error Handling (9)

- [ ] **ERR-01** ⚠️ Bare `catch {}` in Produktionscode (AppStateStore mit Kommentar)  
  📁 `src/Romulus.Infrastructure/State/AppStateStore.cs:139`

- [ ] **ERR-02** ❌ Silent Exception Swallowing in AppStateStore  
  📁 `src/Romulus.Infrastructure/State/AppStateStore.cs:138-139`

- [ ] **ERR-03** ⚠️ Fire-and-forget Task.Run in MainViewModel (hat try/catch, aber swallows)  
  📁 `src/Romulus.UI.Wpf/ViewModels/MainViewModel.cs:230`

- [ ] **ERR-04** ⚠️ Kein ILogger/Microsoft.Extensions.Logging – custom Action\<string\> Logging  
  📁 Projekt-weit: kein `ILogger<T>` in Infrastructure

- [ ] **ERR-05** ❌ Kein Retry/Circuit-Breaker für externe HTTP/Tool-Calls  
  📁 `src/Romulus.Infrastructure/Dat/DatSourceService.cs`, `ToolRunnerAdapter.cs`

- [x] **ERR-06** ✅ API: Global Exception Middleware vorhanden  
  📁 `src/Romulus.Api/Program.cs:62-80` (`UseExceptionHandler`)

- [ ] **ERR-07** ⚠️ API: Custom Error Format statt RFC 7807 Problem Details  
  📁 `src/Romulus.Api/Program.cs` (OperationErrorResponse)

- [ ] **ERR-08** ⚠️ Sync-over-Async weitgehend reduziert, aber ein synchroner CLI-Compat-Wrapper (`awaiter.GetResult()`) verbleibt  
  📁 `src/Romulus.CLI/Program.cs:165`

- [x] **ERR-09** ✅ Async void: Nur in WPF Event-Handlers (akzeptabel)  
  📁 `MainWindow.xaml.cs:146,247`, `LibraryReportView.xaml.cs:20`

---

## 6. Hashing / Tools (13)

- [x] **TH-01** ✅ N64-Header-Repair auf stream-basiertes I/O umgestellt (kein `ReadAllBytes`)  
  📁 `src/Romulus.Infrastructure/Hashing/HeaderRepairService.cs`

- [ ] **TH-02** ⚠️ Hash-Format: Inconsistenz `Convert.ToHexString` vs `ToHexStringLower`  
  📁 `FileHashService.cs:216` vs `ParallelHasher.cs:28-31`

- [x] **TH-03** ✅ FixedTimeHashEquals: `CryptographicOperations.FixedTimeEquals` korrekt  
  📁 `src/Romulus.Infrastructure/Conversion/ToolInvokerSupport.cs:95-100`

- [ ] **TH-04** ❌ TOCTOU in `ToolRunnerAdapter.VerifyToolHash` (File-Swap zwischen Check und Open)  
  📁 `src/Romulus.Infrastructure/Tools/ToolRunnerAdapter.cs:673-693`

- [x] **TH-05** ✅ ChdmanToolConverter: Alle InvokeProcess-Calls nutzen ToolRequirement  
  📁 `src/Romulus.Infrastructure/Conversion/ChdmanToolConverter.cs:64,87,121,185,239`

- [ ] **TH-06** ⚠️ PENDING-VERIFY Marker entfernt und ECM-Capability deaktiviert, aber verifizierte produktive Hash-Werte fuer unecm/flips/xdelta stehen noch aus  
  📁 `data/tool-hashes.json`, `data/conversion-registry.json`

- [ ] **TH-07** ⚠️ Invoke7z: `VerifyToolHash(path, requirement: null)` – kein spezifisches Requirement  
  📁 `src/Romulus.Infrastructure/Tools/ToolRunnerAdapter.cs:296-305`

- [x] **TH-08** ✅ ArchiveHashService: Cache-Invalidierung via LastWriteTimeUtc + Length  
  📁 `src/Romulus.Infrastructure/Hashing/ArchiveHashService.cs:93-95`

- [x] **TH-09** ✅ FileHashService: `_persistentEntries` unter `lock (_persistentGate)` gesichert  
  📁 `src/Romulus.Infrastructure/Hashing/FileHashService.cs:358-360`

- [x] **TH-10** ✅ SNES-Header-Repair auf stream-basiertes I/O umgestellt (kein `ReadAllBytes`)  
  📁 `src/Romulus.Infrastructure/Hashing/HeaderRepairService.cs`

- [ ] **TH-11** ❌ ToolRunnerAdapter: Doppelte Hash-Prüfung (ToolInvokerSupport + VerifyToolHash)  
  📁 `ToolInvokerSupport.cs:15-17` + `ToolRunnerAdapter.cs:663`

- [ ] **TH-12** ❌ EcmInvoker/NkitInvoker: Verify nur FileExists + Length>0  
  📁 `src/Romulus.Infrastructure/Conversion/EcmInvoker.cs:56-65`, `NkitInvoker.cs:73-82`

- [ ] **TH-13** ❌ Crc32: Kein CancellationToken – nicht unterbrechbar  
  📁 `src/Romulus.Infrastructure/Hashing/Crc32.cs:31-47`

---

## 7. Orchestration (10)

- [x] **ORC-01** ✅ Trigger-Fehlerpfad beobachtet: Faulted-Tasks werden abgefangen und statusseitig hinterlegt  
  📁 `src/Romulus.Api/ApiAutomationService.cs`

- [ ] **ORC-02** ❌ WatchFolderService: Keine Wiederherstellung bei gelöschten Verzeichnissen  
  📁 `src/Romulus.Infrastructure/Watch/WatchFolderService.cs:170`

- [x] **ORC-03** ✅ ExecutePhasePlan: Outer catch-all fängt Phase-Exceptions  
  📁 `src/Romulus.Infrastructure/Orchestration/RunOrchestrator.cs:279`

- [x] **ORC-04** ✅ PipelineState: Double-Assign InvalidOperationException wird vom Outer-Handler gefangen  
  📁 `src/Romulus.Infrastructure/Orchestration/PhasePlanning.cs:44`

- [x] **ORC-05** ✅ RunRecord: `_approvedReviewPaths` HashSet unter `lock (_lock)` gesichert  
  📁 `src/Romulus.Api/RunManager.cs:340,348,358`

- [ ] **ORC-06** ⚠️ PhaseMetricsCollector: Auto-Complete kann Phase-Zeit falsch zuordnen  
  📁 `src/Romulus.Infrastructure/Metrics/PhaseMetricsCollector.cs:47`

- [ ] **ORC-07** ❌ EvictOldRuns: Kann Run evicten während WaitForCompletion-Watcher noch referenziert  
  📁 `src/Romulus.Api/RunLifecycleManager.cs:346-360`

- [ ] **ORC-08** ❌ Preflight Probe: Bare `File.WriteAllText`/`File.Delete` statt IFileSystem  
  📁 `src/Romulus.Infrastructure/Orchestration/RunOrchestrator.cs:173`

- [x] **ORC-09** ✅ DedupePhase: 0 Candidates → leere Groups, CompletePhase(0) sauber  
  📁 `src/Romulus.Infrastructure/Orchestration/DeduplicatePipelinePhase.cs:24-26`

- [x] **ORC-10** ✅ Disposed-CancellationSource liefert weiterhin cancelbaren/cancelled Tokenzustand  
  📁 `src/Romulus.Api/RunManager.cs`

---

## 8. Final Sweep – R8 (6)

- [ ] **FIN-01** ❌ DatCatalogState: Case-Insensitive Comparer geht bei JSON-Roundtrip verloren  
  📁 `src/Romulus.Infrastructure/Dat/DatCatalogStateService.cs` (Deserialize)

- [ ] **FIN-02** ⚠️ OperationResult: Mutable Collections in Contracts (intentional, dokumentiert)  
  📁 `src/Romulus.Contracts/Models/OperationResult.cs:52-56`

- [ ] **FIN-03** ❌ QuarantineModels: Vollständig mutable (`{ get; set; }`)  
  📁 `src/Romulus.Contracts/Models/QuarantineModels.cs:3-58`

- [ ] **FIN-04** ❌ BenchmarkFixture: Sync I/O unter Lock in IAsyncLifetime  
  📁 `src/Romulus.Tests/BenchmarkFixture.cs:24-49`

- [ ] **FIN-05** ❌ xunit maxParallelThreads=-1: Verschärft bekannten Parity-Flake  
  📁 `src/Romulus.Tests/xunit.runner.json:2`

- [ ] **FIN-06** ❌ JSON-Output: Default-Escaping für Non-ASCII (keine UnsafeRelaxedJsonEscaping)  
  📁 Projekt-weit: `JsonSerializerOptions` ohne Encoder-Override

---

## 9. Sorting / Move Pipeline (5)

- [x] **SORT-01** ✅ DryRun Set-Path validiert vollständige Auflösung vor Move-Zählung (Preview≈Execute)  
  📁 `src/Romulus.Infrastructure/Sorting/ConsoleSorter.cs`

- [ ] **SORT-02** ❌ M3U-Content: Wird nach Set-Moves nicht umgeschrieben  
  📁 `src/Romulus.Infrastructure/Sorting/ConsoleSorter.cs` (kein M3U-Rewrite)

- [ ] **SORT-03** ⚠️ Set-Membership: Überlappende Sets nur per Extension-Lookup (simplistisch)  
  📁 `src/Romulus.Infrastructure/Sorting/ConsoleSorter.cs`

- [ ] **SORT-04** ❌ AuditCsvStore: Statische Lock-Dictionary wächst unbegrenzt  
  📁 `src/Romulus.Infrastructure/Audit/AuditCsvStore.cs:14`

- [ ] **SORT-05** ❌ ArchiveHashService: ZIP SHA1+CRC32 gemischt im Hash-Output  
  📁 `src/Romulus.Infrastructure/Hashing/ArchiveHashService.cs:191-199`

---

## 10. Dedup / Core Logic (8)

- [x] **CORE-01** ✅ GameKeyNormalizer: Deterministische Normalisierung mit Disc-Padding  
  📁 `src/Romulus.Core/GameKeys/GameKeyNormalizer.cs`

- [ ] **CORE-02** ❌ DeduplicationEngine: Unbekannter Console-Key Fallback/Grouping nicht klar definiert  
  📁 `src/Romulus.Core/Deduplication/DeduplicationEngine.cs`

- [x] **CORE-03** ✅ FormatScore: Deterministisch via FormatScorer mit registriertem Profil  
  📁 `src/Romulus.Core/Scoring/FormatScorer.cs`

- [ ] **CORE-04** ❌ DeduplicatePipelinePhase: Game-Group Filter (`Winner.Game || any Loser.Game`) – filtert Non-Game Gruppen  
  📁 `src/Romulus.Infrastructure/Orchestration/DeduplicatePipelinePhase.cs`

- [x] **CORE-05** ✅ Winner-Selection: Deterministisch durch Score-Kette + Tiebreaker  
  📁 `src/Romulus.Core/Deduplication/DeduplicationEngine.cs`

- [ ] **CORE-06** ⚠️ ClassificationIoResolver: I/O-Logik in Core (Architekturverstoß – bereits mit Interface gemildert)  
  📁 `src/Romulus.Core/Classification/ClassificationIoResolver.cs`

- [ ] **CORE-07** ⚠️ SetParserIoResolver: I/O-Logik in Core (Architekturverstoß – bereits mit Interface gemildert)  
  📁 `src/Romulus.Core/SetParsing/SetParserIoResolver.cs`

- [ ] **CORE-08** ❌ Disc-Padding Regression: Kein expliziter Test für "disc 001" vs "disc 1" Normalisierung  
  📁 `src/Romulus.Tests/` (fehlende Regression)

---

## 11. API Hardening (8)

- [x] **API-01** ✅ Global Exception Handler: `UseExceptionHandler` vorhanden  
  📁 `src/Romulus.Api/Program.cs:62-80`

- [ ] **API-02** ❌ Einige Endpoint-Pfade leaken Exception-Messages in Response  
  📁 `src/Romulus.Api/Program.cs` (catch-Blöcke mit `ex.Message` in Response)

- [x] **API-03** ✅ Path Security: `ValidatePathSecurity()` mit AllowedRoots für jeden Input  
  📁 `src/Romulus.Api/ProgramHelpers.cs`

- [x] **API-04** ✅ API-Key Timing-Safe: FixedTimeEquals für Auth  
  📁 `src/Romulus.Api/Program.cs`

- [ ] **API-05** ❌ Static Files: `wwwroot/` wird ausgeliefert (Angriffsfläche nicht minimiert)  
  📁 `src/Romulus.Api/wwwroot/`

- [ ] **API-06** ⚠️ RunRequest Case-Sensitivity: `HasProperty` jetzt case-insensitive (Fixed), aber Explicitness-Logik bleibt komplex  
  📁 `src/Romulus.Api/ApiRunConfigurationMapper.cs:103`

- [ ] **API-07** ⚠️ DashboardDataBuilder: Parallele Scan-Logik statt DatCatalogStateService  
  📁 `src/Romulus.Api/DashboardDataBuilder.cs:110-170`

- [x] **API-08** ✅ Run-Lifecycle Tokenhandling bleibt cancelbar auch nach Disposal-Pfad  
  📁 `src/Romulus.Api/RunManager.cs`

---

## 12. Test Hygiene (7)

- [x] **TEST-01** ✅ PhaseMetricsCollectorTests: Echte Assertions vorhanden  
  📁 `src/Romulus.Tests/PhaseMetricsCollectorTests.cs`

- [x] **TEST-02** ✅ Sicherheits-Tests: SafetyIoRecoveryTests, ApiSecurityTests umfangreich  
  📁 `src/Romulus.Tests/`

- [ ] **TEST-03** ❌ BenchmarkFixture: Sync I/O unter Lock → potentieller Deadlock in Test-Infra  
  📁 `src/Romulus.Tests/BenchmarkFixture.cs:24-49`

- [ ] **TEST-04** ❌ xunit Parallelisierung: maxParallelThreads=-1 verschärft Flakes  
  📁 `src/Romulus.Tests/xunit.runner.json`

- [ ] **TEST-05** ❌ Parity-Test Flake: `HardCoreInvariantRegressionSuiteTests.Parity_GuiCliApi` intermittierend  
  📁 `src/Romulus.Tests/HardCoreInvariantRegressionSuiteTests.cs:1067`

- [ ] **TEST-06** ⚠️ Große Test-Dateien: GuiViewModelTests.cs, ApiIntegrationTests.cs > 3000 Zeilen  
  📁 `src/Romulus.Tests/`

- [ ] **TEST-07** ❌ Fehlender Disc-Padding Regressionstest  
  📁 `src/Romulus.Tests/` (kein Test für "disc 001" vs "disc 1")

---

## 13. i18n / UX (7)

- [x] **I18N-01** ✅ FR: Produktname auf "Romulus" korrigiert  
  📁 `data/i18n/fr.json`

- [ ] **I18N-02** ❌ i18n-Fallback: Deutsch statt Englisch als Base (Policy-Entscheidung)  
  📁 `src/Romulus.UI.Wpf/Services/LocalizationService.cs:63`

- [x] **I18N-03** ✅ FR-Set strukturell vollständig (Key-Parität de/fr), verbleibende identische Werte deutlich reduziert  
  📁 `data/i18n/fr.json`, `data/i18n/de.json`

- [x] **I18N-04** ✅ Hardcodierte Converter-Strings ersetzt durch i18n-Keys (`Run.PhaseDetail.*`, `Run.PhaseStatus.*`)  
  📁 `src/Romulus.UI.Wpf/Converters/Converters.cs`

- [x] **I18N-05** ✅ Locale-Switch Runtime: `INotifyPropertyChanged` + Item[]-Refresh korrekt  
  📁 `src/Romulus.UI.Wpf/Services/LocalizationService.cs:44-46`

- [ ] **I18N-06** ⚠️ Theme/Defaults: Fest auf "de"/"dark" – kein System-Detection  
  📁 `data/defaults.json:19-20`, `src/Romulus.Contracts/Models/RomulusSettings.cs`

- [ ] **I18N-07** ❌ JSON: Non-ASCII ROM-Namen mit `\uXXXX`-Escaping (kein UnsafeRelaxedJsonEscaping)  
  📁 Projekt-weit: `JsonSerializerOptions`

---

## Nächste Schritte (Prioritätsreihenfolge)

### Priorität 1 – Release-Blocker
- [x] **1. EP-01/02/03**: MVVM Code-Behind Business-Logik → ViewModel verschieben
- [x] **2. SORT-01**: DryRun/Execute Set-Divergenz → Preview-Parität sichern
- [x] **3. ORC-10/API-08**: Disposed CancellationToken → sauberes Lifecycle-Management
- [x] **4. EP-08**: API Profile PUT route≠body ID → explizite Validierung
- [ ] **5. API-02**: Exception Leak in API Responses → Messages sanitizen

### Priorität 2 – Hohe Risiken
- [x] **6. DI-06/07/08/09**: IDisposable Singletons → Explicit Dispose at Shutdown
- [ ] **7. ERR-08**: Sync-over-Async → async-Kette durchziehen
- [x] **8. TH-01/10**: File.ReadAllBytes OOM → Stream-basiertes Lesen
- [ ] **9. TH-06**: tool-hashes.json PENDING → echte Hashes eintragen oder Tools entfernen
- [x] **10. ORC-01**: Fire-and-Forget → Exceptions loggen

### Priorität 3 – Wartbarkeit
- [x] **11. DATA-01/02/11**: Fehlende Schemas und Startup-Validierung erweitern
- [x] **12. I18N-01/03/04**: FR-Übersetzungen vervollständigen, Converter-Strings lokalisieren
- [ ] **13. DI-01**: CLI DI-Container einführen (größeres Refactoring)
- [ ] **14. EP-12**: API Program.cs Endpoints extrahieren
- [ ] **15. TEST-04/05**: xunit Parallelisierung tunen, Parity-Flake stabilisieren
