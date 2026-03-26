---
goal: Feature-Landschaft konsolidieren – von 87 auf ~50 release-taugliche Tools bereinigen
version: 1.0
date_created: 2026-03-26
last_updated: 2026-03-26
owner: Romulus Team
status: 'In Progress'
tags: [refactor, chore, architecture, cleanup, ui, release-readiness]
---

# Introduction

![Status: In Progress](https://img.shields.io/badge/status-In%20Progress-orange)

Vollständige Konsolidierung der sichtbaren Feature-Landschaft von Romulus. Ein systematischer Feature-Audit hat ergeben, dass von 87 registrierten Tool-Karten 16 sofort entfernt, 5 deaktiviert, 10 konsolidiert und 5 repariert/umbenannt werden müssen. Der Kern (Pipeline, Dedupe, Sort, Convert, Audit, Rollback) ist produktionsreif mit 5.400+ Tests. Die Tool-Karten-Landschaft ist zu breit, enthält Stubs, Blendwerk, Code-Duplikate und irreführende Benennungen.

Zielzustand: ~50 ehrliche, vollständig implementierte, getestete und korrekt benannte Features.

## 1. Requirements & Constraints

- **REQ-001**: Jedes sichtbare Feature muss echte Funktionalität bieten — keine Dialoge mit "nicht implementiert" oder "Coming Soon"
- **REQ-002**: Keine Code-Duplikate zwischen Features (CollectionDiff≡DryRunCompare, PluginMarketplace≡PluginManager etc.)
- **REQ-003**: Feature-Namen müssen korrekt beschreiben was das Feature tut (kein "PDF-Report" für HTML-Output)
- **REQ-004**: Alle `IsPlanned=true` Features die nicht implementiert werden sollen, müssen aus dem Tool-Katalog entfernt werden — nicht nur versteckt
- **REQ-005**: Entfernte Features dürfen keine verwaisten i18n-Keys, toten Handler-Code oder ungenutzte FeatureService-Methoden hinterlassen
- **SEC-001**: Path-Traversal-Schutz in verbleibenden Features beibehalten (ToolImport, CustomDatEditor)
- **SEC-002**: CSV-Injection-Schutz in Export-Features beibehalten
- **CON-001**: Kern-Pipeline (RunOrchestrator) wird NICHT verändert — nur die UI-Tool-Karten-Schicht
- **CON-002**: Bestehende 5.400+ Tests müssen nach jeder Phase grün bleiben
- **CON-003**: API-Endpoints und CLI bleiben unverändert
- **CON-004**: Alle Änderungen in `src/RomCleanup.UI.Wpf/` — keine Core/Infrastructure/Contracts-Änderungen
- **GUD-001**: Nach Konsolidierung soll jede Tool-Kategorie maximal 8 Karten enthalten
- **GUD-002**: DefaultPinnedKeys aktualisieren — keine entfernten Tools in Schnellzugriff
- **PAT-001**: Entfernung = Tool-Registration in ToolsViewModel + Command-Registration in FeatureCommandService + Handler-Methode + FeatureService-Methode + i18n-Keys (alle 3 Sprachen) + MainViewModel.Filters-Registrierung

## 2. Implementation Steps

### Phase 1: Stubs und Blendwerk entfernen (6 Features)

- GOAL-001: Alle Features entfernen, die nur Platzhalter-Dialoge ("nicht implementiert", "Coming Soon", "in Planung") anzeigen

- [x] **TASK-001** — **FtpSource entfernen**: ToolItem-Registration in `src/RomCleanup.UI.Wpf/ViewModels/ToolsViewModel.cs` (Zeile ~213), IsPlanned-Zuweisung (Zeile ~229), Duplikat in `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.Filters.cs` (Zeile ~258, ~274). Command-Registration `cmds["FtpSource"]` in `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.cs`. Handler-Methode `FtpSource()` in `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.Infra.cs` (Zeile ~32). FeatureService-Methode `BuildFtpSourceReport()` in `src/RomCleanup.UI.Wpf/Services/FeatureService.Infra.cs`. i18n-Keys `Tool.FtpSource*` aus `data/i18n/de.json`, `en.json`, `fr.json` entfernen.
- [x] **TASK-002** — **CloudSync entfernen**: ToolItem (ToolsViewModel ~214, ~229), MainViewModel.Filters (~259, ~274). Command `cmds["CloudSync"]`, Handler `CloudSync()` in FeatureCommandService.Infra.cs (~63). FeatureService `BuildCloudSyncReport()` in FeatureService.Infra.cs. i18n-Keys `Tool.CloudSync*` aus allen 3 Sprachen.
- [x] **TASK-003** — **PluginMarketplaceFeature entfernen**: ToolItem (ToolsViewModel ~215, ~229), MainViewModel.Filters (~260, ~274). Command `cmds["PluginMarketplaceFeature"]`, Handler `PluginMarketplace()` in FeatureCommandService.Infra.cs (~75). FeatureService `GetPluginMarketplaceStatus()` in FeatureService.Infra.cs. i18n-Keys `Tool.PluginMarketplaceFeature*` aus allen 3 Sprachen.
- [x] **TASK-004** — **PluginManager entfernen**: ToolItem (ToolsViewModel ~216, ~229), MainViewModel.Filters (~261, ~274). Command `cmds["PluginManager"]` in FeatureCommandService.cs (~83). Handler `PluginManager()` in FeatureCommandService.cs (~461). FeatureService `GetInstalledPlugins()` in FeatureService.Infra.cs. i18n-Keys `Tool.PluginManager*` aus allen 3 Sprachen.
- [x] **TASK-005** — **GpuHashing entfernen**: ToolItem (ToolsViewModel ~161, ~230), MainViewModel.Filters (~206, ~275). Command `cmds["GpuHashing"]`, Handler `GpuHashing()` in FeatureCommandService.Conversion.cs (~110). FeatureService `BuildGpuHashingStatus()` + `ToggleGpuHashing()` in FeatureService.Conversion.cs. i18n-Keys `Tool.GpuHashing*`.
- [x] **TASK-006** — **ParallelHashing entfernen**: ToolItem (ToolsViewModel ~160, ~230), MainViewModel.Filters (~205, ~275). Command `cmds["ParallelHashing"]`, Handler `ParallelHashing()` in FeatureCommandService.Conversion.cs (~92). FeatureService `BuildParallelHashingReport()` in FeatureService.Conversion.cs. i18n-Keys `Tool.ParallelHashing*`. Referenz in MainViewModel.cs (~395) entfernen.
- [x] **TASK-007** — Build ausführen: `dotnet build src/RomCleanup.sln` — muss fehlerfrei kompilieren
- [x] **TASK-008** — Tests ausführen: `dotnet test src/RomCleanup.Tests/RomCleanup.Tests.csproj --nologo` — alle Tests müssen grün sein

> **Phase 1 abgeschlossen (2026-03-26):** 6 Stub-Features vollständig entfernt. 18 Dateien editiert. Build: 0 Fehler, 0 Warnungen. Tests: 3086 bestanden, 0 fehlgeschlagen, 6 übersprungen. Netto ~36 Tests entfernt (Stub-Alibi-Tests). Zusätzlich bereinigt: `FeatureCommandKeys.cs` (6 Konstanten), `IConversionEstimator.cs` + `ConversionEstimator.cs` (3 Interface-Methoden + Implementierungen), tote `PluginManager()`-Methode in FeatureCommandService.cs Hauptteil. `FeatureService.Dat.BuildFtpSourceReport()` bewusst erhalten (DAT-Layer, nicht Tool-FtpSource).

### Phase 2: Redundante Features entfernen (6 Features)

- GOAL-002: Alle Features entfernen, die 100% Code-Duplikate oder 95%+ Überlappung mit anderen Features haben

- [ ] **TASK-009** — **QuickPreview entfernen**: ToolItem (ToolsViewModel ~58, ~138), MainViewModel.Filters (~161, ~183). Command-Registration `cmds["QuickPreview"]` in FeatureCommandService.cs. Handler entfernen. **WICHTIG**: `QuickPreviewCommand` in MainViewModel.cs (~99, ~395) ist ein eigenständiger ICommand — diesen beibehalten und auf DryRun-Preset-Logik umleiten (`PresetSafeDryRunCommand` + `RunCommand`). Keyboard-Shortcut Ctrl+D in MainWindow.xaml auf `PresetSafeDryRunCommand` umverdrahten. Aus `DefaultPinnedKeys` entfernen (ToolsViewModel ~58). i18n-Keys `Tool.QuickPreview*`.
- [ ] **TASK-010** — **CollectionDiff entfernen**: ToolItem (ToolsViewModel ~140), MainViewModel.Filters (~185). Command `cmds["CollectionDiff"]` in FeatureCommandService.cs (~71). Handler `CollectionDiff()` in FeatureCommandService.Infra.cs. DryRunCompare (das identische Feature) bleibt. i18n-Keys `Tool.CollectionDiff*`.
- [ ] **TASK-011** — **ConvertQueue entfernen**: ToolItem (ToolsViewModel ~157, ~231), MainViewModel.Filters (~202, ~276). Command `cmds["ConvertQueue"]` in FeatureCommandService.cs (~102). Handler `ConvertQueue()` in FeatureCommandService.Conversion.cs (~51). FeatureService `BuildConvertQueueReport()` in FeatureService.Conversion.cs. i18n-Keys `Tool.ConvertQueue*`. ConversionPipeline bleibt als konsolidiertes Feature.
- [ ] **TASK-012** — **ConversionEstimate entfernen** (als separates Tool): ToolItem (ToolsViewModel ~142), MainViewModel.Filters (~187). Command `cmds["ConversionEstimate"]` in FeatureCommandService.cs (~87). Handler `ConversionEstimate()` in FeatureCommandService.Analysis.cs (~18) und FeatureCommandService.Conversion.cs (~18/~22). **WICHTIG**: `FeatureService.GetConversionEstimate()` wird von ConversionPipeline weiter genutzt — NUR die Tool-Karte entfernen, nicht die FeatureService-Methode. i18n-Keys `Tool.ConversionEstimate*`.
- [ ] **TASK-013** — **TosecDat entfernen**: ToolItem (ToolsViewModel ~166, ~231), MainViewModel.Filters (~211, ~276). Command `cmds["TosecDat"]` in FeatureCommandService.cs (~111). Handler `TosecDat()` in FeatureCommandService.Dat.cs (~281). ToolImport deckt denselben Use-Case ab. i18n-Keys `Tool.TosecDat*`.
- [ ] **TASK-014** — **SplitPanelPreview entfernen**: ToolItem (ToolsViewModel ~192), MainViewModel.Filters (~237). Command `cmds["SplitPanelPreview"]`, Handler `SplitPanelPreview()` in FeatureCommandService.Workflow.cs. FeatureService Methoden für Split-Panel-Formatierung. i18n-Keys `Tool.SplitPanelPreview*`.
- [ ] **TASK-015** — Build + Tests ausführen — alle grün

### Phase 3: Qualitativ unzureichende Features entfernen (3 Features)

- GOAL-003: Features entfernen, die unzuverlässige oder keine echte Funktionalität bieten

- [ ] **TASK-016** — **PlaytimeTracker entfernen**: ToolItem (ToolsViewModel ~175, ~232), MainViewModel.Filters (~220, ~277). Command `cmds["PlaytimeTracker"]`, Handler `PlaytimeTracker()` in FeatureCommandService.Collection.cs (~105). i18n-Keys `Tool.PlaytimeTracker*`. Grund: Zählt nur .lrtl-Dateien statt Spielzeiten zu parsen.
- [ ] **TASK-017** — **GenreClassification entfernen**: ToolItem (ToolsViewModel ~174, ~232), MainViewModel.Filters (~219, ~277). Command `cmds["GenreClassification"]`, Handler `GenreClassification()` in FeatureCommandService.Collection.cs (~88). i18n-Keys `Tool.GenreClassification*`. Grund: Keyword-Regex auf Dateinamen ist unzuverlässig.
- [ ] **TASK-018** — **EmulatorCompat entfernen**: ToolItem (ToolsViewModel ~152, ~232), MainViewModel.Filters (~197, ~277). Command `cmds["EmulatorCompat"]` in FeatureCommandService.cs (~97). Handler `EmulatorCompat()` in FeatureCommandService.Analysis.cs (~246). FeatureService `FormatEmulatorCompat()`. i18n-Keys `Tool.EmulatorCompat*`. Grund: Statische hardcodierte Matrix ohne ROM-Bezug — Inhalt in docs/USER_HANDBOOK.md verschieben.
- [ ] **TASK-019** — Emulator-Kompatibilitäts-Matrix als Referenz-Tabelle in `docs/USER_HANDBOOK.md` einfügen (den Inhalt aus `FormatEmulatorCompat()` übernehmen, als Markdown-Tabelle)
- [ ] **TASK-020** — Build + Tests ausführen — alle grün

### Phase 4: Features deaktivieren / ausblenden (5 Features)

- GOAL-004: Sichtbare aber unfertige Features aus dem Standard-Katalog nehmen, indem sie komplett aus der Tool-Registrierung entfernt werden (statt nur IsPlanned=true, da "geplant" immer noch sichtbar ist)

- [ ] **TASK-021** — **CoverScraper aus Katalog entfernen**: ToolItem (ToolsViewModel), MainViewModel.Filters. Command + Handler in FeatureCommandService.Collection.cs (~39). FeatureService-Methoden BEIBEHALTEN (für späteres Epic). Nur die UI-Sichtbarkeit entfernen. i18n-Keys entfernen.
- [ ] **TASK-022** — **CollectionSharing aus Katalog entfernen**: ToolItem, MainViewModel.Filters. Command + Handler in FeatureCommandService.Collection.cs (~124). Grund: Export-only, kein Import-Gegenstück.
- [ ] **TASK-023** — **TrendAnalysis aus Katalog entfernen**: ToolItem, MainViewModel.Filters. Command + Handler in FeatureCommandService.Analysis.cs. Grund: Kein Auto-Snapshot nach Runs.
- [ ] **TASK-024** — **WindowsContextMenu aus Katalog entfernen**: ToolItem, MainViewModel.Filters. Command + Handler in FeatureCommandService.Infra.cs (~155). Grund: Generiert .reg ohne Auto-Import.
- [ ] **TASK-025** — **DockerContainer aus Katalog entfernen**: ToolItem, MainViewModel.Filters. Command + Handler in FeatureCommandService.Infra.cs (~136). Grund: Template-Generierung ohne Build/Deploy.
- [ ] **TASK-026** — Build + Tests ausführen — alle grün

### Phase 5: Irreführende Benennungen korrigieren (5 Features)

- GOAL-005: Feature-Namen und Beschreibungen korrigieren, sodass sie korrekt beschreiben was das Feature tut

- [ ] **TASK-027** — **PdfReport → HtmlReport umbenennen**: ToolItem Key `PdfReport` → `HtmlReport` in ToolsViewModel. Command-Key `cmds["PdfReport"]` → `cmds["HtmlReport"]` in FeatureCommandService.Export.cs. i18n-Keys `Tool.PdfReport` → `Tool.HtmlReport` in allen 3 Sprachen. DisplayName in de.json: "HTML-Report", en.json: "HTML Report", fr.json: "Rapport HTML". Icon von 📄 auf 🌐 ändern.
- [ ] **TASK-028** — **MobileWebUI → ApiServer umbenennen**: ToolItem Key `MobileWebUI` → `ApiServer` in ToolsViewModel. Command-Key in FeatureCommandService. i18n DisplayName in de.json: "API-Server starten", en.json: "Start API Server", fr.json: "Démarrer le serveur API".
- [ ] **TASK-029** — **SchedulerAdvanced → CronTester umbenennen + Beschreibung korrigieren**: ToolItem Key `SchedulerAdvanced` → `CronTester` in ToolsViewModel. i18n DisplayName: "Cron-Tester" (de), "Cron Tester" (en), "Testeur Cron" (fr). Beschreibung anpassen: "Cron-Ausdrücke testen und validieren" statt "Automatische Zeitplanung".
- [ ] **TASK-030** — **HardlinkMode Beschreibung korrigieren**: i18n-Beschreibung in allen Sprachen auf "Speicher-Einsparung durch Hardlinks schätzen" ändern (statt "Hardlinks erstellen"). IsPlanned-Flag NICHT setzen — bleibt als Info-Feature.
- [ ] **TASK-031** — **SystemTray aus Tools-Katalog in Einstellungen verschieben**: ToolItem aus ToolsViewModel entfernen. Die `ToggleSystemTray()`-Funktionalität bleibt als Setting in der Allgemein-Sektion erhalten. i18n-Keys `Tool.SystemTray*` entfernen.
- [ ] **TASK-032** — Build + Tests ausführen — alle grün

### Phase 6: Features konsolidieren (3 Gruppen)

- GOAL-006: Redundante Feature-Gruppen zu je einer Karte mit Unteroptionen zusammenführen

- [ ] **TASK-033** — **Duplikat-Analyse konsolidieren**: DuplicateInspector + DuplicateHeatmap + CrossRootDupe zu einer Karte "DuplicateAnalysis" zusammenführen. Neues ToolItem `DuplicateAnalysis` in ToolsViewModel mit Kategorie "Analyse". Neuer Handler `DuplicateAnalysis()` in FeatureCommandService.Analysis.cs, der einen Dialog mit 3 Tabs/Abschnitten zeigt: (1) Verzeichnis-Analyse (alter DuplicateInspector-Code), (2) Konsolen-Heatmap (alter DuplicateHeatmap-Code), (3) Cross-Root (alter CrossRootDupe-Code). Alte 3 separate Tool-Registrierungen entfernen. `DefaultPinnedKeys`: `DuplicateInspector` → `DuplicateAnalysis`.
- [ ] **TASK-034** — **Export konsolidieren**: ExportCsv + ExportExcel + DuplicateExport zu einer Karte "ExportCollection" zusammenführen. Neuer Handler `ExportCollection()` mit Format-Auswahl-Dialog (CSV / Excel XML / Duplikate-CSV). Alte 3 separate Tool-Registrierungen entfernen. `DefaultPinnedKeys`: `ExportCsv` → `ExportCollection`. **WICHTIG**: Die separaten FeatureService-Methoden (`ExportCollectionCsv`, `ExportExcelXml`) beibehalten — nur die UI-Karten zusammenführen.
- [ ] **TASK-035** — **DAT-Import konsolidieren**: ToolImport bleibt als einziger DAT-Import (TosecDat bereits in Phase 2 entfernt). ToolImport umbenennen zu `DatImport` — klarerer Name. ToolItem-Key `ToolImport` → `DatImport`. i18n aktualisieren.
- [ ] **TASK-036** — Build + Tests ausführen — alle grün

### Phase 7: CommandPalette reparieren

- GOAL-007: CommandPalette von 8 auf alle verfügbaren Tool-Commands erweitern

- [ ] **TASK-037** — `FeatureService.SearchCommands()` refaktorieren: Statt hardcodierter 8-Command-Liste die Tool-Registrierungen aus dem FeatureCommandService dynamisch auslesen. Jeder registrierte Command-Key soll durchsuchbar sein. Fuzzy-Matching (Levenshtein) beibehalten.
- [ ] **TASK-038** — `FeatureCommandService.ExecuteCommand()` switch-Statement durch Dictionary-Lookup auf `FeatureCommands` ersetzen. Wenn Command-Key im Dictionary existiert → `((ICommand)cmd).Execute(null)`.
- [ ] **TASK-039** — Build + Tests ausführen — alle grün

### Phase 8: DefaultPinnedKeys und Kategorien bereinigen

- GOAL-008: Schnellzugriff, Kategorien und Tool-Zähler aktualisieren

- [ ] **TASK-040** — `DefaultPinnedKeys` in ToolsViewModel aktualisieren: Entfernte Tools raus, konsolidierte Tools rein. Neue Liste: `HealthScore`, `DuplicateAnalysis`, `RollbackQuick`, `ExportCollection`, `DatAutoUpdate`, `ConversionPipeline`.
- [ ] **TASK-041** — Tool-Kategorien prüfen und leere Kategorien entfernen. Nach Bereinigung sollte jede Kategorie 3-8 Tools enthalten. Kategorien mit <3 Tools mit benachbarter Kategorie zusammenlegen.
- [ ] **TASK-042** — i18n-Dateien auf verwaiste Keys prüfen: Alle `Tool.*`-Keys in `data/i18n/de.json`, `en.json`, `fr.json` gegen die verbleibenden ToolItem-Keys abgleichen. Verwaiste Keys entfernen.
- [ ] **TASK-043** — `data/ui-lookups.json` auf verwaiste Einträge prüfen und bereinigen.
- [ ] **TASK-044** — Build + Tests ausführen — alle grün

### Phase 9: Toter Code in FeatureService bereinigen

- GOAL-009: Nicht mehr referenzierte Methoden aus FeatureService-Partials entfernen

- [ ] **TASK-045** — **FeatureService.Infra.cs bereinigen**: `BuildPluginMarketplaceReport()` bereits in Phase 1 entfernt. Verbleibend prüfen: `BuildCloudSyncReport()`, `GetPluginMarketplaceStatus()`, `GetInstalledPlugins()` — falls noch vorhanden, entfernen.
- [ ] **TASK-046** — **FeatureService.Conversion.cs bereinigen**: `BuildGpuHashingStatus()`, `ToggleGpuHashing()`, `BuildParallelHashingReport()` bereits in Phase 1 entfernt. Verbleibend: `BuildConvertQueueReport()` entfernen (Aufrufer wird in Phase 2 entfernt). `GetConversionEstimate()` BEIBEHALTEN (wird von ConversionPipeline genutzt).
- [ ] **TASK-047** — **FeatureService.Analysis.cs bereinigen**: `FormatEmulatorCompat()` entfernen (Inhalt in Phase 3 nach USER_HANDBOOK.md migriert).
- [ ] **TASK-048** — **FeatureService.Collection.cs bereinigen**: Methoden für PlaytimeTracker und GenreClassification entfernen falls vorhanden. CoverScraper- und CollectionSharing-Methoden BEIBEHALTEN (spätere Epics).
- [ ] **TASK-049** — **FeatureService.Workflow.cs bereinigen**: SplitPanel-Formatierungsmethoden entfernen.
- [ ] **TASK-050** — Compiler-Warnungen prüfen: `dotnet build src/RomCleanup.sln -warnaserror` auf unreferenzierte Methoden/Variablen prüfen.
- [ ] **TASK-051** — Finaler Test-Lauf: `dotnet test src/RomCleanup.Tests/RomCleanup.Tests.csproj --nologo` — alle Tests müssen grün sein.

### Phase 10: Tests für Konsolidierung

- GOAL-010: Sicherstellen, dass die konsolidierten Features korrekt funktionieren und keine Regressionen entstehen

- [ ] **TASK-052** — Neue Tests in `src/RomCleanup.Tests/FeatureCommandServiceTests.cs` hinzufügen: Test dass `DuplicateAnalysis`-Command alle 3 Sektionen (Inspector, Heatmap, CrossRoot) ausgibt.
- [ ] **TASK-053** — Test dass `ExportCollection`-Command alle 3 Formate (CSV, Excel, DuplicateCSV) korrekt exportiert.
- [ ] **TASK-054** — Test dass `CommandPalette` alle registrierten Tool-Keys findet (statt nur 8). Fuzzy-Search-Test mit Levenshtein ≤3 beibehalten.
- [ ] **TASK-055** — Test dass `DefaultPinnedKeys` keine entfernten Tool-Keys referenzieren.
- [ ] **TASK-056** — Negativtest: Entfernte Tool-Keys (`FtpSource`, `CloudSync`, `GpuHashing`, etc.) dürfen nicht im `FeatureCommands`-Dictionary existieren.
- [ ] **TASK-057** — i18n-Konsistenztest: Alle ToolItem-Keys müssen korrespondierende i18n-Einträge in allen 3 Sprachen haben.
- [ ] **TASK-058** — Finaler vollständiger Test-Lauf mit `dotnet test src/RomCleanup.sln --nologo`

## 3. Alternatives

- **ALT-001**: Features nur als `IsPlanned=true` markieren statt entfernen — **abgelehnt**, weil geplante Features immer noch im Katalog sichtbar sind und Nutzer verwirren. Komplette Entfernung ist sauberer.
- **ALT-002**: Alle Features behalten und nur die Stubs implementieren — **abgelehnt**, weil FTP, Cloud, Plugin-System, GPU-Hashing substantielle Eigenentwicklungen sind die nicht zum Kern-Produkt gehören (ROM-Cleanup ≠ ROM-Launcher ≠ Cloud-Platform).
- **ALT-003**: Features in ein separates "Labs"-Tab verschieben — **abgelehnt**, weil dies die Komplexität erhöht statt reduziert. Ein "Labs"-Tab legitimiert Halbfertiges.
- **ALT-004**: ConversionEstimate als Sub-Tab in ConversionPipeline statt eigene Entfernung — **abgelehnt**, ConversionPipeline zeigt bereits den Estimate als integralen Bestandteil.

## 4. Dependencies

- **DEP-001**: `dotnet build` muss nach jeder Phase erfolgreich sein (kein toter Code mit Compile-Fehlern)
- **DEP-002**: `dotnet test` mit 5.400+ Tests muss nach jeder Phase grün sein
- **DEP-003**: Phase 2 hängt von Phase 1 ab (PluginManager-Entfernung muss vor Konsolidierung geschehen)
- **DEP-004**: Phase 6 (Konsolidierung) hängt von Phase 2 (Redundante entfernen) ab — ConvertQueue muss weg sein bevor ConversionPipeline konsolidiert wird
- **DEP-005**: Phase 9 (Toter Code) hängt von Phase 1-6 ab — erst alle Aufrufer entfernen, dann Methoden
- **DEP-006**: Phase 10 (Tests) hängt von Phase 6-9 ab — konsolidierte Features müssen existieren

## 5. Files

**Primäre Dateien (werden stark verändert):**

- **FILE-001**: `src/RomCleanup.UI.Wpf/ViewModels/ToolsViewModel.cs` — Tool-Katalog-Registrierung, DefaultPinnedKeys, Kategorien (~350 Zeilen, wird auf ~250 reduziert)
- **FILE-002**: `src/RomCleanup.UI.Wpf/ViewModels/MainViewModel.Filters.cs` — Duplikat-Registrierungen und IsPlanned-Zuweisungen (wird parallel bereinigt)
- **FILE-003**: `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.cs` — Haupt-Registrierung (513 Zeilen, ~15 Registrierungen entfernen)
- **FILE-004**: `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.Analysis.cs` — Handler (251 Zeilen, ~3 Handler entfernen, 1 konsolidieren)
- **FILE-005**: `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.Conversion.cs` — Handler (137 Zeilen, ~3 Handler entfernen)
- **FILE-006**: `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.Dat.cs` — Handler (366 Zeilen, ~1 Handler entfernen)
- **FILE-007**: `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.Collection.cs` — Handler (143 Zeilen, ~4 Handler entfernen)
- **FILE-008**: `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.Workflow.cs` — Handler (177 Zeilen, ~2 Handler entfernen)
- **FILE-009**: `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.Infra.cs` — Handler (292 Zeilen, ~5 Handler entfernen)
- **FILE-010**: `src/RomCleanup.UI.Wpf/Services/FeatureCommandService.Export.cs` — Handler (88 Zeilen, Export-Konsolidierung)

**FeatureService-Dateien (toter Code entfernen):**

- **FILE-011**: `src/RomCleanup.UI.Wpf/Services/FeatureService.Infra.cs` — 4 Methoden entfernen
- **FILE-012**: `src/RomCleanup.UI.Wpf/Services/FeatureService.Conversion.cs` — 3 Methoden entfernen
- **FILE-013**: `src/RomCleanup.UI.Wpf/Services/FeatureService.Analysis.cs` — 1 Methode entfernen
- **FILE-014**: `src/RomCleanup.UI.Wpf/Services/FeatureService.Collection.cs` — 2 Methoden entfernen
- **FILE-015**: `src/RomCleanup.UI.Wpf/Services/FeatureService.Workflow.cs` — SplitPanel-Methoden entfernen

**i18n-Dateien:**

- **FILE-016**: `data/i18n/de.json` — ~20 Tool-Keys entfernen, ~5 umbenennen
- **FILE-017**: `data/i18n/en.json` — ~20 Tool-Keys entfernen, ~5 umbenennen
- **FILE-018**: `data/i18n/fr.json` — ~20 Tool-Keys entfernen, ~5 umbenennen

**UI-Dateien:**

- **FILE-019**: `data/ui-lookups.json` — Verwaiste Einträge bereinigen
- **FILE-020**: `src/RomCleanup.UI.Wpf/MainWindow.xaml` — Ctrl+D Shortcut umverdrahten

**Dokumentation:**

- **FILE-021**: `docs/USER_HANDBOOK.md` — Emulator-Kompatibilitätsmatrix als Referenz einfügen

**Test-Dateien:**

- **FILE-022**: `src/RomCleanup.Tests/FeatureCommandServiceTests.cs` — Neue Tests für konsolidierte Features
- **FILE-023**: `src/RomCleanup.Tests/FeatureServiceTests.cs` — Bestehende Tests anpassen (entfernte Methoden)

## 6. Testing

- **TEST-001**: Nach jeder Phase: `dotnet build src/RomCleanup.sln` kompiliert fehlerfrei
- **TEST-002**: Nach jeder Phase: `dotnet test src/RomCleanup.Tests/RomCleanup.Tests.csproj --nologo` — 5.400+ Tests grün
- **TEST-003**: Neuer Test: `DuplicateAnalysis_ShowsAllThreeSections` — konsolidierter Duplikat-Dialog enthält Inspector-, Heatmap- und CrossRoot-Abschnitte
- **TEST-004**: Neuer Test: `ExportCollection_SupportsAllFormats` — CSV, Excel XML, Duplicate CSV Export alle funktional
- **TEST-005**: Neuer Test: `CommandPalette_FindsAllRegisteredTools` — Verifikation dass alle Tool-Keys auffindbar sind
- **TEST-006**: Neuer Test: `DefaultPinnedKeys_AllExistInCatalog` — kein Pinned-Key zeigt auf entferntes Tool
- **TEST-007**: Neuer Test: `RemovedTools_NotInFeatureCommands` — entfernte Keys (FtpSource, CloudSync, etc.) sind nicht im Dictionary
- **TEST-008**: Neuer Test: `I18nKeys_MatchToolItemKeys` — jedes ToolItem hat korrespondierende i18n-Einträge in de/en/fr
- **TEST-009**: Regressionstests: Alle bestehenden FeatureCommandServiceTests und FeatureServiceTests müssen weiterhin grün sein (Referenzen auf entfernte Features werden entfernt/angepasst)
- **TEST-010**: Final: `dotnet test src/RomCleanup.sln --nologo` — vollständige Suite

## 7. Risks & Assumptions

- **RISK-001**: Entfernte Features hinterlassen verwaiste Test-Referenzen — **Mitigation**: Phase 10 prüft explizit auf Compile-Fehler in Tests
- **RISK-002**: i18n-Keys könnten an unerwarteten Stellen referenziert werden (XAML Bindings, ResourceDictionary) — **Mitigation**: Grep-Suche nach jedem entfernten Key in allen .xaml/.cs/.json Dateien
- **RISK-003**: ConsolidatedFeatures (DuplicateAnalysis, ExportCollection) könnten UX-Regression verursachen — **Mitigation**: Tests für Konsolidierung in Phase 10
- **RISK-004**: DefaultPinnedKeys verweisen auf entfernte Keys → Laufzeit-NullRef — **Mitigation**: TASK-040 und TEST-006 adressieren dies explizit
- **RISK-005**: MainViewModel.Filters.cs enthält Tool-Registrierungen parallel zu ToolsViewModel — **Mitigation**: Beide Dateien werden synchron bereinigt in jeder Phase
- **ASSUMPTION-001**: Die Kern-Pipeline (RunOrchestrator, Core-Logik, Infrastructure-Services) wird durch diese Änderungen NICHT beeinflusst — nur UI-Schicht
- **ASSUMPTION-002**: FeatureService-Methoden die von entfernten Handlern aufgerufen werden, werden nicht von anderen Stellen referenziert (Compiler-Check in Phase 9 verifiziert dies)
- **ASSUMPTION-003**: Die 5.400+ bestehenden Tests decken ausreichend ab, dass keine Kernfunktionalität durch UI-Bereinigung bricht

## 8. Related Specifications / Further Reading

- Feature-Audit-Ergebnisse aus der vorherigen Chat-Konversation (2026-03-26)
- [docs/USER_HANDBOOK.md](docs/USER_HANDBOOK.md) — Ziel für migrierte Emulator-Kompatibilitätsmatrix
- [.github/copilot-instructions.md](.github/copilot-instructions.md) — Projektweite Architektur- und Sicherheitsregeln
- [.claude/rules/cleanup.instructions.md](.claude/rules/cleanup.instructions.md) — Coding Guidelines (Stabilität vor Feature-Hype)
