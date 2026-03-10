# Architektur-Map & Modul-Verantwortlichkeiten

Stand: 2026-03-02
Referenzen: ADR 0002 (Ports/Services), ADR 0004 (Vertical Slices + Hexagonal-light)

---

## 1 — Architektur-Übersicht (Hexagonal-light + Vertical Slices)

```
┌─────────────────────────────────────────────────────────────────────┐
│                        ENTRY POINTS                                │
│  simple_sort.ps1 (GUI)  │  Invoke-RomCleanup.ps1 (CLI)            │
│  Invoke-RomCleanupApi.ps1 (API)  │  Invoke-RomCleanupPreflight.ps1│
└──────────┬──────────────────┬────────────────┬──────────────────────┘
           │                  │                │
           ▼                  ▼                ▼
┌──────────────────┐ ┌────────────────┐ ┌──────────────────────────┐
│   WPF Adapter    │ │  CLI Adapter   │ │     API Adapter          │
│ WpfApp           │ │ OperationAdap. │ │ ApiServer                │
│ WpfHost          │ │ Invoke-CliRun  │ │ Start-RomCleanupApi...   │
│ WpfEventHandlers │ │                │ │                          │
│ WpfSlice.*  (6)  │ │                │ │                          │
│ WpfMainViewModel │ │                │ │                          │
│ SimpleSort.WpfM  │ │                │ │                          │
└────────┬─────────┘ └───────┬────────┘ └────────────┬─────────────┘
         │                   │                       │
         └───────────────────┼───────────────────────┘
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    APPLICATION LAYER (Use Cases)                    │
│                                                                     │
│  ApplicationServices.ps1                                          │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ Invoke-RunDedupeService      Invoke-RunSortService           │   │
│  │ Invoke-RunConversionService  Invoke-RunRollbackService       │   │
│  │ Invoke-RunPreflight          Invoke-RunReportService         │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                     │
│  RunHelpers.ps1 (→ .Execution, .Audit, .Insights, .SettingsLeg.)  │
│  SafetyToolsService.ps1                                            │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│                       PORT INTERFACES                              │
│                                                                     │
│  PortInterfaces.ps1                                                │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │ FileSystem    ToolRunner    DatRepository                    │   │
│  │ AuditStore    AppState      RegionDedupe                     │   │
│  └──────────────────────────────────────────────────────────────┘   │
│  New-OperationPorts (Composite Factory)                            │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     DOMAIN / ENGINE LAYER                          │
│                                                                     │
│  Core.ps1           Dedupe.ps1          Convert.ps1                │
│  Dat.ps1            DatSources.ps1      Classification.ps1         │
│  FormatScoring.ps1  SetParsing.ps1      Sets.ps1                   │
│  Ps3Dedupe.ps1      ZipSort.ps1         ConsoleSort.ps1            │
│  ConsolePlugins.ps1                                                │
└──────────────────────────┬──────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   INFRASTRUCTURE / SHARED                          │
│                                                                     │
│  FileOps.ps1        Settings.ps1         Logging.ps1               │
│  EventBus.ps1       LruCache.ps1         MemoryGuard.ps1           │
│  PhaseMetrics.ps1   RunspaceLifecycle.ps1 BackgroundOps.ps1        │
│  Notifications.ps1  Scheduler.ps1        UpdateCheck.ps1           │
│  GuiLogBuffer.ps1   UiTelemetry.ps1      RunIndex.ps1              │
│  Localization.ps1   Compatibility.ps1    SettingsBinding.ps1       │
│                                                                     │
│  CONTRACTS:                                                        │
│  DataContracts.ps1  ErrorContracts.ps1   AppStateSchema.ps1        │
│                                                                     │
│  OBSERVABILITY:                                                    │
│  CatchGuard.ps1     LogLanguagePolicy.ps1 SecurityEventStream.ps1  │
│  DiagnosticsService.ps1                                            │
│                                                                     │
│  REPORTING:                                                        │
│  Report.ps1         ReportBuilder.ps1    OpsBundle.ps1             │
│  RunHelpers.Audit.ps1                                              │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 2 — Modul-Verantwortlichkeiten (Owner + Allowed Dependencies)

### Legende
- **Owner**: Zuständiges Team / Rolle
- **Layer**: Domain | Application | Adapter | Infrastructure | Observability
- **Allowed Deps**: Module, die dieses Modul importieren / aufrufen darf
- **Forbidden**: Explizit verbotene Abhängigkeiten

### 2.1 Entry Points

| Modul | Owner | Layer | Primärverantwortung | Allowed Deps |
|---|---|---|---|---|
| `simple_sort.ps1` | GUI Team | Entry | GUI-Startpunkt. Lädt Module, startet WPF | Loader, WpfApp |
| `Invoke-RomCleanup.ps1` | Core Team | Entry | CLI-Startpunkt. Parst Args, ruft Services | Loader, ApplicationServices, OperationAdapters |
| `Invoke-RomCleanupApi.ps1` | API Team | Entry | API-Startpunkt. Startet HTTP-Server | Loader, ApiServer |
| `Invoke-RomCleanupPreflight.ps1` | Core Team | Entry | Preflight-Checks CLI | Loader, ApplicationServices |

### 2.2 Adapter Layer

| Modul | Owner | Layer | Primärverantwortung | Allowed Deps | Forbidden |
|---|---|---|---|---|---|
| `WpfApp.ps1` | GUI Team | Adapter | WPF-App-Bootstrap, Dispatcher | SimpleSort.WpfMain | Domain direct |
| `WpfHost.ps1` | GUI Team | Adapter | XAML-Fenster-Erstellung, Assembly-Init | WpfXaml | Domain direct |
| `WpfXaml.ps1` | GUI Team | Adapter | XAML-Laden (Datei + Fallback) | — | Domain direct |
| `WpfMainViewModel.ps1` | GUI Team | Adapter | ViewModel-Initialisierung | WpfHost | Domain direct |
| `WpfEventHandlers.ps1` | GUI Team | Adapter | Event-Wiring, UI↔UseCase-Delegation | WpfSlice.*, ApplicationServices, AppState | FileOps direct, Dedupe direct |
| `WpfSlice.Roots.ps1` | GUI Team | Adapter | Root-Input, Browse, Paste, Validation | AppState, FileOps (via Port) | Dedupe, Convert |
| `WpfSlice.RunControl.ps1` | GUI Team | Adapter | Start/Cancel/Progress/Completion | ApplicationServices, AppState | Dat, FileOps direct |
| `WpfSlice.Settings.ps1` | GUI Team | Adapter | Settings/Profile/Theme Binding | Settings, AppState | Dedupe, Convert |
| `WpfSlice.DatMapping.ps1` | GUI Team | Adapter | DAT-Grid, Mapping, CRC Verify | Dat, DatSources, AppState | Dedupe |
| `WpfSlice.ReportPreview.ps1` | GUI Team | Adapter | Reports, Export, Dashboards | Report, ReportBuilder, AppState | FileOps direct |
| `WpfSlice.AdvancedFeatures.ps1` | GUI Team | Adapter | Plugin-Manager, Rollback, Watch, Feature-Tab (65 Module) | ApplicationServices, AppState, alle Feature-Module | Dedupe direct |
| `WpfWizard.ps1` | GUI Team | Adapter | ISS-001 First-Start Wizard, Preflight, Intent-Auswahl | WpfHost, Settings, AppState | — |
| `WpfSelectionConfig.ps1` | GUI Team | Adapter | UI-Selection/Config-Binding | AppState | Domain direct |
| `SimpleSort.WpfMain.ps1` | GUI Team | Adapter | WPF-Main-Orchestrierung | WpfHost, WpfMainViewModel, WpfEventHandlers | Domain direct |
| `ApiServer.ps1` | API Team | Adapter | HTTP-REST-Server, Auth, CORS, Rate-Limit | ApplicationServices, OperationAdapters | UI modules |
| `OperationAdapters.ps1` | Core Team | Adapter | CLI/GUI Adapter → ApplicationServices | ApplicationServices | Domain direct |
| `WpfShims.ps1` | GUI Team | Adapter | WPF-Kompatibilitäts-Shims | — | Domain direct |

### 2.3 Application Layer

| Modul | Owner | Layer | Primärverantwortung | Allowed Deps | Forbidden |
|---|---|---|---|---|---|
| `ApplicationServices.ps1` | Core Team | Application | Service-Fassaden (Run, Sort, Convert, Rollback) | PortInterfaces, Domain (via Ports) | UI modules |
| `RunHelpers.ps1` | Core Team | Application | Modularer Run-Helper-Hub | .Execution, .Audit, .Insights, .SettingsLegacy | UI modules |
| `RunHelpers.Execution.ps1` | Core Team | Application | Run-Ausführungslogik | Domain modules | UI modules |
| `RunHelpers.Audit.ps1` | Core Team | Application | Audit-CSV/HMAC-Erzeugung | FileOps, AuditService | UI modules |
| `RunHelpers.Insights.ps1` | Core Team | Application | Run-Analyse/Statistik | Domain modules | UI modules |
| `RunHelpers.SettingsLegacy.ps1` | Core Team | Application | Legacy-Settings-Migration | Settings | UI modules |
| `SafetyToolsService.ps1` | Core Team | Application | Sicherheitsprofile (Conservative/Balanced/Expert) | Settings | UI modules |
| `PortInterfaces.ps1` | Core Team | Application | Port-Factories (FileSystem, ToolRunner, Dat, Audit, AppState) | Infra-Module (indirekt via Ports) | UI modules |

### 2.4 Domain / Engine Layer

| Modul | Owner | Layer | Primärverantwortung | Allowed Deps | Forbidden |
|---|---|---|---|---|---|
| `Core.ps1` | Core Team | Domain | Zentrale Geschäftsregeln | — | UI, Infra direct |
| `Dedupe.ps1` | Core Team | Domain | Region-Deduplizierung (1G1R, BIOS, Junk, DAT-Verify) | Classification, FormatScoring, Sets, Dat | UI modules |
| `Convert.ps1` | Core Team | Domain | Format-Konvertierung (CHD/RVZ/ZIP/PBP) | Tools, FormatScoring | UI modules |
| `Dat.ps1` | Core Team | Domain | DAT-/No-Intro-Datenbank, LRU-Hash-Cache | DatSources, LruCache | UI modules |
| `DatSources.ps1` | Core Team | Domain | DAT-Quellen-Auflösung | — | UI modules |
| `Classification.ps1` | Core Team | Domain | Datei-Klassifikation (Region, Typ, Tags) | Core | UI modules |
| `FormatScoring.ps1` | Core Team | Domain | Format-Scoring/Prioritätslogik | — | UI modules |
| `SetParsing.ps1` | Core Team | Domain | Set/Kollektion-Parsing | — | UI modules |
| `Sets.ps1` | Core Team | Domain | Set-Verwaltung | SetParsing | UI modules |
| `Ps3Dedupe.ps1` | Core Team | Domain | PS3-spezifische Dedup-Logik | — | UI modules |
| `ZipSort.ps1` | Core Team | Domain | ZIP-Sortierungslogik | — | UI modules |
| `ConsoleSort.ps1` | Core Team | Domain | Konsolen-Sortierung | ConsolePlugins | UI modules |
| `ConsolePlugins.ps1` | Core Team | Domain | Konsolen-Plugin-Aufloesung | - | UI modules |
| `FolderDedupe.ps1` | Core Team | Domain | Ordner-Deduplizierung (DOS/PC: Base-Name-Matching) | Classification, FileOps | UI modules |

### 2.4b Feature Modules (Phases 1–4)

Eigenständige Feature-Module, orchestriert über `ApplicationServices.ps1` Facades.

| Modul | Layer | Primärverantwortung |
|---|---|---|
| `DatRename.ps1` | Domain | DAT-basierte ROM-Umbenennung (QW-01) |
| `EcmDecompress.ps1` | Domain | ECM→BIN-Dekompression (QW-02) |
| `ArchiveRepack.ps1` | Domain | ZIP↔7z Archive-Repack (QW-03) |
| `ConversionEstimate.ps1` | Domain | Speicherplatz-Prognose (QW-04) |
| `JunkReport.ps1` | Domain | Junk-Klassifikationsreport (QW-05) |
| `KeyboardShortcuts.ps1` | Adapter | WPF Keyboard-Shortcuts (QW-06) |
| `ThemeManager.ps1` | Adapter | Dark/Light-Theme-Toggle (QW-07) |
| `RomFilter.ps1` | Adapter | ROM-Suche/Filter (QW-08) |
| `DuplicateHeatmap.ps1` | Domain | Duplikat-Heatmap (QW-09) |
| `CliExport.ps1` | Domain | CLI-Command-Export (QW-10) |
| `WebhookNotification.ps1` | Infrastructure | Webhook-Benachrichtigung (QW-11) |
| `PortableMode.ps1` | Infrastructure | Portable-Modus (QW-12) |
| `CollectionCsvExport.ps1` | Domain | CSV-Export (QW-13) |
| `RunHistory.ps1` | Infrastructure | Run-History-Browser (QW-14) |
| `M3uGenerator.ps1` | Domain | M3U-Playlist-Generator (QW-15) |
| `RetroArchPlaylist.ps1` | Domain | RetroArch-Playlist-Export (QW-16) |
| `MissingRomTracker.ps1` | Domain | Missing-ROM-Tracker (MF-01) |
| `CrossRootDedupe.ps1` | Domain | Cross-Root-Duplikat-Finder (MF-02) |
| `HeaderAnalysis.ps1` | Domain | ROM-Header-Analyse (MF-03) |
| `CompletenessTracker.ps1` | Domain | Sammlung-Completeness-Ziel (MF-04) |
| `CollectionManager.ps1` | Domain | Smart-Collections (MF-05) |
| `ConversionPipeline.ps1` | Domain | CSO/ZSO→ISO→CHD-Pipeline (MF-06) |
| `NKitConvert.ps1` | Domain | NKit→ISO-Rückkonvertierung (MF-07) |
| `ConvertQueue.ps1` | Domain | Konvertierungs-Queue (MF-08) |
| `ConversionVerify.ps1` | Domain | Batch-Verify nach Konvertierung (MF-09) |
| `FormatPriority.ps1` | Domain | Konvertierungs-Prioritätsliste (MF-10) |
| `DatAutoUpdate.ps1` | Domain | DAT-Auto-Update (MF-11) |
| `DatDiffViewer.ps1` | Domain | DAT-Diff-Viewer (MF-12) |
| `TosecDatSupport.ps1` | Domain | TOSEC-DAT-Support (MF-13) |
| `ParallelHashing.ps1` | Infrastructure | Parallel-Hashing (MF-14) |
| `CommandPalette.ps1` | Adapter | Command-Palette (MF-15) |
| `SplitPanelPreview.ps1` | Adapter | Split-Panel-Vorschau (MF-16) |
| `FilterBuilder.ps1` | Adapter | Visueller Filter-Builder (MF-17) |
| `SystemTray.ps1` | Adapter | Mini-Modus/System-Tray (MF-18) |
| `RuleEngine.ps1` | Domain | User-Klassifikationsregeln (MF-19) |
| `PipelineEngine.ps1` | Domain | Conditional-Pipelines (MF-20) |
| `DryRunCompare.ps1` | Domain | Dry-Run-Vergleich (MF-21) |
| `SortTemplates.ps1` | Domain | Ordnerstruktur-Vorlagen (MF-22) |
| `SchedulerAdvanced.ps1` | Infrastructure | Run-Scheduler (MF-23) |
| `IntegrityMonitor.ps1` | Domain | Integritäts-Monitor (MF-24) |
| `BackupManager.ps1` | Infrastructure | Backup-Strategie (MF-25) |
| `Quarantine.ps1` | Domain | ROM-Quarantäne (MF-26) |
| `CoverScraper.ps1` | Infrastructure | Cover-Scraping (LF-01) |
| `GenreClassification.ps1` | Domain | Genre-/Tag-Klassifikation (LF-02) |
| `LauncherIntegration.ps1` | Domain | Emulator-Launcher-Integration (LF-03) |
| `PlaytimeTracker.ps1` | Domain | Spielzeit-Tracking-Import (LF-04) |
| `PatchEngine.ps1` | Domain | IPS/BPS/UPS-Patch-Engine (LF-05) |
| `HeaderRepair.ps1` | Domain | ROM-Header-Reparatur (LF-06) |
| `ArcadeMergeSplit.ps1` | Domain | Arcade ROM-Merge/Split (LF-07) |
| `StorageTiering.ps1` | Infrastructure | Intelligent Storage Tiering (LF-08) |
| `CustomDatEditor.ps1` | Domain | Custom-DAT-Editor (LF-09) |
| `CloneListViewer.ps1` | Domain | Clone-List-Visualisierung (LF-10) |
| `HashDatabaseExport.ps1` | Domain | Hash-Datenbank-Export (LF-11) |
| `VirtualFolderPreview.ps1` | Adapter | Virtuelle Ordner-Vorschau (LF-12) |
| `Accessibility.ps1` | Adapter | Barrierefreiheit (LF-13) |
| `PdfReportExport.ps1` | Domain | PDF-Report-Export (LF-14) |
| `NasOptimization.ps1` | Infrastructure | NAS/SMB-Optimierung (LF-15) |
| `FtpSource.ps1` | Infrastructure | FTP/SFTP-Source (LF-16) |
| `CloudSettingsSync.ps1` | Infrastructure | Cloud-Settings-Sync (LF-17) |
| `PluginMarketplace.ps1` | Adapter | Plugin-Marketplace-UI (LF-18) |
| `RulePackSharing.ps1` | Domain | Rule-Pack-Sharing (LF-19) |
| `ThemeEngine.ps1` | Adapter | Theme-Engine (LF-20) |
| `DockerContainer.ps1` | Infrastructure | Docker-Container (XL-01) |
| `MobileWebUI.ps1` | Adapter | Mobile-Web-UI (XL-02) |
| `WindowsContextMenu.ps1` | Adapter | Windows-Context-Menu (XL-03) |
| `PSGalleryModule.ps1` | Infrastructure | PSGallery-Modul (XL-04) |
| `PackageManagerIntegration.ps1` | Infrastructure | Winget/Scoop-Paket (XL-05) |
| `TrendAnalysis.ps1` | Domain | Historische Trendanalyse (XL-06) |
| `EmulatorCompatReport.ps1` | Domain | Emulator-Kompatibilitäts-Report (XL-07) |
| `CollectionSharing.ps1` | Domain | Sammlungs-Sharing (XL-08) |
| `GpuHashing.ps1` | Infrastructure | GPU-beschleunigtes Hashing (XL-09) |
| `UsnJournalScan.ps1` | Infrastructure | USN-Journal Differential-Scan (XL-10) |
| `HardlinkMode.ps1` | Infrastructure | Hardlink/Symlink-Modus (XL-11) |
| `ToolImport.ps1` | Domain | clrmamepro/RomVault-Import (XL-12) |
| `MultiInstanceSync.ps1` | Infrastructure | Multi-Instance-Koordination (XL-13) |
| `Telemetry.ps1` | Infrastructure | Telemetrie (XL-14) |

### 2.5 Infrastructure / Shared Layer

| Modul | Owner | Layer | Primärverantwortung | Allowed Deps | Forbidden |
|---|---|---|---|---|---|
| `FileOps.ps1` | Core Team | Infrastructure | Dateisystem-Operationen (Move, Scan, Path-Safety) | — | UI modules |
| `Settings.ps1` | Core Team | Infrastructure | Settings-Laden/Speichern/Defaults | - | UI modules |
| `ConfigProfiles.ps1` | Core Team | Infrastructure | Config-Profile-Verwaltung (Save/Load/Export/Import) | Settings | UI modules |
| `ConfigMerge.ps1` | Core Team | Infrastructure | Config-Precedence-Merge (Default/User/Profile/Env) | Settings, ConfigProfiles | UI modules |
| `AppState.ps1` | Core Team | Infrastructure | App-State-Verwaltung (Get/Set/Watch/History) | - | UI modules |
| `SettingsBinding.ps1` | Core Team | Infrastructure | Settings-UI-Binding-Helper | Settings | - |
| `Logging.ps1` | Core Team | Infrastructure | Structured Logging (JSONL, Rotation, CorrelationId) | — | — |
| `EventBus.ps1` | Core Team | Infrastructure | Publish/Subscribe Event-System | — | — |
| `LruCache.ps1` | Core Team | Infrastructure | Generischer LRU-Cache | — | — |
| `MemoryGuard.ps1` | Perf Team | Infrastructure | Memory-Budget-Guard mit Soft/Hard-Limits | — | — |
| `PhaseMetrics.ps1` | Perf Team | Infrastructure | Phasen-Zeitmessung (Start/Complete/Export) | — | — |
| `RunspaceLifecycle.ps1` | Core Team | Infrastructure | Shared Runspace-Pool-Management | — | — |
| `BackgroundOps.ps1` | Core Team | Infrastructure | Background-Job-Verwaltung | RunspaceLifecycle | — |
| `Notifications.ps1` | Core Team | Infrastructure | Benachrichtigungssystem | — | — |
| `Scheduler.ps1` | Core Team | Infrastructure | Task-Scheduling | — | — |
| `UpdateCheck.ps1` | Core Team | Infrastructure | Update-Prüfung | — | — |
| `GuiLogBuffer.ps1` | GUI Team | Infrastructure | GUI-Log-Pufferverwaltung | — | — |
| `UiTelemetry.ps1` | GUI Team | Infrastructure | UI-Nutzungstelemetrie | EventBus | — |
| `RunIndex.ps1` | Core Team | Infrastructure | Run-Index/Historie | — | — |
| `Localization.ps1` | Core Team | Infrastructure | i18n/Lokalisierung | — | — |
| `Compatibility.ps1` | Core Team | Infrastructure | Kompatibilitäts-Checks | — | — |

### 2.6 Contracts & Observability

| Modul | Owner | Layer | Primärverantwortung | Allowed Deps | Forbidden |
|---|---|---|---|---|---|
| `DataContracts.ps1` | Core Team | Contracts | Schema-Validierung, Deep-Copy, DTOs | — | — |
| `ErrorContracts.ps1` | Core Team | Contracts | Strukturierte Fehlerobjekte, Fehler-Kategorien | — | — |
| `AppStateSchema.ps1` | Core Team | Contracts | Typed State-Schema + Assert-Validation | DataContracts | — |
| `UseCaseContracts.ps1` | Core Team | Contracts | Versionierte UseCase-Input/Output-Verträge (`Run`, `Preflight`, `Convert`, `Rollback`, `Report`) | DataContracts, ErrorContracts | — |
| `CatchGuard.ps1` | Core Team | Observability | Silent-Catch-Governance | Logging | — |
| `LogLanguagePolicy.ps1` | Core Team | Observability | Log-Sprach-Policy (DE/EN-Enforcement) | — | — |
| `SecurityEventStream.ps1` | Security Team | Observability | Security-Audit-Events (JSONL + EventBus) | EventBus, Logging | — |
| `DiagnosticsService.ps1` | Core Team | Observability | Diagnose-Sammlung | — | — |

### 2.7 Reporting

| Modul | Owner | Layer | Primärverantwortung | Allowed Deps | Forbidden |
|---|---|---|---|---|---|
| `Report.ps1` | Core Team | Reporting | Report-Generierung | — | UI modules |
| `ReportBuilder.ps1` | Core Team | Reporting | Strukturierte Report-Erstellung | Report | UI modules |
| `OpsBundle.ps1` | Core Team | Reporting | Operation-Bundle-Export | — | UI modules |

---

## 3 — Vertical-Slice-Zielbild

### Aktuelle Slices (WPF, alle 6 fertig)

| Slice | Modul | Scope |
|---|---|---|
| Roots & Input | `WpfSlice.Roots.ps1` | Add/Remove/Browse/Paste/Validation |
| Run Control | `WpfSlice.RunControl.ps1` | Start/Cancel/Progress/Completion |
| Settings/Profile/Theme | `WpfSlice.Settings.ps1` | Settings-Roundtrip, Theme, Profile |
| DAT Mapping | `WpfSlice.DatMapping.ps1` | DAT-Grid, CRC-Verify, Mapping |
| Report Preview | `WpfSlice.ReportPreview.ps1` | Reports, Export, Dashboards |
| Advanced Features | `WpfSlice.AdvancedFeatures.ps1` | Plugin-Manager, Rollback, Watch, 65 Feature-Buttons (Phase 1-4) |

### Ziel-UseCase-Slices (Application Layer)

Jeder UseCase-Slice hat:
- **Input-Contract** (typisierte Parameter)
- **Output-Contract** (strukturiertes Ergebnisobjekt)
- **Fehlervertrag** (kategorisierte Fehler via `ErrorContracts.ps1`)
- **Port-Abhängigkeiten** (via `PortInterfaces.ps1`)

| UseCase | Service-Funktion | Input | Output | Ports |
|---|---|---|---|---|
| **Run (Dedupe)** | `Invoke-RunDedupeService` | `RunDedupeInput` | `RunDedupeOutput` | FileSystem, ToolRunner, DatRepository, AuditStore |
| **Run (Sort)** | `Invoke-RunSortService` | `RunSortInput` | `RunSortOutput` | FileSystem, ToolRunner |
| **Preflight** | `Invoke-RunPreflight` | `PreflightInput` | `PreflightOutput` | FileSystem, DatRepository |
| **Convert** | `Invoke-RunConversionService` | `ConversionInput` | `ConversionOutput` | FileSystem, ToolRunner |
| **Rollback** | `Invoke-RunRollbackService` | `RollbackInput` | `RollbackOutput` | AuditStore |
| **Reporting** | `Invoke-RunReportService` | `ReportInput` | `ReportOutput` | FileSystem |

### Zielzustand der Kontrollflüsse

```
GUI/CLI/API Entry
  └── Adapter (WpfSlice.*/CliRunAdapter/ApiServer)
        └── ApplicationService (Invoke-Run*Service)
              ├── Ports (PortInterfaces.ps1)
              │     ├── FileSystem Port
              │     ├── ToolRunner Port
              │     ├── DatRepository Port
              │     ├── AuditStore Port
              │     └── AppState Port
              └── Domain Engine (Dedupe/Convert/Dat/Classification)
