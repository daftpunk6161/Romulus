# ROM Cleanup – Test-Strategie

**Datum:** 2026-03-10  
**Grundsatz:** Kein Alibi-Test. Jeder Test hat eine **Failure-First-Anforderung** – er muss ohne den zu testenden Code rot werden.

---

## 1. Test-Pyramide

```
        ┌──────────────────┐
        │   E2E (GUI-Live) │  ~1 E2E-Testdatei – echte Dateisystem-Ops
        ├──────────────────┤
        │ Integration      │  ~2 Integration-Testdateien – mehrere Module zusammen
        ├──────────────────┤
        │ Unit             │  ~183 Testdateien (137 unit/ + 46 Root-Level)
        └──────────────────┘
        Gesamt: ~1300+ Tests (Passed: ~1307, Skipped: ~3)
```

---

## 2. Testdateien-Übersicht

### 2.1 Core-/Engine-Tests (Root-Level `dev/tests/`)

| Datei | Stage | Zweck |
|---|---|---|
| `RomCleanup.Tests.ps1` | unit | Hauptintegrations-Smoke (~3174 Zeilen) |
| `Core.Tests.ps1` | unit | GameKey-Generierung, Region-Scoring |
| `Dedupe.Tests.ps1` | unit | Region-Dedupe-Logik |
| `Dedupe.Coverage.Tests.ps1` | unit | Erweiterte Dedupe-Coverage |
| `Classification.Tests.ps1` | unit | Konsolen-Erkennung |
| `Convert.Tests.ps1` | unit | Format-Konvertierung Mocks |
| `Convert.Strategy.Tests.ps1` | unit | Konvertierungs-Strategie-Map |
| `Convert.Coverage.Tests.ps1` | unit | Erweiterte Convert-Coverage |
| `Dat.Tests.ps1` | unit | DAT-XML- und CLRmamePro-Parser |
| `DatSources.Tests.ps1` | unit | DAT-Download/Install |
| `FormatScoring.Tests.ps1` | unit | Prioritäts-Score-Berechnung |
| `SetParsing.Tests.ps1` | unit | CUE/GDI/M3U-Parser |
| `FileOps.Tests.ps1` | unit | Move/Link-Operationen |
| `Tools.Tests.ps1` | unit | Werkzeug-Wrappers, Hash-Prüfung |
| `Report.Tests.ps1` | unit | HTML/CSV-Reportgenerierung |
| `Settings.BugFinder.Tests.ps1` | unit | Settings-Schema-Validierung |
| `Modules.Tests.ps1` | unit | Modulexistenz + Parse-Check |
| `ZipSort.Tests.ps1` | unit | ZIP-Sortierlogik |

### 2.2 Architektur-/Governance-Tests (Root-Level)

| Datei | Stage | Zweck |
|---|---|---|
| `PortInterfaces.Unit.Tests.ps1` | unit | Port-Contract-Validierung |
| `OperationAdapters.Ports.Tests.ps1` | unit | Adapter-Port-Integration |
| `ErrorContracts.Tests.ps1` | unit | Fehlerklassen/Contracts |
| `EventBus.Tests.ps1` | unit | Pub/Sub-Events |
| `PluginContractValidation.Tests.ps1` | unit | Plugin-Manifest-Validierung |
| `OperationPlugins.Tests.ps1` | unit | Operation-Plugin-Lifecycle |
| `Security.Tests.ps1` | unit | Sicherheitsregeln |
| `Preflight.Tests.ps1` | unit | Preflight-Checks |
| `Startup.Tests.ps1` | unit | Modul-Startup-Reihenfolge |
| `ApiServer.Unit.Tests.ps1` | unit | API-Server-Logik |
| `Api.OpenApiDrift.Tests.ps1` | unit | OpenAPI-Spec-Drift-Erkennung |
| `LruCache.Tests.ps1` | unit | LRU-Cache-Logik |
| `Benchmark.Tests.ps1` | unit | Performance-Baseline |

### 2.3 GUI-/WPF-Tests (Root-Level)

