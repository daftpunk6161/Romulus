# Contributing

## Voraussetzungen
- PowerShell 7+ (für Tests via Pester 5)
- Windows 10/11 (WPF GUI)
- Pester Modul installiert (`Install-Module Pester -Force`)
- PSScriptAnalyzer installiert (`Install-Module PSScriptAnalyzer -Force`)

## Setup
1. Repository klonen
2. In Repo-Root wechseln
3. Tests ausführen:
   - `pwsh -NoProfile -File ./dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage unit`
   - `pwsh -NoProfile -File ./dev/tools/pipeline/Invoke-TestPipeline.ps1 -Stage all`
4. Linting:
   - `Invoke-ScriptAnalyzer -Path ./dev/modules/*.ps1 -Settings ./PSScriptAnalyzerSettings.psd1`

## Architektur

Hexagonal-light + Vertical Slices (ADR-0004). Schichten kommunizieren nur abwärts:

```
Entry Points → Adapter → Application → Domain → Infrastructure
```

- Produktivcode liegt in `dev/modules/` (~90 Module)
- `simple_sort.ps1` ist GUI Entry Point (WPF)
- `Invoke-RomCleanup.ps1` ist CLI Entry Point
- `Invoke-RomCleanupApi.ps1` ist REST API Entry Point

### Schichten

| Schicht | Module | Zweck |
|---------|--------|-------|
| **Entry Points** | `simple_sort.ps1`, `Invoke-RomCleanup.ps1`, `Invoke-RomCleanupApi.ps1` | Startpunkte für GUI/CLI/API |
| **Adapter** | `WpfSlice.*.ps1` (6 Slices), `WpfWizard.ps1`, `WpfEventHandlers.ps1`, `WpfMainViewModel.ps1`, `ApiServer.ps1`, `OperationAdapters.ps1` | UI/API → Application |
| **Application** | `ApplicationServices.ps1` (22 Facades), `RunHelpers.*.ps1`, `PortInterfaces.ps1` | Service-Orchestrierung |
| **Domain** | `Core.ps1`, `Dedupe.ps1`, `Classification.ps1`, `FormatScoring.ps1`, `Convert.ps1`, `Dat.ps1`, `Sets.ps1`, 76 Feature-Module | Pure Geschäftslogik |
| **Infrastructure** | `FileOps.ps1`, `Tools.ps1`, `Settings.ps1`, `Report.ps1`, `Logging.ps1`, `AppState.ps1`, `EventBus.ps1`, `LruCache.ps1` | I/O, FS, Logging |
| **Contracts** | `DataContracts.ps1`, `ErrorContracts.ps1`, `CatchGuard.ps1` | Schema-Validierung, Fehlerobjekte |

### WPF-Module (aktiver GUI-Stack)

| Modul | Zweck |
|-------|-------|
| `dev/modules/wpf/MainWindow.xaml` | WPF-XAML-Layout (114 Buttons, 9 Feature-Expander) |
| `dev/modules/WpfShims.ps1` | WPF-Typen/Binding-Hilfen (Inline-C#) |
| `dev/modules/WpfXaml.ps1` | XAML-Loader |
| `dev/modules/WpfHost.ps1` | Window-Host/Parse/Context |
| `dev/modules/WpfMainViewModel.ps1` | ViewModel mit Undo/Redo (INotifyPropertyChanged) |
| `dev/modules/WpfEventHandlers.ps1` | Event-Wiring (~1700 Zeilen) |
| `dev/modules/WpfSelectionConfig.ps1` | Advanced-Options-Maps |
| `dev/modules/WpfSlice.Roots.ps1` | Slice 1: Root-Verwaltung |
| `dev/modules/WpfSlice.RunControl.ps1` | Slice 2: Start/Cancel/Progress |
| `dev/modules/WpfSlice.Settings.ps1` | Slice 3: Settings/Profile/Theme |
| `dev/modules/WpfSlice.DatMapping.ps1` | Slice 4: DAT-Grid/CRC-Verify |
| `dev/modules/WpfSlice.ReportPreview.ps1` | Slice 5: Reports/Export/Dashboards |
| `dev/modules/WpfSlice.AdvancedFeatures.ps1` | Slice 6: Plugins, Rollback, Watch + 65 Feature-Tab-Buttons |
| `dev/modules/WpfWizard.ps1` | ISS-001 First-Start Wizard |
| `dev/modules/SimpleSort.WpfMain.ps1` | Start-WpfGui Orchestrierung |

### Feature-Module (Phase 1–4)

76 eigenständige Module (`dev/modules/`), jeweils mit eigenem Test in `dev/tests/unit/`. IDs: QW-01 bis XL-14. Details: `FEATURE_ROADMAP.md`.

## Coding Standards
- **Funktionsnamen:** `Verb-Noun` mit PowerShell-approved Verben (englisch)
- **Variablen:** PascalCase für Parameter, camelCase für lokale Variablen
- **Modul-State:** Nur `$script:` Scope
- **Core-Logik pure halten:** Keine UI-Aufrufe, keine `$script:`-Globals in Core/Dedupe/Classification
- **Fehlerbehandlung:** `New-OperationError` / `ConvertTo-OperationError` — keine rohen Strings
- **Kein silent catch** in Domain/Application/IO (nur WPF-Event-Handler erlaubt, TD-002)
- **Keine hardcodierten Pfade/Farben** außerhalb bestehender Designsystem-Primitiven
- **Neue State-Zugriffe** über `PortInterfaces.ps1` (`GetValue`/`SetValue`/`TestCancel`)
- **Kein neues Inline-C#** anlegen (bestehende sind Migrations-Kandidaten)
- Kleine, fokussierte Änderungen
- Öffentliche APIs stabil halten

## Teststrategie
- Erst zielgerichtete Tests für den geänderten Bereich
- Danach `tests: unit` (aktuell ~1300+ Tests, ~183 Testdateien)
- Bei Infrastruktur-/Flow-Änderungen `tests: all`
- Details: `docs/TEST_STRATEGY.md`

## Pull Request Checkliste
- [ ] Relevante Tests lokal grün (`Invoke-TestPipeline.ps1 -Stage unit`)
- [ ] PSScriptAnalyzer ohne Errors
- [ ] Keine Parser-/Lint-Fehler
- [ ] ModuleDependencyBoundary-Tests grün
- [ ] Governance-Gate bestanden (`Invoke-GovernanceGate.ps1`)
- [ ] Neue Module in `ModuleFileList.ps1` registriert
- [ ] ARCHITECTURE_MAP.md aktualisiert bei neuem Modul
- [ ] Schichtengrenzen respektiert (ADR-0004)
- [ ] Backlog/Docs aktualisiert (wenn Scope betroffen)
- [ ] Breaking Changes dokumentiert