```

**Regel:** Adapter → Application → Domain. Kein Bypass.

---

## 4 — UseCase-Contracts (versioniert, v1)

### 4.1 RunDedupeContract

```
RunDedupeInput v1:
  Roots           : string[]        (required, non-empty)
  Mode            : 'DryRun'|'Move' (required)
  Prefer          : string[]        (optional, default: EU,US,WORLD,JP)
  Extensions      : string[]        (optional)
  UseDat          : bool            (optional, default: false)
  DatRoot         : string          (optional)
  DatHashType     : string          (optional, default: SHA1)
  DatMap          : hashtable       (optional)
  ToolOverrides   : hashtable       (optional)

RunDedupeOutput v1:
  Success         : bool
  TotalFiles      : int
  WinnerCount     : int
  LoserCount      : int
  JunkCount       : int
  MoveCount       : int
  Errors          : OperationError[]
  AuditCsvPath    : string
  Duration        : TimeSpan
  PhaseMetrics    : hashtable
```

### 4.2 PreflightContract

```
PreflightInput v1:
  Roots           : string[]        (required, non-empty)
  Mode            : 'DryRun'|'Move' (required)
  UseDat          : bool            (optional)
  DatRoot         : string          (optional)

PreflightOutput v1:
  Valid           : bool
  Checks          : PreflightCheck[]
  Errors          : OperationError[]
  Warnings        : string[]