| Datei | Stage | Zweck |
|---|---|---|
| `WpfEventHandlers.Coverage.Tests.ps1` | unit | WPF-Event-Handler-Coverage |
| `UiSmoke.Tests.ps1` | unit | UI-Smoke-Tests |
| `ConsoleDetection.Tests.ps1` | unit | Konsolen-Erkennungs-Pipeline |

### 2.4 Regressions- & Bug-Tests (Root-Level)

| Datei | Stage | Zweck |
|---|---|---|
| `BugRegression.Tests.ps1` | unit | Bug-Regressions Batch 1 |
| `BugRegression2.Tests.ps1` | unit | Bug-Regressions Batch 2 |
| `RuleRegressionPack.Tests.ps1` | unit | Regel-Regressions |
| `FaultInjection.Tests.ps1` | unit | Fehler-Injektion |
| `GameKey.Tests.ps1` | unit | GameKey-Konsistenz |
| `GameKey.Fuzz.Tests.ps1` | unit | GameKey-Fuzzing |
| `RegionDedupe.Tests.ps1` | unit | Region-Dedupe-Regression |
| `OneGameOneRom.Tests.ps1` | unit | 1G1R-Logik |
| `Phase2Smoke.Tests.ps1` | unit | Phase-2-Smoke |
| `Phase3Smoke.Tests.ps1` | unit | Phase-3-Smoke |
| `ParallelConvert.Tests.ps1` | unit | Parallele Konvertierung |

### 2.5 Unit-Tests (`dev/tests/unit/`) — 137 Dateien

Alle 76 Feature-Module haben eigene Tests:

| Kategorie | Testdateien (Beispiele) |
|---|---|
| **Quick Wins (QW-01–QW-16)** | `DatRename.Tests.ps1`, `EcmDecompress.Tests.ps1`, `ArchiveRepack.Tests.ps1`, `ConversionEstimate.Tests.ps1`, `JunkReport.Tests.ps1`, `KeyboardShortcuts.Tests.ps1`, `DuplicateHeatmap.Tests.ps1`, `CliExport.Tests.ps1`, `PortableMode.Tests.ps1`, `CollectionCsvExport.Tests.ps1`, `M3uGenerator.Tests.ps1`, `RetroArchPlaylist.Tests.ps1` |
| **Medium Features (MF-01–MF-26)** | `MissingRomTracker.Tests.ps1`, `CrossRootDedupe.Tests.ps1`, `HeaderAnalysis.Tests.ps1`, `CompletenessTracker.Tests.ps1`, `CollectionManager.Tests.ps1`, `ConversionPipeline.Tests.ps1`, `NKitConvert.Tests.ps1`, `ConvertQueue.Tests.ps1`, `ConversionVerify.Tests.ps1`, `FormatPriority.Tests.ps1`, `DatAutoUpdate.Tests.ps1`, `DatDiffViewer.Tests.ps1`, `TosecDatSupport.Tests.ps1`, `ParallelHashing.Tests.ps1`, `CommandPalette.Tests.ps1`, `FilterBuilder.Tests.ps1`, `RuleEngine.Tests.ps1`, `PipelineEngine.Tests.ps1`, `DryRunCompare.Tests.ps1`, `SortTemplates.Tests.ps1`, `IntegrityMonitor.Tests.ps1`, `BackupManager.Tests.ps1`, `Quarantine.Tests.ps1` |
| **Large Features (LF-01–LF-20)** | `CoverScraper.Tests.ps1`, `GenreClassification.Tests.ps1`, `LauncherIntegration.Tests.ps1`, `PlaytimeTracker.Tests.ps1`, `PatchEngine.Tests.ps1`, `HeaderRepair.Tests.ps1`, `ArcadeMergeSplit.Tests.ps1`, `StorageTiering.Tests.ps1`, `CustomDatEditor.Tests.ps1`, `CloneListViewer.Tests.ps1`, `HashDatabaseExport.Tests.ps1`, `Accessibility.Tests.ps1`, `PdfReportExport.Tests.ps1`, `NasOptimization.Tests.ps1`, `FtpSource.Tests.ps1`, `CloudSettingsSync.Tests.ps1`, `PluginMarketplace.Tests.ps1`, `RulePackSharing.Tests.ps1`, `ThemeEngine.Tests.ps1` |
| **XL Features (XL-01–XL-14)** | `DockerContainer.Tests.ps1`, `MobileWebUI.Tests.ps1`, `WindowsContextMenu.Tests.ps1`, `PSGalleryModule.Tests.ps1`, `PackageManagerIntegration.Tests.ps1`, `TrendAnalysis.Tests.ps1`, `EmulatorCompatReport.Tests.ps1`, `CollectionSharing.Tests.ps1`, `GpuHashing.Tests.ps1`, `UsnJournalScan.Tests.ps1`, `HardlinkMode.Tests.ps1`, `ToolImport.Tests.ps1`, `MultiInstanceSync.Tests.ps1`, `Telemetry.Tests.ps1` |
| **Infrastruktur & Architektur** | `BackgroundOps.Tests.ps1`, `MemoryGuard.Tests.ps1`, `PhaseMetrics.Tests.ps1`, `LruCache.Perf.Tests.ps1`, `CatchGuard.Tests.ps1`, `Governance.Tests.ps1`, `ModuleDependencyBoundary.Tests.ps1`, `ArchitectureMap.Sync.Tests.ps1`, `PortContractValidation.Tests.ps1`, `AppStore.Configuration.Tests.ps1`, `ConfigProfiles.Tests.ps1`, `ConfigMerge.Tests.ps1` |
| **Konsolen-/Erkennung** | `ConsoleDetection.Determinism.Tests.ps1`, `ConsoleDetection.Fuzz.Tests.ps1`, `ConsoleDetection.FolderCache.Tests.ps1`, `ConsoleDetection.DolphinGcWii.Tests.ps1`, `ConsoleDetection.PipelineCoverage.Tests.ps1`, `ConsoleSort.Core.Tests.ps1`, `ConsoleSort.UnknownReasons.Tests.ps1`, `ConsolesJsonConsistency.Tests.ps1`, `ConsoleTypeSource.Tests.ps1` |
| **Bug-Fixes** | `BugFix.Batch1.Tests.ps1`, `BugFix.Batch2.Tests.ps1`, `BugFix.Batch3.Tests.ps1`, `BugFix.Batch4.Tests.ps1` |
| **Edge Cases & Archiv** | `EdgeCases.Tests.ps1`, `NegativeTests.Tests.ps1`, `ArchiveSecurity.Tests.ps1`, `ArchiveRepack.Tests.ps1`, `ArchiveMixedContent.Tests.ps1`, `ArchiveDiscSet.Tests.ps1` |
| **DAT & Hash** | `Dat.IndexCache.Tests.ps1`, `Dat.BomStrip.Tests.ps1`, `ChdHeaderCache.Tests.ps1`, `Determinism.Tests.ps1` |
| **WPF/ISS-001** | `WpfWizard.Tests.ps1`, `WpfTrashSettings.Tests.ps1` (39 Wizard-Tests) |

### 2.6 Integration-Tests (`dev/tests/integration/`)

| Datei | Zweck |
|---|---|
| `WpfSmoke.Tests.ps1` | WPF-Fenster instanziierbar, XAML-Parse, Control-Binding |
| `PluginIntegration.Tests.ps1` | Plugin-Discovery, Trust-Modus, Lifecycle |

### 2.7 E2E-Tests (`dev/tests/e2e/`)

| Datei | Zweck |
|---|---|
| `E2E.Tests.ps1` | Vollständiger Dedupe-Durchlauf mit Fixtures |

---

## 3. Test-Konventionen

### 3.1 Modul-Loading

Jeder Test-File lädt seine Abhängigkeiten explizit via Dot-Source:

```powershell
BeforeAll {
    $root = $PSScriptRoot
    while ($root -and -not (Test-Path (Join-Path $root 'simple_sort.ps1'))) {
        $root = Split-Path -Parent $root
    }
    . (Join-Path $root 'dev\modules\Settings.ps1')
    . (Join-Path $root 'dev\modules\ZuTestendesModul.ps1')
}
```

**Warum:** Kein globaler Import-Module-Overhead. Tests sind isoliert. Reihenfolge ist deterministisch.

### 3.2 Naming