PreflightCheck:
  Name            : string
  Passed          : bool
  Message         : string
  FixSuggestion   : string          (optional)
```

### 4.3 ConversionContract

```
ConversionInput v1:
  Operation       : 'WinnerMove'|'Preview'|'Standalone'  (required)
  Enabled         : bool            (required)
  Mode            : 'DryRun'|'Move' (required)
  Result          : object          (optional, from Dedupe)
  Roots           : string[]        (optional)
  ToolOverrides   : hashtable       (optional)

ConversionOutput v1:
  Success         : bool
  ConvertedCount  : int
  SkippedCount    : int
  FailedCount     : int
  Errors          : OperationError[]
  Duration        : TimeSpan
```

### 4.4 RollbackContract

```
RollbackInput v1:
  AuditCsvPath       : string       (required)
  AllowedRestoreRoots: string[]     (required, non-empty)
  AllowedCurrentRoots: string[]     (required, non-empty)
  DryRun             : bool         (optional, default: true)

RollbackOutput v1:
  Success            : bool
  RestoredCount      : int
  SkippedCount       : int
  FailedCount        : int
  Errors             : OperationError[]
```

### 4.5 ReportContract

```
ReportInput v1:
  Type            : 'Summary'|'Diff'|'Audit'|'KPI' (required)
  SourceData      : object          (required)
  OutputPath      : string          (optional)
  Format          : 'JSON'|'Markdown'|'CSV'        (optional, default: JSON)

ReportOutput v1:
  Success         : bool
  OutputPath      : string
  Errors          : OperationError[]
```

---

## 5 — Dependency-Grenzen (verbindliche Regeln)

### 5.1 Layer-Regeln

| Von (Aufrufer) | Darf aufrufen | Darf NICHT aufrufen |
|---|---|---|
| Entry Points | Loader, Adapter Layer | Domain direct |
| Adapter (WPF/CLI/API) | Application Layer, Infrastructure, Contracts | Domain direct (Ausnahme: Settings, AppState) |
| Application Layer | Domain, Ports, Infrastructure, Contracts | Adapter (WPF/CLI/API) |
| Domain Layer | Andere Domain-Module, Contracts | Adapter, Application, UI modules |
| Infrastructure | Andere Infrastructure, Contracts | Application, Adapter, Domain |
| Observability | Infrastructure (Logging, EventBus) | Application, Adapter, Domain |

### 5.2 Verbotene Abhängigkeiten (explizit)

| Regel | Beschreibung |
|---|---|
| **NO-UI-IN-DOMAIN** | Domain-Module dürfen keine WPF/WpfSlice/SimpleSort-Module referenzieren |
| **NO-DOMAIN-BYPASS** | Adapter dürfen `Invoke-RegionDedupe`, `Invoke-FormatConversion` etc. nicht direkt aufrufen — nur via ApplicationServices |
| **NO-INFRA-IN-DOMAIN** | Domain-Module dürfen `FileOps.ps1` nicht direkt aufrufen (nur via Port) — Ausnahme: bestehende Phase-B-Migration |
| **NO-CROSSADAPTER** | WPF-Module dürfen `ApiServer.ps1` nicht aufrufen und umgekehrt |

### 5.3 Erlaubte Ausnahmen (dokumentiert)

| Ausnahme | Begründung | Ablaufdatum |
|---|---|---|
| `Dedupe.ps1` ruft `FileOps.ps1` direkt | Phase-B-Migration: wird über Port umgeleitet | 2026-Q3 |
| `Convert.ps1` ruft `Tools.ps1` direkt | Phase-B-Migration: wird über ToolRunner-Port umgeleitet | 2026-Q3 |
| `Dat.ps1` ruft `LruCache.ps1` direkt | Infrastruktur-Nutzung in Domain akzeptabel (Cache ist generisch) | Dauerhaft |

---

## 6 — Monolith-Splitting-Plan

### 6.1 Status der großen Dateien

| Datei | LOC (aktuell) | LOC (Ziel) | Status | Maßnahme |
|---|---|---|---|---|
| `WpfEventHandlers.ps1` | ~2 210 | <1 500 | ✅ 6 Slices done | Weiteres Splitting bei Bedarf |
| `Dedupe.ps1` | ~1 600 | <800 | 🔲 Geplant | Split in Domain-Phasen |
| `Convert.ps1` | ~1 200 | <600 | 🔲 Geplant | Split in Strategie + Execution |
| `Dat.ps1` | ~1 180 | <600 | 🔲 Geplant | Split in Index + Cache + Query |
| `ApiServer.ps1` | ~1 000 | <500 | 🔲 Geplant | Split in Router + Handlers |

### 6.2 Splitting-Strategie je Modul

#### Dedupe.ps1 → 3 Module
```
Dedupe.ps1           → Dedupe.Core.ps1         (~500 LOC) Hauptlogik, Pipeline-Orchestrierung
                     → Dedupe.Classify.ps1     (~500 LOC) Klassifikation, Region-Scoring
                     → Dedupe.Report.ps1       (~400 LOC) Audit, Statistik, Report-Generierung