```
<Modul>.<Thema>.Tests.ps1       # Unit
<Feature>.Tests.ps1             # Integration
<Feature>.E2E.Tests.ps1         # E2E
```

### 3.3 Skipped-Tests statt Lügen

Wenn ein Test auf nicht verfügbare Infrastruktur trifft (STA-Thread, echte Dateipfade):

```powershell
if (-not $script:isSta) {
    Set-ItResult -Skipped -Because 'STA-Thread erforderlich'
    return
}
```

**Verboten:** `Should -BeTrue` auf `$true` (Alibi-Test), `try/catch` das immer grün macht.

### 3.4 Mock-Strategie

| Situation | Empfohlen |
|---|---|
| Externe Tools (chdman, 7z) | `Mock` via Pester 5 oder Dummy-Wrapper-Skripte in `dev/tests/fixtures/` |
| Dateisystem | `New-TemporaryFile` / `New-Item -TempDir` + cleanup in `AfterAll` |
| `$Log`-Callback | `$captured = [List]::new()` + `{ param($msg) $captured.Add($msg) }` |
| GUI-Controls | Mock-`$ctx`: `@{ btnRunGlobal = New-Object AnyMockType }` im Test |

### 3.5 Benchmark-Gate (`tests: benchmark gate`)

Der separate CI-Stage prüft Leistungsregression via `CacheBenchmark.Tests.ps1` + `LruCache.Perf.Tests.ps1`. **Beides muss bestehen bevor ein Merge erfolgt.**

---

## 4. Coverage-Ziel

| Modul | Minimal-Coverage |
|---|---|
| `Tools.ps1` | 70% |
| `Dedupe.ps1` | 65% |
| `Core.ps1` | 60% |
| `Classification.ps1` | 55% |
| `WpfShims.ps1` | 80% |
| `WpfHost.ps1` | 50% |
| `WpfEventHandlers.ps1` | 40% (GUI-Handling ist schwer vollständig zu covern) |

Task `tests: coverage` prüft aktuell einen globalen **Interim-Schwellwert von 34%** (`-CoverageTarget 34`).
Das **Sprintziel bleibt 50%** (wird nach Ausbau der Harness-Tests wieder als Gate gesetzt).

---

## 5. E2E-Tests

E2E-Tests (`dev/tests/e2e/`) nutzen synthetische ROM-Verzeichnisse aus `dev/tests/fixtures/` mit Dummy-Dateien (bekannte Hashes, 0-Byte-Stubs). Sie verifizieren End-to-End:

1. Modul-Initialisierung ohne Fehler
2. `Invoke-RegionDedupe` mit DryRun-Modus
3. Report-Generierung (CSV + HTML vorhanden nach Lauf)
4. Keine echten Dateibewegungen ohne `Mode=Move`

---

## 6. CI-Pipeline-Stages

| Stage | Trigger | Was wird getestet |
|---|---|---|
| `unit` | jeder Commit | ~183 Unit-Testdateien, < 60s |
| `integration` | PR | Unit + Integration (WpfSmoke, Plugins, DAT-Index) |
| `e2e` | vor Release | Vollständiger Durchlauf mit Fixtures |
| `benchmark gate` | vor Release | Performance-Benchmarks (LRU, Cache) |
| `coverage` | vor Release | Coverage ≥ 50% |
| `governance` | jeder Commit | Modul-Grenzen, Komplexitätslimits, LOC-Gates |
| `catch-compliance` | jeder Commit | TD-002: Keine Silent-Catches außer WPF |
| `mutation` | vor Release | Mutation-Testing (Reporting only) |

### Testausführung

```powershell
# Vollständige Pipeline
pwsh -NoProfile -File ./dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage all

# Einzelne Stufe
pwsh -NoProfile -File ./dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage unit

# Mit Coverage-Gate
pwsh -NoProfile -File ./dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage unit -Coverage -CoverageTarget 50

# Einzelne Testdatei direkt
Invoke-Pester -Path ./dev/tests/unit/Core.Tests.ps1 -Output Detailed

# Mit Flaky-Retry
pwsh -NoProfile -File ./dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage unit -FlakyRetries 2
```