```

#### Convert.ps1 → 2 Module
```
Convert.ps1          → Convert.Strategy.ps1    (~400 LOC) Format-Tabellen, Zielformat-Auswahl
                     → Convert.Execution.ps1   (~600 LOC) Batch-Verarbeitung, Pool-Management
```

#### Dat.ps1 → 2 Module
```
Dat.ps1              → Dat.Index.ps1           (~600 LOC) DAT-Parsing, Index-Aufbau
                     → Dat.Cache.ps1           (~400 LOC) LRU-Hash-Cache, Scan-Cache
```

#### ApiServer.ps1 → 2 Module
```
ApiServer.ps1        → ApiServer.Router.ps1    (~400 LOC) HTTP-Listener, Routing, CORS, Auth
                     → ApiServer.Handlers.ps1  (~500 LOC) Endpoint-Handler, Response-Builder
```

### 6.3 Reihenfolge & Abhängigkeiten

| Priorität | Modul | Voraussetzung | Zeitrahmen |
|---|---|---|---|
| P1 | `Dedupe.ps1` | UseCase-Contracts finalisiert | 2026-Q2 |
| P2 | `Convert.ps1` | Dedupe-Split abgeschlossen | 2026-Q2 |
| P3 | `Dat.ps1` | Unabhängig | 2026-Q2/Q3 |
| P4 | `ApiServer.ps1` | Unabhängig | 2026-Q3 |

### 6.4 DoD für jeden Split
- [ ] Maximal ein Split pro PR
- [ ] Keine funktionalen Änderungen
- [ ] Alle Tests grün (unit + integration + e2e)
- [ ] Governance-Gate: keine Hard-Limit-Verletzungen
- [ ] ModuleFileList.ps1 aktualisiert
- [ ] Dependency-Map in diesem Dokument aktualisiert

---

## 7 — Review-Kriterien (PR-Checkliste)

Jeder PR muss folgende Architektur-Checks bestehen:

- [ ] **Layer-Regel eingehalten:** Keine verbotenen Querverweise (siehe 5.2)
- [ ] **Contract-Kompatibilität:** UseCase-Inputs/Outputs nicht gebrochen
- [ ] **Port-Nutzung:** Neue Infra-Zugriffe gehen über Port-Interfaces
- [ ] **Governance-Gate grün:** `Invoke-GovernanceGate.ps1` ohne Hard-Limit-Verletzung
- [ ] **Module-Owner:** Änderungen in fremdem Layer benötigen Owner-Review
- [ ] **Dependency-Boundary-Test grün:** `ModuleDependencyBoundary.Tests.ps1`
