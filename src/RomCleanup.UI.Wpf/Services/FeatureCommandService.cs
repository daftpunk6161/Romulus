using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Reporting;
using RomCleanup.Infrastructure.Tools;
using RomCleanup.UI.Wpf.ViewModels;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>
/// TASK-111: All feature button logic extracted from MainWindow code-behind.
/// Each method maps 1:1 to a former On* event handler.
/// Commands are exposed via MainViewModel.FeatureCommands dictionary.
/// </summary>
public sealed class FeatureCommandService
{
    private readonly MainViewModel _vm;
    private readonly SettingsService _settings;
    private readonly IDialogService _dialog;
    private readonly IWindowHost? _windowHost;

    public FeatureCommandService(MainViewModel vm, SettingsService settings, IDialogService dialog, IWindowHost? windowHost = null)
    {
        _vm = vm;
        _settings = settings;
        _dialog = dialog;
        _windowHost = windowHost;
    }

    public void RegisterCommands()
    {
        var cmds = _vm.FeatureCommands;

        // ── Functional buttons ──────────────────────────────────────────
        cmds["ExportLog"] = new RelayCommand(ExportLog);
        cmds["ProfileDelete"] = new RelayCommand(ProfileDelete);
        cmds["ProfileImport"] = new RelayCommand(ProfileImport);
        cmds["ConfigDiff"] = new RelayCommand(ConfigDiff);
        cmds["ExportUnified"] = new RelayCommand(ExportUnified);
        cmds["ConfigImport"] = new RelayCommand(ConfigImport);
        cmds["AutoFindTools"] = new RelayCommand(async () => await AutoFindToolsAsync());

        // ── Konfiguration tab misc ──────────────────────────────────────
        cmds["HealthScore"] = new RelayCommand(HealthScore);
        cmds["CollectionDiff"] = new RelayCommand(CollectionDiff);
        cmds["DuplicateInspector"] = new RelayCommand(DuplicateInspector);
        cmds["DuplicateExport"] = new RelayCommand(DuplicateExport);
        cmds["ExportCsv"] = new RelayCommand(ExportCsv);
        cmds["ExportExcel"] = new RelayCommand(ExportExcel);
        cmds["RollbackUndo"] = new RelayCommand(RollbackUndo);
        cmds["RollbackRedo"] = new RelayCommand(RollbackRedo);
        cmds["ApplyLocale"] = new RelayCommand(ApplyLocale);
        cmds["PluginManager"] = new RelayCommand(PluginManager);
        cmds["AutoProfile"] = new RelayCommand(AutoProfile);

        // ── Analyse & Berichte ──────────────────────────────────────────
        cmds["ConversionEstimate"] = new RelayCommand(ConversionEstimate);
        cmds["JunkReport"] = new RelayCommand(JunkReport);
        cmds["RomFilter"] = new RelayCommand(RomFilter);
        cmds["DuplicateHeatmap"] = new RelayCommand(DuplicateHeatmap);
        cmds["MissingRom"] = new RelayCommand(MissingRom);
        cmds["CrossRootDupe"] = new RelayCommand(CrossRootDupe);
        cmds["HeaderAnalysis"] = new RelayCommand(HeaderAnalysis);
        cmds["Completeness"] = new RelayCommand(Completeness);
        cmds["DryRunCompare"] = new RelayCommand(DryRunCompare);
        cmds["TrendAnalysis"] = new RelayCommand(TrendAnalysis);
        cmds["EmulatorCompat"] = new RelayCommand(EmulatorCompat);

        // ── Konvertierung & Hashing ─────────────────────────────────────
        cmds["ConversionPipeline"] = new RelayCommand(ConversionPipeline);
        cmds["NKitConvert"] = new RelayCommand(NKitConvert);
        cmds["ConvertQueue"] = new RelayCommand(ConvertQueue);
        cmds["ConversionVerify"] = new RelayCommand(ConversionVerify);
        cmds["FormatPriority"] = new RelayCommand(FormatPriority);
        cmds["ParallelHashing"] = new RelayCommand(ParallelHashing);
        cmds["GpuHashing"] = new RelayCommand(GpuHashing);

        // ── DAT & Verifizierung ─────────────────────────────────────────
        cmds["DatAutoUpdate"] = new RelayCommand(DatAutoUpdate);
        cmds["DatDiffViewer"] = new RelayCommand(DatDiffViewer);
        cmds["TosecDat"] = new RelayCommand(TosecDat);
        cmds["CustomDatEditor"] = new RelayCommand(CustomDatEditor);
        cmds["HashDatabaseExport"] = new RelayCommand(HashDatabaseExport);

        // ── Sammlungsverwaltung ─────────────────────────────────────────
        cmds["CollectionManager"] = new RelayCommand(CollectionManager);
        cmds["CloneListViewer"] = new RelayCommand(CloneListViewer);
        cmds["CoverScraper"] = new RelayCommand(CoverScraper);
        cmds["GenreClassification"] = new RelayCommand(GenreClassification);
        cmds["PlaytimeTracker"] = new RelayCommand(PlaytimeTracker);
        cmds["CollectionSharing"] = new RelayCommand(CollectionSharing);
        cmds["VirtualFolderPreview"] = new RelayCommand(VirtualFolderPreview);

        // ── Sicherheit & Integrität ─────────────────────────────────────
        cmds["IntegrityMonitor"] = new RelayCommand(async () => await IntegrityMonitorAsync());
        cmds["BackupManager"] = new RelayCommand(BackupManager);
        cmds["Quarantine"] = new RelayCommand(Quarantine);
        cmds["RuleEngine"] = new RelayCommand(RuleEngine);
        cmds["PatchEngine"] = new RelayCommand(PatchEngine);
        cmds["HeaderRepair"] = new RelayCommand(HeaderRepair);

        // ── Workflow & Automatisierung ───────────────────────────────────
        cmds["SplitPanelPreview"] = new RelayCommand(SplitPanelPreview);
        cmds["FilterBuilder"] = new RelayCommand(FilterBuilder);
        cmds["SortTemplates"] = new RelayCommand(SortTemplates);
        cmds["PipelineEngine"] = new RelayCommand(PipelineEngine);
        cmds["SchedulerAdvanced"] = new RelayCommand(SchedulerAdvanced);
        cmds["RulePackSharing"] = new RelayCommand(RulePackSharing);
        cmds["ArcadeMergeSplit"] = new RelayCommand(ArcadeMergeSplit);

        // ── Export & Integration ────────────────────────────────────────
        cmds["PdfReport"] = new RelayCommand(PdfReport);
        cmds["LauncherIntegration"] = new RelayCommand(LauncherIntegration);
        cmds["ToolImport"] = new RelayCommand(ToolImport);

        // ── Infrastruktur & Deployment ──────────────────────────────────
        cmds["StorageTiering"] = new RelayCommand(StorageTiering);
        cmds["NasOptimization"] = new RelayCommand(NasOptimization);
        cmds["FtpSource"] = new RelayCommand(FtpSource);
        cmds["CloudSync"] = new RelayCommand(CloudSync);
        cmds["PluginMarketplaceFeature"] = new RelayCommand(PluginMarketplace);
        cmds["PortableMode"] = new RelayCommand(PortableMode);
        cmds["DockerContainer"] = new RelayCommand(DockerContainer);
        cmds["WindowsContextMenu"] = new RelayCommand(WindowsContextMenu);
        cmds["HardlinkMode"] = new RelayCommand(HardlinkMode);
        cmds["MultiInstanceSync"] = new RelayCommand(MultiInstanceSync);

        // ── Window-level commands (need IWindowHost) ────────────────────
        if (_windowHost is not null)
        {
            cmds["CommandPalette"] = new RelayCommand(CommandPalette);
            cmds["SystemTray"] = new RelayCommand(() => _windowHost.ToggleSystemTray());
            cmds["MobileWebUI"] = new RelayCommand(MobileWebUI);
            cmds["Accessibility"] = new RelayCommand(Accessibility);
            cmds["ThemeEngine"] = new RelayCommand(ThemeEngine);
        }
    }

    // ═══ FUNCTIONAL BUTTONS ═════════════════════════════════════════════

    private void ExportLog()
    {
        var path = _dialog.SaveFile("Log exportieren", "Textdateien (*.txt)|*.txt|Alle (*.*)|*.*", "log-export.txt");
        if (path is null) return;
        try
        {
            var lines = _vm.LogEntries.Select(entry => $"[{entry.Level}] {entry.Text}");
            File.WriteAllLines(path, lines);
            _vm.AddLog($"Log exportiert: {path}", "INFO");
        }
        catch (Exception ex)
        { _vm.AddLog($"Log-Export fehlgeschlagen: {ex.Message}", "ERROR"); }
    }

    private void ProfileDelete()
    {
        if (!_dialog.Confirm("Gespeicherte Einstellungen wirklich löschen?", "Profil löschen")) return;
        if (ProfileService.Delete()) _vm.AddLog("Profil gelöscht.", "INFO");
        else _vm.AddLog("Kein gespeichertes Profil gefunden.", "WARN");
    }

    private void ProfileImport()
    {
        var path = _dialog.BrowseFile("Profil importieren", "JSON (*.json)|*.json");
        if (path is null) return;
        try
        {
            ProfileService.Import(path);
            _settings.LoadInto(_vm);
            _vm.RefreshStatus();
            _vm.AddLog($"Profil importiert: {Path.GetFileName(path)}", "INFO");
        }
        catch (JsonException) { _vm.AddLog("Import fehlgeschlagen: Ungültiges JSON-Format.", "ERROR"); }
        catch (Exception ex) { _vm.AddLog($"Import fehlgeschlagen: {ex.Message}", "ERROR"); }
    }

    private void ConfigDiff()
    {
        var current = _vm.GetCurrentConfigMap();
        var saved = ProfileService.LoadSavedConfigFlat();
        if (saved is null)
        { _dialog.Info("Keine gespeicherte Konfiguration zum Vergleichen vorhanden.", "Config-Diff"); return; }
        var diffs = FeatureService.GetConfigDiff(current, saved);
        if (diffs.Count == 0)
        { _dialog.Info("Keine Unterschiede zwischen aktueller und gespeicherter Konfiguration.", "Config-Diff"); return; }
        var sb = new StringBuilder();
        sb.AppendLine("Config-Diff (Aktuell vs. Gespeichert):\n");
        foreach (var d in diffs)
            sb.AppendLine($"  {d.Key}: \"{d.SavedValue}\" → \"{d.CurrentValue}\"");
        _dialog.ShowText("Config-Diff", sb.ToString());
    }

    private void ExportUnified()
    {
        var path = _dialog.SaveFile("Konfiguration exportieren", "JSON (*.json)|*.json", "romcleanup-config.json");
        if (path is null) return;
        try
        {
            ProfileService.Export(path, _vm.GetCurrentConfigMap());
            _vm.AddLog($"Konfiguration exportiert: {path} — Hinweis: Enthält lokale Pfade (Roots, ToolPaths). Vor dem Teilen prüfen.", "INFO");
        }
        catch (Exception ex) { _vm.AddLog($"Export fehlgeschlagen: {ex.Message}", "ERROR"); }
    }

    private void ConfigImport()
    {
        var path = _dialog.BrowseFile("Konfiguration importieren", "JSON (*.json)|*.json");
        if (path is null) return;
        try
        {
            ProfileService.Import(path);
            _settings.LoadInto(_vm);
            _vm.RefreshStatus();
            _vm.AddLog($"Konfiguration importiert: {Path.GetFileName(path)}", "INFO");
        }
        catch (JsonException) { _vm.AddLog("Import fehlgeschlagen: Ungültiges JSON-Format.", "ERROR"); }
        catch (Exception ex) { _vm.AddLog($"Import fehlgeschlagen: {ex.Message}", "ERROR"); }
    }

    private async Task AutoFindToolsAsync()
    {
        _vm.AddLog("Suche nach Tools…", "INFO");
        var results = await Task.Run(() =>
        {
            var runner = new ToolRunnerAdapter(null);
            return new Dictionary<string, string?>
            {
                ["chdman"] = runner.FindTool("chdman"),
                ["dolphintool"] = runner.FindTool("dolphintool"),
                ["7z"] = runner.FindTool("7z"),
                ["psxtract"] = runner.FindTool("psxtract"),
                ["ciso"] = runner.FindTool("ciso")
            };
        });
        int found = 0;
        if (!string.IsNullOrEmpty(results["chdman"])) { _vm.ToolChdman = results["chdman"]!; found++; }
        if (!string.IsNullOrEmpty(results["dolphintool"])) { _vm.ToolDolphin = results["dolphintool"]!; found++; }
        if (!string.IsNullOrEmpty(results["7z"])) { _vm.Tool7z = results["7z"]!; found++; }
        if (!string.IsNullOrEmpty(results["psxtract"])) { _vm.ToolPsxtract = results["psxtract"]!; found++; }
        if (!string.IsNullOrEmpty(results["ciso"])) { _vm.ToolCiso = results["ciso"]!; found++; }
        _vm.AddLog($"Tool-Suche abgeschlossen: {found} von 5 gefunden.", found > 0 ? "INFO" : "WARN");
        _vm.RefreshStatus();
    }

    // ═══ KONFIGURATION TAB ══════════════════════════════════════════════

    private void HealthScore()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten, um den Health-Score zu berechnen.", "WARN"); return; }
        var total = _vm.LastCandidates.Count;
        var dupes = _vm.LastDedupeGroups.Sum(g => g.Losers.Count);
        var junk = _vm.LastCandidates.Count(c => c.Category == "JUNK");
        var verified = _vm.LastCandidates.Count(c => c.DatMatch);
        var score = FeatureService.CalculateHealthScore(total, dupes, junk, verified);
        _vm.HealthScore = $"{score}%";
        _dialog.ShowText("Health-Score", $"Sammlungs-Gesundheit: {score}/100\n\n" +
            $"Dateien: {total}\nDuplikate: {dupes} ({100.0 * dupes / total:F1}%)\n" +
            $"Junk: {junk} ({100.0 * junk / total:F1}%)\nVerifiziert: {verified} ({100.0 * verified / total:F1}%)");
    }

    private void CollectionDiff()
    {
        var fileA = _dialog.BrowseFile("Ersten Report wählen", "HTML (*.html)|*.html|CSV (*.csv)|*.csv");
        if (fileA is null) return;
        var fileB = _dialog.BrowseFile("Zweiten Report wählen", "HTML (*.html)|*.html|CSV (*.csv)|*.csv");
        if (fileB is null) return;
        _vm.AddLog($"Collection-Diff: {Path.GetFileName(fileA)} vs. {Path.GetFileName(fileB)}", "INFO");

        if (fileA.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            fileB.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var setA = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var setB = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadLines(fileA).Skip(1))
                { var mainPath = line.Split(';')[0].Trim('"'); if (!string.IsNullOrWhiteSpace(mainPath)) setA.Add(mainPath); }
                foreach (var line in File.ReadLines(fileB).Skip(1))
                { var mainPath = line.Split(';')[0].Trim('"'); if (!string.IsNullOrWhiteSpace(mainPath)) setB.Add(mainPath); }

                var added = setB.Except(setA).ToList();
                var removed = setA.Except(setB).ToList();
                var same = setA.Intersect(setB).Count();

                var sb = new StringBuilder();
                sb.AppendLine("Collection-Diff (CSV)");
                sb.AppendLine(new string('═', 50));
                sb.AppendLine($"\n  A: {Path.GetFileName(fileA)} ({setA.Count} Einträge)");
                sb.AppendLine($"  B: {Path.GetFileName(fileB)} ({setB.Count} Einträge)");
                sb.AppendLine($"\n  Gleich:     {same}");
                sb.AppendLine($"  Hinzugefügt (in B, nicht in A): {added.Count}");
                sb.AppendLine($"  Entfernt (in A, nicht in B):    {removed.Count}");
                if (added.Count > 0)
                {
                    sb.AppendLine($"\n  --- Hinzugefügt (erste {Math.Min(30, added.Count)}) ---");
                    foreach (var entry in added.Take(30)) sb.AppendLine($"    + {Path.GetFileName(entry)}");
                    if (added.Count > 30) sb.AppendLine($"    … und {added.Count - 30} weitere");
                }
                if (removed.Count > 0)
                {
                    sb.AppendLine($"\n  --- Entfernt (erste {Math.Min(30, removed.Count)}) ---");
                    foreach (var entry in removed.Take(30)) sb.AppendLine($"    - {Path.GetFileName(entry)}");
                    if (removed.Count > 30) sb.AppendLine($"    … und {removed.Count - 30} weitere");
                }
                _dialog.ShowText("Collection-Diff", sb.ToString());
            }
            catch (Exception ex) { _vm.AddLog($"Collection-Diff Fehler: {ex.Message}", "ERROR"); }
        }
        else
        {
            _dialog.ShowText("Collection-Diff", $"Vergleich:\n  A: {fileA}\n  B: {fileB}\n\nReport-Dateien werden verglichen – detaillierter Diff erfordert CSV-Format.");
        }
    }

    private void DuplicateInspector()
    {
        var sources = FeatureService.GetDuplicateInspector(_vm.LastAuditPath);
        if (sources.Count == 0)
        { _vm.AddLog("Keine Duplikat-Daten vorhanden (erst Move/DryRun starten).", "WARN"); return; }
        var sb = new StringBuilder();
        sb.AppendLine("Top Duplikat-Quellverzeichnisse:\n");
        foreach (var s in sources)
            sb.AppendLine($"  {s.Count,4}× │ {s.Directory}");
        _dialog.ShowText("Duplikat-Inspektor", sb.ToString());
    }

    private void DuplicateExport()
    {
        if (_vm.LastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Duplikat-Daten zum Exportieren.", "WARN"); return; }
        var path = _dialog.SaveFile("Duplikate exportieren", "CSV (*.csv)|*.csv", "duplikate.csv");
        if (path is null) return;
        var losers = _vm.LastDedupeGroups.SelectMany(g => g.Losers).ToList();
        var csv = FeatureService.ExportCollectionCsv(losers);
        File.WriteAllText(path, csv, Encoding.UTF8);
        _vm.AddLog($"Duplikate exportiert: {path} ({losers.Count} Einträge)", "INFO");
    }

    private void ExportCsv()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Keine Daten zum Exportieren.", "WARN"); return; }
        var path = _dialog.SaveFile("CSV Export", "CSV (*.csv)|*.csv", "sammlung.csv");
        if (path is null) return;
        var csv = FeatureService.ExportCollectionCsv(_vm.LastCandidates);
        File.WriteAllText(path, "\uFEFF" + csv, Encoding.UTF8);
        _vm.AddLog($"CSV exportiert: {path} ({_vm.LastCandidates.Count} Einträge)", "INFO");
    }

    private void ExportExcel()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Keine Daten zum Exportieren.", "WARN"); return; }
        var path = _dialog.SaveFile("Excel Export", "Excel XML (*.xml)|*.xml", "sammlung.xml");
        if (path is null) return;
        var xml = FeatureService.ExportExcelXml(_vm.LastCandidates);
        File.WriteAllText(path, xml, Encoding.UTF8);
        _vm.AddLog($"Excel exportiert: {path}", "INFO");
    }

    private void RollbackUndo()
    {
        var auditPath = _vm.PopRollbackUndo();
        if (auditPath is null)
        { _vm.AddLog("Kein Rollback zum Rückgängig machen.", "WARN"); return; }
        _vm.AddLog($"Rollback rückgängig gemacht: {Path.GetFileName(auditPath)}", "INFO");
    }

    private void RollbackRedo()
    {
        var auditPath = _vm.PopRollbackRedo();
        if (auditPath is null)
        { _vm.AddLog("Kein Redo-Rollback verfügbar.", "WARN"); return; }
        _vm.AddLog($"Rollback Redo: {Path.GetFileName(auditPath)}", "INFO");
    }

    private void ApplyLocale()
    {
        var locale = _vm.Locale ?? "de";
        var strings = FeatureService.LoadLocale(locale);
        if (strings.Count == 0)
        { _vm.AddLog($"Sprachdatei '{locale}' nicht gefunden.", "WARN"); return; }
        _vm.AddLog($"Sprache gewechselt: {locale} ({strings.Count} Strings geladen). Hinweis: Aktuell wird nur der Fenstertitel lokalisiert.", "INFO");
        // Title update must be done in code-behind (Window property)
    }

    private void PluginManager()
    {
        var pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginDir))
        {
            _dialog.Info("Kein Plugin-Verzeichnis gefunden.\n\nErstelle 'plugins/' im Programmverzeichnis, um Plugins zu verwenden.", "Plugin-Manager");
            return;
        }
        var manifests = Directory.GetFiles(pluginDir, "plugin.json", SearchOption.AllDirectories);
        var sb = new StringBuilder();
        sb.AppendLine($"Plugin-Manager: {manifests.Length} Plugin(s) gefunden\n");
        foreach (var m in manifests)
        {
            var dir = Path.GetDirectoryName(m)!;
            sb.AppendLine($"  📦 {Path.GetFileName(dir)}");
            sb.AppendLine($"     Pfad: {dir}");
        }
        if (manifests.Length == 0)
            sb.AppendLine("  Keine Plugins installiert.");
        _dialog.ShowText("Plugin-Manager", sb.ToString());
    }

    private void AutoProfile()
    {
        if (_vm.Roots.Count == 0)
        { _vm.AddLog("Keine Root-Ordner – Auto-Profil nicht möglich.", "WARN"); return; }
        var hasDisc = false;
        var hasCartridge = false;
        foreach (var root in _vm.Roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var f in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories).Take(200))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is ".chd" or ".iso" or ".bin" or ".cue" or ".gdi") hasDisc = true;
                if (ext is ".nes" or ".sfc" or ".gba" or ".nds" or ".z64" or ".gb") hasCartridge = true;
            }
        }
        var profile = (hasDisc, hasCartridge) switch
        {
            (true, true) => "Gemischt (Disc + Cartridge): Konvertierung empfohlen",
            (true, false) => "Disc-basiert: CHD-Konvertierung empfohlen, aggressive Deduplizierung",
            (false, true) => "Cartridge-basiert: ZIP-Komprimierung, leichte Deduplizierung",
            _ => "Unbekannt: Keine erkannten ROM-Formate gefunden. Bitte überprüfen Sie die Root-Ordner."
        };
        if (!hasDisc && !hasCartridge)
            _vm.AddLog("Auto-Profil: Keine bekannten ROM-Formate erkannt – Standard-Profil wird empfohlen.", "WARN");
        else
            _vm.AddLog($"Auto-Profil: {profile}", "INFO");
        _dialog.Info($"Auto-Profil-Empfehlung:\n\n{profile}\n\n" +
            "Hinweis: Die Erkennung basiert auf Dateierweiterungen der ersten 200 Dateien pro Root-Ordner.", "Auto-Profil");
    }

    // ═══ ANALYSE & BERICHTE ═════════════════════════════════════════════

    private void ConversionEstimate()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten, um Konvertierungs-Schätzungen zu berechnen.", "WARN"); return; }
        var est = FeatureService.GetConversionEstimate(_vm.LastCandidates);
        var sb = new StringBuilder();
        sb.AppendLine("Konvertierungs-Schätzung");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"  Quellgröße:     {FeatureService.FormatSize(est.TotalSourceBytes)}");
        sb.AppendLine($"  Geschätzt:      {FeatureService.FormatSize(est.EstimatedTargetBytes)}");
        sb.AppendLine($"  Ersparnis:      {FeatureService.FormatSize(est.SavedBytes)} ({(1 - est.CompressionRatio) * 100:F1}%)");
        sb.AppendLine($"\nDetails ({est.Details.Count} konvertierbare Dateien):");
        foreach (var d in est.Details.Take(20))
            sb.AppendLine($"  {d.FileName}: {d.SourceFormat}→{d.TargetFormat} ({FeatureService.FormatSize(d.SourceBytes)}→{FeatureService.FormatSize(d.EstimatedBytes)})");
        if (est.Details.Count > 20)
            sb.AppendLine($"  … und {est.Details.Count - 20} weitere");
        _dialog.ShowText("Konvertierungs-Schätzung", sb.ToString());
    }

    private void JunkReport()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var report = FeatureService.BuildJunkReport(_vm.LastCandidates, _vm.AggressiveJunk);
        _dialog.ShowText("Junk-Bericht", report);
    }

    private void RomFilter()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Keine ROM-Daten geladen.", "WARN"); return; }
        var input = _dialog.ShowInputBox("Suchbegriff eingeben (Name, Region, Konsole, Format):", "ROM-Filter", "");
        if (string.IsNullOrWhiteSpace(input)) return;
        var results = FeatureService.SearchRomCollection(_vm.LastCandidates, input);
        var sb = new StringBuilder();
        sb.AppendLine($"ROM-Filter: \"{input}\" → {results.Count} Treffer\n");
        foreach (var r in results.Take(50))
            sb.AppendLine($"  {Path.GetFileName(r.MainPath),-40} [{r.Region}] {r.Extension} {r.Category}");
        if (results.Count > 50)
            sb.AppendLine($"\n  … und {results.Count - 50} weitere");
        _dialog.ShowText("ROM-Filter", sb.ToString());
    }

    private void DuplicateHeatmap()
    {
        if (_vm.LastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Deduplizierungs-Daten vorhanden.", "WARN"); return; }
        var heatmap = FeatureService.GetDuplicateHeatmap(_vm.LastDedupeGroups);
        var sb = new StringBuilder();
        sb.AppendLine("Duplikat-Heatmap (nach Konsole)\n");
        foreach (var h in heatmap)
        {
            var bar = new string('█', (int)(h.DuplicatePercent / 5));
            sb.AppendLine($"  {h.Console,-25} {h.Duplicates,4} Dupes ({h.DuplicatePercent:F1}%) {bar}");
        }
        _dialog.ShowText("Duplikat-Heatmap", sb.ToString());
    }

    private void MissingRom()
    {
        if (!_vm.UseDat || string.IsNullOrWhiteSpace(_vm.DatRoot))
        { _vm.AddLog("DAT muss aktiviert und konfiguriert sein.", "WARN"); return; }
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen DryRun mit aktiviertem DAT starten.", "WARN"); return; }

        var unverified = _vm.LastCandidates.Where(c => !c.DatMatch).ToList();
        if (unverified.Count == 0)
        { _dialog.Info("Alle ROMs haben einen DAT-Match. Keine fehlenden ROMs erkannt.", "Fehlende ROMs"); return; }

        var roots = _vm.Roots.Select(r => Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToList();
        string GetSubDir(string filePath)
        {
            var full = Path.GetFullPath(filePath);
            foreach (var root in roots)
            {
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = full[(root.Length + 1)..];
                    var sep = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                    return sep > 0 ? relative[..sep] : "(Root)";
                }
            }
            return Path.GetDirectoryName(filePath) ?? "(Unbekannt)";
        }
        var byDir = unverified.GroupBy(c => GetSubDir(c.MainPath)).OrderByDescending(g => g.Count()).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Fehlende ROMs (ohne DAT-Match)");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Gesamt ohne DAT-Match: {unverified.Count} / {_vm.LastCandidates.Count}");
        sb.AppendLine($"\n  Nach Verzeichnis:\n");
        foreach (var g in byDir)
            sb.AppendLine($"    {g.Count(),5}  {g.Key}");
        _dialog.ShowText("Fehlende ROMs", sb.ToString());
    }

    private void CrossRootDupe()
    {
        if (_vm.Roots.Count < 2)
        { _vm.AddLog("Mindestens 2 Root-Ordner für Cross-Root-Duplikate erforderlich.", "WARN"); return; }
        if (_vm.LastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Deduplizierungs-Daten vorhanden. Erst einen DryRun starten.", "WARN"); return; }

        var roots = _vm.Roots.Select(r => Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToList();
        string? GetRoot(string filePath)
        {
            var full = Path.GetFullPath(filePath);
            return roots.FirstOrDefault(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase));
        }

        var crossRootGroups = new List<DedupeResult>();
        foreach (var g in _vm.LastDedupeGroups)
        {
            var allPaths = new[] { g.Winner }.Concat(g.Losers);
            var distinctRoots = allPaths.Select(c => GetRoot(c.MainPath)).Where(r => r is not null).Distinct().Count();
            if (distinctRoots > 1) crossRootGroups.Add(g);
        }

        var sb = new StringBuilder();
        sb.AppendLine("Cross-Root-Duplikate");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Roots: {_vm.Roots.Count}");
        sb.AppendLine($"  Dedupe-Gruppen gesamt: {_vm.LastDedupeGroups.Count}");
        sb.AppendLine($"  Cross-Root-Gruppen: {crossRootGroups.Count}\n");
        foreach (var g in crossRootGroups.Take(30))
        {
            sb.AppendLine($"  [{g.GameKey}]");
            sb.AppendLine($"    Winner: {g.Winner.MainPath}");
            foreach (var l in g.Losers) sb.AppendLine($"    Loser:  {l.MainPath}");
        }
        if (crossRootGroups.Count > 30) sb.AppendLine($"\n  … und {crossRootGroups.Count - 30} weitere Gruppen");
        if (crossRootGroups.Count == 0) sb.AppendLine("  Keine Cross-Root-Duplikate gefunden.");
        _dialog.ShowText("Cross-Root-Duplikate", sb.ToString());
    }

    private void HeaderAnalysis()
    {
        var path = _dialog.BrowseFile("ROM für Header-Analyse wählen",
            "ROM-Dateien (*.nes;*.sfc;*.gba;*.z64;*.v64;*.n64;*.smc)|*.nes;*.sfc;*.gba;*.z64;*.v64;*.n64;*.smc|Alle (*.*)|*.*");
        if (path is null) return;
        var header = FeatureService.AnalyzeHeader(path);
        if (header is null)
        { _vm.AddLog($"Header konnte nicht gelesen werden: {path}", "ERROR"); return; }
        _dialog.ShowText("Header-Analyse", $"Datei: {Path.GetFileName(path)}\n\n" +
            $"Plattform: {header.Platform}\nFormat: {header.Format}\nDetails: {header.Details}");
    }

    private void Completeness()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var verified = _vm.LastCandidates.Count(c => c.DatMatch);
        var total = _vm.LastCandidates.Count;
        var pct = total > 0 ? 100.0 * verified / total : 0;
        _dialog.ShowText("Vollständigkeit", $"Sammlungs-Vollständigkeit\n\n" +
            $"Verifizierte Dateien: {verified} / {total} ({pct:F1}%)\n\n" +
            $"Für eine DAT-basierte Vollständigkeitsanalyse\naktiviere DAT-Verifizierung und starte einen DryRun.");
    }

    private void DryRunCompare()
    {
        var fileA = _dialog.BrowseFile("Ersten DryRun-Report wählen", "CSV (*.csv)|*.csv|HTML (*.html)|*.html");
        var fileB = fileA is not null ? _dialog.BrowseFile("Zweiten DryRun-Report wählen", "CSV (*.csv)|*.csv|HTML (*.html)|*.html") : null;
        if (fileA is null || fileB is null) return;
        _vm.AddLog($"DryRun-Vergleich: {Path.GetFileName(fileA)} vs. {Path.GetFileName(fileB)}", "INFO");

        if (fileA.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            fileB.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var setA = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var setB = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadLines(fileA).Skip(1))
                { var mainPath = line.Split(';')[0].Trim('"'); if (!string.IsNullOrWhiteSpace(mainPath)) setA.Add(mainPath); }
                foreach (var line in File.ReadLines(fileB).Skip(1))
                { var mainPath = line.Split(';')[0].Trim('"'); if (!string.IsNullOrWhiteSpace(mainPath)) setB.Add(mainPath); }

                var added = setB.Except(setA).ToList();
                var removed = setA.Except(setB).ToList();
                var same = setA.Intersect(setB).Count();

                var sb = new StringBuilder();
                sb.AppendLine("DryRun-Vergleich (CSV)");
                sb.AppendLine(new string('═', 50));
                sb.AppendLine($"\n  A: {Path.GetFileName(fileA)} ({setA.Count} Einträge)");
                sb.AppendLine($"  B: {Path.GetFileName(fileB)} ({setB.Count} Einträge)");
                sb.AppendLine($"\n  Gleich:     {same}");
                sb.AppendLine($"  Hinzugefügt: {added.Count}");
                sb.AppendLine($"  Entfernt:    {removed.Count}");
                if (added.Count > 0)
                {
                    sb.AppendLine($"\n  --- Hinzugefügt (erste {Math.Min(30, added.Count)}) ---");
                    foreach (var entry in added.Take(30)) sb.AppendLine($"    + {Path.GetFileName(entry)}");
                    if (added.Count > 30) sb.AppendLine($"    … und {added.Count - 30} weitere");
                }
                if (removed.Count > 0)
                {
                    sb.AppendLine($"\n  --- Entfernt (erste {Math.Min(30, removed.Count)}) ---");
                    foreach (var entry in removed.Take(30)) sb.AppendLine($"    - {Path.GetFileName(entry)}");
                    if (removed.Count > 30) sb.AppendLine($"    … und {removed.Count - 30} weitere");
                }
                _dialog.ShowText("DryRun-Vergleich", sb.ToString());
            }
            catch (Exception ex) { _vm.AddLog($"DryRun-Vergleich Fehler: {ex.Message}", "ERROR"); }
        }
        else
        {
            _dialog.ShowText("DryRun-Vergleich", $"Vergleich:\n  A: {fileA}\n  B: {fileB}\n\n" +
                "Detaillierter Vergleich erfordert CSV-Reports.\nExportiere Reports als CSV und vergleiche erneut.");
        }
    }

    private void TrendAnalysis()
    {
        if (_vm.LastCandidates.Count > 0)
        {
            var dupes = _vm.LastDedupeGroups.Sum(g => g.Losers.Count);
            var junk = _vm.LastCandidates.Count(c => c.Category == "JUNK");
            var verified = _vm.LastCandidates.Count(c => c.DatMatch);
            var totalSize = _vm.LastCandidates.Sum(c => c.SizeBytes);
            FeatureService.SaveTrendSnapshot(_vm.LastCandidates.Count, totalSize, verified, dupes, junk);
        }
        var history = FeatureService.LoadTrendHistory();
        var report = FeatureService.FormatTrendReport(history);
        _dialog.ShowText("Trend-Analyse", report);
    }

    private void EmulatorCompat()
    {
        _dialog.ShowText("Emulator-Kompatibilität", FeatureService.FormatEmulatorCompat());
    }

    // ═══ KONVERTIERUNG & HASHING ════════════════════════════════════════

    private void ConversionPipeline()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var est = FeatureService.GetConversionEstimate(_vm.LastCandidates);
        _vm.AddLog($"Konvertierungs-Pipeline: {est.Details.Count} Dateien, Ersparnis ~{FeatureService.FormatSize(est.SavedBytes)}", "INFO");
        _dialog.Info($"Konvertierungs-Pipeline bereit:\n\n{est.Details.Count} Dateien konvertierbar\n" +
            $"Geschätzte Ersparnis: {FeatureService.FormatSize(est.SavedBytes)}\n\n" +
            "Aktiviere 'Konvertierung' und starte einen Move-Lauf.", "Konvertierungs-Pipeline");
    }

    private void NKitConvert()
    {
        var path = _dialog.BrowseFile("NKit-Image wählen", "NKit (*.nkit.iso;*.nkit.gcz;*.nkit)|*.nkit.iso;*.nkit.gcz;*.nkit|Alle (*.*)|*.*");
        if (path is null) return;
        var isNkit = path.Contains(".nkit", StringComparison.OrdinalIgnoreCase);
        _vm.AddLog($"NKit erkannt: {isNkit}, Datei: {Path.GetFileName(path)}", isNkit ? "INFO" : "WARN");
        try
        {
            var runner = new ToolRunnerAdapter(null);
            var nkitPath = runner.FindTool("nkit");
            if (nkitPath is not null)
                _dialog.Info($"NKit-Tool gefunden: {nkitPath}\n\nImage: {Path.GetFileName(path)}\nNKit-Format: {(isNkit ? "Ja" : "Nein")}\n\nKonvertierungs-Anleitung:\n  NKit → ISO: NKit.exe recover <Datei>\n  NKit → RVZ: Erst recover, dann dolphintool convert\n\nEmpfohlenes Zielformat: RVZ (GameCube/Wii)", "NKit-Konvertierung");
            else
                _dialog.Info($"NKit-Tool nicht gefunden.\n\nImage: {Path.GetFileName(path)}\n\nDownload: https://vimm.net/vault/nkit\n\nNach dem Download das Tool in den PATH aufnehmen\noder im Programmverzeichnis ablegen.", "NKit-Konvertierung");
        }
        catch (Exception ex)
        {
            _vm.AddLog($"NKit-Tool-Suche fehlgeschlagen: {ex.Message}", "WARN");
            _dialog.Info($"NKit-Image: {Path.GetFileName(path)}\n\nKonvertierung nach ISO/RVZ erfordert das Tool 'NKit'.\nDownload: https://vimm.net/vault/nkit", "NKit-Konvertierung");
        }
    }

    private void ConvertQueue()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Keine Dateien in der Konvert-Warteschlange.", "WARN"); return; }
        var est = FeatureService.GetConversionEstimate(_vm.LastCandidates);

        var sb = new StringBuilder();
        sb.AppendLine("Konvert-Warteschlange");
        sb.AppendLine(new string('═', 60));
        sb.AppendLine($"\n  Dateien: {est.Details.Count}");
        sb.AppendLine($"  Quellgröße: {FeatureService.FormatSize(est.TotalSourceBytes)}");
        sb.AppendLine($"  Geschätzte Zielgröße: {FeatureService.FormatSize(est.EstimatedTargetBytes)}");
        sb.AppendLine($"  Ersparnis: {FeatureService.FormatSize(est.SavedBytes)}\n");
        if (est.Details.Count > 0)
        {
            sb.AppendLine($"  {"Datei",-40} {"Quelle",-8} {"Ziel",-8} {"Größe",12}");
            sb.AppendLine($"  {new string('-', 40)} {new string('-', 8)} {new string('-', 8)} {new string('-', 12)}");
            foreach (var d in est.Details)
                sb.AppendLine($"  {d.FileName,-40} {d.SourceFormat,-8} {d.TargetFormat,-8} {FeatureService.FormatSize(d.SourceBytes),12}");
        }
        else
            sb.AppendLine("  Keine konvertierbaren Dateien gefunden.");
        _dialog.ShowText("Konvert-Warteschlange", sb.ToString());
    }

    private void ConversionVerify()
    {
        var dir = _dialog.BrowseFolder("Konvertierte Dateien prüfen");
        if (dir is null) return;
        var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".chd" or ".rvz" or ".7z").ToList();
        var (passed, failed, missing) = FeatureService.VerifyConversions(files);
        _dialog.ShowText("Konvertierung verifizieren", $"Verifizierung: {dir}\n\n" +
            $"Bestanden: {passed}\nFehlgeschlagen: {failed}\nFehlend: {missing}\nGesamt: {files.Count}");
    }

    private void FormatPriority()
    {
        _dialog.ShowText("Format-Priorität", FeatureService.FormatFormatPriority());
    }

    private void ParallelHashing()
    {
        var cores = Environment.ProcessorCount;
        var optimal = Math.Max(1, cores - 1);
        var input = _dialog.ShowInputBox(
            $"CPU-Kerne: {cores}\nAktuell: {optimal} Threads\n\nGewünschte Thread-Anzahl eingeben (1-{cores}):",
            "Parallel-Hashing Konfiguration", optimal.ToString());
        if (string.IsNullOrWhiteSpace(input)) return;
        if (int.TryParse(input, out var threads) && threads >= 1 && threads <= cores * 2)
        {
            Environment.SetEnvironmentVariable("ROMCLEANUP_HASH_THREADS", threads.ToString());
            _vm.AddLog($"Parallel-Hashing: {threads} Threads konfiguriert (experimentell – wird in einer zukünftigen Version unterstützt)", "INFO");
            _dialog.ShowText("Parallel-Hashing", $"Parallel-Hashing Konfiguration\n\nCPU-Kerne: {cores}\nThreads (neu): {threads}\n\nDie Änderung wird beim nächsten Hash-Vorgang wirksam.");
        }
        else
            _vm.AddLog($"Ungültige Thread-Anzahl: {input} (erlaubt: 1-{cores * 2})", "WARN");
    }

    private void GpuHashing()
    {
        var openCl = File.Exists(Path.Combine(Environment.SystemDirectory, "OpenCL.dll"));
        var currentSetting = Environment.GetEnvironmentVariable("ROMCLEANUP_GPU_HASHING") ?? "off";
        var isEnabled = currentSetting.Equals("on", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.AppendLine("GPU-Hashing Konfiguration\n");
        sb.AppendLine($"  OpenCL verfügbar: {(openCl ? "Ja" : "Nein")}");
        sb.AppendLine($"  CPU-Kerne:        {Environment.ProcessorCount}");
        sb.AppendLine($"  Aktueller Status: {(isEnabled ? "AKTIVIERT" : "Deaktiviert")}");
        if (!openCl)
        {
            sb.AppendLine("\n  GPU-Hashing benötigt OpenCL-Treiber.\n  Installiere aktuelle GPU-Treiber für Unterstützung.");
            _dialog.ShowText("GPU-Hashing", sb.ToString());
            return;
        }
        sb.AppendLine("\n  GPU-Hashing kann SHA1/SHA256-Berechnungen\n  um 5-20x beschleunigen (experimentell).");
        _dialog.ShowText("GPU-Hashing", sb.ToString());
        var toggle = isEnabled ? "deaktivieren" : "aktivieren";
        if (_dialog.Confirm($"GPU-Hashing {toggle}?\n\nAktuell: {(isEnabled ? "AN" : "AUS")}", "GPU-Hashing"))
        {
            var newValue = isEnabled ? "off" : "on";
            Environment.SetEnvironmentVariable("ROMCLEANUP_GPU_HASHING", newValue);
            _vm.AddLog($"GPU-Hashing: {(isEnabled ? "deaktiviert" : "aktiviert")} (experimentell – wird in einer zukünftigen Version unterstützt)", "INFO");
        }
    }

    // ═══ DAT & VERIFIZIERUNG ════════════════════════════════════════════

    private void DatAutoUpdate()
    {
        if (string.IsNullOrWhiteSpace(_vm.DatRoot))
        { _vm.AddLog("DAT-Root nicht konfiguriert.", "WARN"); return; }
        if (!Directory.Exists(_vm.DatRoot))
        { _vm.AddLog($"DAT-Root existiert nicht: {_vm.DatRoot}", "ERROR"); return; }
        _vm.AddLog("DAT Auto-Update: Prüfe lokale DAT-Dateien…", "INFO");

        var dataDir = FeatureService.ResolveDataDirectory() ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var catalogPath = Path.Combine(dataDir, "dat-catalog.json");

        var sb = new StringBuilder();
        sb.AppendLine("DAT Auto-Update");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  DAT-Root: {_vm.DatRoot}");

        var localDats = Directory.GetFiles(_vm.DatRoot, "*.dat", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_vm.DatRoot, "*.xml", SearchOption.AllDirectories)).ToList();
        sb.AppendLine($"  Lokale DAT-Dateien: {localDats.Count}\n");

        if (localDats.Count > 0)
        {
            sb.AppendLine("  Lokale Dateien (nach Alter sortiert):");
            foreach (var dat in localDats.OrderBy(d => File.GetLastWriteTime(d)).Take(20))
            {
                var age = DateTime.Now - File.GetLastWriteTime(dat);
                var ageStr = age.TotalDays > 365 ? $"{age.TotalDays / 365:0.0} Jahre"
                    : age.TotalDays > 30 ? $"{age.TotalDays / 30:0.0} Monate"
                    : $"{age.TotalDays:0} Tage";
                sb.AppendLine($"    {Path.GetFileName(dat),-40} {ageStr} alt");
            }
            if (localDats.Count > 20) sb.AppendLine($"    … und {localDats.Count - 20} weitere");
        }

        if (File.Exists(catalogPath))
        {
            try
            {
                var catalogJson = File.ReadAllText(catalogPath);
                using var doc = JsonDocument.Parse(catalogJson);
                var entries = doc.RootElement.EnumerateArray().ToList();
                var withUrl = entries.Count(e => e.TryGetProperty("Url", out var u) && u.GetString()?.Length > 0);
                var groups = entries.GroupBy(e => e.TryGetProperty("Group", out var g) ? g.GetString() : "?");
                sb.AppendLine($"\n  Katalog: {entries.Count} Einträge ({withUrl} mit Download-URL)");
                foreach (var g in groups)
                    sb.AppendLine($"    {g.Key}: {g.Count()} Systeme");
            }
            catch (Exception ex) { sb.AppendLine($"\n  Katalog-Fehler: {ex.Message}"); }
        }
        else
            sb.AppendLine($"\n  Katalog nicht gefunden: {catalogPath}");

        var oldDats = localDats.Where(d => (DateTime.Now - File.GetLastWriteTime(d)).TotalDays > 180).ToList();
        if (oldDats.Count > 0) sb.AppendLine($"\n  ⚠ {oldDats.Count} DATs sind älter als 6 Monate!");

        _dialog.ShowText("DAT Auto-Update", sb.ToString());
        _vm.AddLog($"DAT-Status: {localDats.Count} lokale DATs, {(localDats.Count > 0 ? $"älteste: {(DateTime.Now - File.GetLastWriteTime(localDats.OrderBy(d => File.GetLastWriteTime(d)).First())).TotalDays:0} Tage" : "keine")}", "INFO");
    }

    private void DatDiffViewer()
    {
        var fileA = _dialog.BrowseFile("Alte DAT-Datei wählen", "DAT (*.dat;*.xml)|*.dat;*.xml");
        if (fileA is null) return;
        var fileB = _dialog.BrowseFile("Neue DAT-Datei wählen", "DAT (*.dat;*.xml)|*.dat;*.xml");
        if (fileB is null) return;
        _vm.AddLog($"DAT-Diff: {Path.GetFileName(fileA)} vs. {Path.GetFileName(fileB)}", "INFO");
        try
        {
            var diff = FeatureService.CompareDatFiles(fileA, fileB);
            var sb = new StringBuilder();
            sb.AppendLine("DAT-Diff-Viewer (Logiqx XML)");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine($"\n  A: {Path.GetFileName(fileA)}");
            sb.AppendLine($"  B: {Path.GetFileName(fileB)}");
            sb.AppendLine($"\n  Gleich:       {diff.UnchangedCount}");
            sb.AppendLine($"  Geändert:     {diff.ModifiedCount}");
            sb.AppendLine($"  Hinzugefügt:  {diff.Added.Count}");
            sb.AppendLine($"  Entfernt:     {diff.Removed.Count}");
            if (diff.Added.Count > 0)
            {
                sb.AppendLine($"\n  --- Hinzugefügt (erste {Math.Min(30, diff.Added.Count)}) ---");
                foreach (var name in diff.Added.Take(30)) sb.AppendLine($"    + {name}");
                if (diff.Added.Count > 30) sb.AppendLine($"    … und {diff.Added.Count - 30} weitere");
            }
            if (diff.Removed.Count > 0)
            {
                sb.AppendLine($"\n  --- Entfernt (erste {Math.Min(30, diff.Removed.Count)}) ---");
                foreach (var name in diff.Removed.Take(30)) sb.AppendLine($"    - {name}");
                if (diff.Removed.Count > 30) sb.AppendLine($"    … und {diff.Removed.Count - 30} weitere");
            }
            _dialog.ShowText("DAT-Diff-Viewer", sb.ToString());
        }
        catch (Exception ex)
        {
            _vm.AddLog($"DAT-Diff Fehler: {ex.Message}", "ERROR");
            _dialog.ShowText("DAT-Diff-Viewer", $"Fehler beim Parsen der DAT-Dateien:\n\n{ex.Message}\n\nStelle sicher, dass beide Dateien gültiges Logiqx-XML enthalten.");
        }
    }

    private void TosecDat()
    {
        var path = _dialog.BrowseFile("TOSEC-DAT wählen", "DAT (*.dat;*.xml)|*.dat;*.xml");
        if (path is null) return;
        _vm.AddLog($"TOSEC-DAT geladen: {Path.GetFileName(path)}", "INFO");
        if (string.IsNullOrWhiteSpace(_vm.DatRoot))
        { _dialog.Error("DAT-Root ist nicht konfiguriert. Bitte zuerst den DAT-Root-Ordner setzen.", "TOSEC-DAT"); return; }
        try
        {
            var safeName = Path.GetFileName(path);
            var targetPath = Path.GetFullPath(Path.Combine(_vm.DatRoot, safeName));
            if (!targetPath.StartsWith(Path.GetFullPath(_vm.DatRoot), StringComparison.OrdinalIgnoreCase))
            { _vm.AddLog("TOSEC-DAT Import blockiert: Pfad außerhalb des DatRoot.", "ERROR"); return; }
            File.Copy(path, targetPath, overwrite: true);
            _vm.AddLog($"TOSEC-DAT kopiert nach: {targetPath}", "INFO");
            _dialog.Info($"TOSEC-DAT erfolgreich importiert:\n\n  Quelle: {path}\n  Ziel: {targetPath}", "TOSEC-DAT");
        }
        catch (Exception ex) { _vm.AddLog($"TOSEC-DAT Import fehlgeschlagen: {ex.Message}", "ERROR"); }
    }

    private void CustomDatEditor()
    {
        var gameName = _dialog.ShowInputBox("Spielname eingeben:", "Custom-DAT-Editor", "");
        if (string.IsNullOrWhiteSpace(gameName)) return;
        var romName = _dialog.ShowInputBox("ROM-Dateiname eingeben:", "Custom-DAT-Editor", $"{gameName}.zip");
        if (string.IsNullOrWhiteSpace(romName)) return;
        var crc32 = _dialog.ShowInputBox("CRC32-Hash eingeben (hex):", "Custom-DAT-Editor", "00000000");
        if (string.IsNullOrWhiteSpace(crc32)) return;
        if (!Regex.IsMatch(crc32, @"^[0-9A-Fa-f]{8}$"))
        { _vm.AddLog($"Ungültiger CRC32-Hash: '{crc32}' — erwartet: 8 Hex-Zeichen.", "WARN"); return; }
        var sha1 = _dialog.ShowInputBox("SHA1-Hash eingeben (hex):", "Custom-DAT-Editor", "");
        if (string.IsNullOrWhiteSpace(sha1)) sha1 = "";
        if (sha1.Length > 0 && !Regex.IsMatch(sha1, @"^[0-9A-Fa-f]{40}$"))
        { _vm.AddLog($"Ungültiger SHA1-Hash: '{sha1}' — erwartet: 40 Hex-Zeichen.", "WARN"); return; }

        var xmlEntry = $"  <game name=\"{System.Security.SecurityElement.Escape(gameName)}\">\n" +
                       $"    <description>{System.Security.SecurityElement.Escape(gameName)}</description>\n" +
                       $"    <rom name=\"{System.Security.SecurityElement.Escape(romName)}\" size=\"0\" crc=\"{crc32}\"" +
                       (sha1.Length > 0 ? $" sha1=\"{sha1}\"" : "") + " />\n  </game>";

        if (!string.IsNullOrWhiteSpace(_vm.DatRoot) && Directory.Exists(_vm.DatRoot))
        {
            try
            {
                var customDatPath = Path.Combine(_vm.DatRoot, "custom.dat");
                if (File.Exists(customDatPath))
                {
                    var content = File.ReadAllText(customDatPath);
                    var closeTag = "</datafile>";
                    var idx = content.LastIndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0) content = content[..idx] + xmlEntry + "\n" + closeTag;
                    else content += "\n" + xmlEntry;
                    var tempPath = customDatPath + ".tmp";
                    File.WriteAllText(tempPath, content);
                    File.Move(tempPath, customDatPath, overwrite: true);
                }
                else
                {
                    var fullXml = "<?xml version=\"1.0\"?>\n" +
                                  "<!DOCTYPE datafile SYSTEM \"http://www.logiqx.com/Dats/datafile.dtd\">\n" +
                                  "<datafile>\n  <header>\n    <name>Custom DAT</name>\n" +
                                  "    <description>Benutzerdefinierte DAT-Einträge</description>\n  </header>\n" +
                                  xmlEntry + "\n</datafile>";
                    File.WriteAllText(customDatPath, fullXml);
                }
                _vm.AddLog($"Custom-DAT-Eintrag gespeichert: {customDatPath}", "INFO");
            }
            catch (Exception ex) { _vm.AddLog($"Custom-DAT Fehler: {ex.Message}", "ERROR"); }
        }
        else
            _vm.AddLog("DatRoot nicht gesetzt – Eintrag wird nur angezeigt.", "WARN");
        _dialog.ShowText("Custom-DAT-Editor", $"Generierter Logiqx-XML-Eintrag:\n\n{xmlEntry}");
    }

    private void HashDatabaseExport()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Keine Daten für Hash-Export.", "WARN"); return; }
        var path = _dialog.SaveFile("Hash-Datenbank exportieren", "JSON (*.json)|*.json", "hash-database.json");
        if (path is null) return;
        var entries = _vm.LastCandidates.Select(c => new { c.MainPath, c.GameKey, c.Extension, c.Region, c.DatMatch, c.SizeBytes }).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        _vm.AddLog($"Hash-Datenbank exportiert: {path} ({entries.Count} Einträge)", "INFO");
    }

    // ═══ SAMMLUNGSVERWALTUNG ════════════════════════════════════════════

    private void CollectionManager()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var byConsole = _vm.LastCandidates.GroupBy(c => FeatureService.ClassifyGenre(c.GameKey))
            .OrderByDescending(g => g.Count()).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("Smart Collection Manager\n");
        sb.AppendLine($"Gesamt: {_vm.LastCandidates.Count} ROMs\n");
        foreach (var g in byConsole)
            sb.AppendLine($"  {g.Key,-20} {g.Count(),5} ROMs");
        _dialog.ShowText("Smart Collection", sb.ToString());
    }

    private void CloneListViewer()
    {
        if (_vm.LastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Gruppen vorhanden.", "WARN"); return; }
        _dialog.ShowText("Clone-Liste", FeatureService.BuildCloneTree(_vm.LastDedupeGroups));
    }

    private void CoverScraper()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var coverDir = _dialog.BrowseFolder("Cover-Ordner wählen (enthält Cover-Bilder)");
        if (coverDir is null) return;

        var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
        var coverFiles = Directory.GetFiles(coverDir, "*.*", SearchOption.AllDirectories)
            .Where(f => imageExts.Contains(Path.GetExtension(f))).ToList();
        if (coverFiles.Count == 0)
        { _dialog.Info($"Keine Cover-Bilder gefunden in:\n{coverDir}", "Cover-Scraper"); return; }

        var gameKeys = _vm.LastCandidates
            .Select(c => RomCleanup.Core.GameKeys.GameKeyNormalizer.Normalize(Path.GetFileNameWithoutExtension(c.MainPath)))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matched = new List<string>();
        var unmatched = new List<string>();
        foreach (var cover in coverFiles)
        {
            var coverName = Path.GetFileNameWithoutExtension(cover);
            var normalizedCover = RomCleanup.Core.GameKeys.GameKeyNormalizer.Normalize(coverName);
            if (gameKeys.Contains(normalizedCover)) matched.Add(coverName);
            else unmatched.Add(coverName);
        }

        var sb = new StringBuilder();
        sb.AppendLine("Cover-Scraper Ergebnis\n");
        sb.AppendLine($"  Cover-Ordner: {coverDir}");
        sb.AppendLine($"  Gefundene Bilder: {coverFiles.Count}");
        sb.AppendLine($"  ROMs in Sammlung: {gameKeys.Count}");
        sb.AppendLine($"\n  Zugeordnet:    {matched.Count}");
        sb.AppendLine($"  Nicht zugeordnet: {unmatched.Count}");
        sb.AppendLine($"  Ohne Cover:    {gameKeys.Count - matched.Count}");
        if (matched.Count > 0)
        {
            sb.AppendLine($"\n  --- Zugeordnet (erste {Math.Min(15, matched.Count)}) ---");
            foreach (var m in matched.Take(15)) sb.AppendLine($"    ✓ {m}");
        }
        if (unmatched.Count > 0)
        {
            sb.AppendLine($"\n  --- Nicht zugeordnet (erste {Math.Min(15, unmatched.Count)}) ---");
            foreach (var u in unmatched.Take(15)) sb.AppendLine($"    ? {u}");
        }
        _dialog.ShowText("Cover-Scraper", sb.ToString());
        _vm.AddLog($"Cover-Scan: {matched.Count} zugeordnet, {unmatched.Count} nicht zugeordnet", "INFO");
    }

    private void GenreClassification()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var genres = _vm.LastCandidates.GroupBy(c => FeatureService.ClassifyGenre(c.GameKey))
            .OrderByDescending(g => g.Count()).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("Genre-Klassifikation\n");
        foreach (var g in genres)
        {
            sb.AppendLine($"  {g.Key,-20} {g.Count(),5} Spiele");
            foreach (var item in g.Take(3))
                sb.AppendLine($"    • {Path.GetFileNameWithoutExtension(item.MainPath)}");
        }
        _dialog.ShowText("Genre-Klassifikation", sb.ToString());
    }

    private void PlaytimeTracker()
    {
        var dir = _dialog.BrowseFolder("RetroArch-Spielzeit-Ordner wählen (runtime_log)");
        if (dir is null) return;
        var lrtlFiles = Directory.GetFiles(dir, "*.lrtl", SearchOption.AllDirectories);
        if (lrtlFiles.Length == 0)
        { _dialog.Info("Keine .lrtl Spielzeit-Dateien gefunden.", "Spielzeit-Tracker"); return; }
        var sb = new StringBuilder();
        sb.AppendLine($"Spielzeit-Tracker: {lrtlFiles.Length} Dateien\n");
        sb.AppendLine("Hinweis: Es werden nur RetroArch .lrtl-Dateien unterstützt.\n");
        foreach (var f in lrtlFiles.Take(20))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var lines = File.ReadAllLines(f);
            sb.AppendLine($"  {name}: {lines.Length} Einträge");
        }
        _dialog.ShowText("Spielzeit-Tracker", sb.ToString());
    }

    private void CollectionSharing()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Keine Daten zum Teilen.", "WARN"); return; }
        var path = _dialog.SaveFile("Sammlung exportieren", "JSON (*.json)|*.json|HTML (*.html)|*.html", "meine-sammlung.json");
        if (path is null) return;
        var entries = _vm.LastCandidates.Where(c => c.Category == "GAME")
            .Select(c => new { Name = Path.GetFileNameWithoutExtension(c.MainPath), c.Region, c.Extension, SizeMB = c.SizeBytes / 1048576.0 }).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        _vm.AddLog($"Sammlung exportiert: {path} ({entries.Count} Spiele, keine Pfade/Hashes)", "INFO");
    }

    private void VirtualFolderPreview()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        _dialog.ShowText("Virtuelle Ordner", FeatureService.BuildVirtualFolderPreview(_vm.LastCandidates));
    }

    // ═══ SICHERHEIT & INTEGRITÄT ════════════════════════════════════════

    private async Task IntegrityMonitorAsync()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var createBaseline = _dialog.Confirm("Integritäts-Baseline erstellen oder prüfen?\n\nJA = Neue Baseline erstellen\nNEIN = Gegen Baseline prüfen", "Integritäts-Monitor");
        if (createBaseline)
        {
            _vm.AddLog("Erstelle Integritäts-Baseline…", "INFO");
            var paths = _vm.LastCandidates.Select(c => c.MainPath).ToList();
            var progress = new Progress<string>(msg => _vm.ProgressText = msg);
            try
            {
                var baseline = await FeatureService.CreateBaseline(paths, progress);
                _vm.AddLog($"Baseline erstellt: {baseline.Count} Dateien", "INFO");
            }
            catch (Exception ex) { _vm.AddLog($"Baseline-Fehler: {ex.Message}", "ERROR"); }
        }
        else
        {
            _vm.AddLog("Prüfe Integrität…", "INFO");
            var progress = new Progress<string>(msg => _vm.ProgressText = msg);
            try
            {
                var check = await FeatureService.CheckIntegrity(progress);
                _dialog.ShowText("Integritäts-Check", $"Ergebnis:\n\n" +
                    $"Intakt: {check.Intact.Count}\nGeändert: {check.Changed.Count}\nFehlend: {check.Missing.Count}\n" +
                    $"Bit-Rot-Risiko: {(check.BitRotRisk ? "⚠ JA" : "Nein")}");
            }
            catch (Exception ex) { _vm.AddLog($"Integritäts-Fehler: {ex.Message}", "ERROR"); }
        }
    }

    private void BackupManager()
    {
        var backupRoot = _dialog.BrowseFolder("Backup-Zielordner wählen");
        if (backupRoot is null) return;
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Keine Dateien für Backup.", "WARN"); return; }
        var winners = _vm.LastDedupeGroups.Select(g => g.Winner.MainPath).ToList();
        if (!_dialog.Confirm($"{winners.Count} Winner-Dateien sichern nach:\n{backupRoot}", "Backup bestätigen")) return;
        try
        {
            var sessionDir = FeatureService.CreateBackup(winners, backupRoot, "winners");
            _vm.AddLog($"Backup erstellt: {sessionDir} ({winners.Count} Dateien)", "INFO");
        }
        catch (Exception ex) { _vm.AddLog($"Backup-Fehler: {ex.Message}", "ERROR"); }
    }

    private void Quarantine()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var quarantined = _vm.LastCandidates.Where(c =>
            c.Category == "JUNK" || (!c.DatMatch && c.Region == "UNKNOWN")).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"Quarantäne-Kandidaten: {quarantined.Count}\n");
        sb.AppendLine("Kriterien: Junk-Kategorie ODER (kein DAT-Match + Unbekannte Region)\n");
        foreach (var q in quarantined.Take(30))
            sb.AppendLine($"  {Path.GetFileName(q.MainPath),-50} [{q.Category}] {q.Region}");
        if (quarantined.Count > 30)
            sb.AppendLine($"\n  … und {quarantined.Count - 30} weitere");
        _dialog.ShowText("Quarantäne", sb.ToString());
    }

    private void RuleEngine()
    {
        try { _dialog.ShowText("Regel-Engine", FeatureService.BuildRuleEngineReport()); }
        catch (Exception ex) { _vm.AddLog($"Fehler beim Laden der Regeln: {ex.Message}", "ERROR"); }
    }

    private void PatchEngine()
    {
        var patchPath = _dialog.BrowseFile("Patch-Datei wählen", "Patches (*.ips;*.bps;*.ups)|*.ips;*.bps;*.ups|Alle (*.*)|*.*");
        if (patchPath is null) return;
        var format = FeatureService.DetectPatchFormat(patchPath);
        if (format is null)
        { _vm.AddLog($"Unbekanntes Patch-Format: {Path.GetFileName(patchPath)}", "WARN"); return; }
        _vm.AddLog($"Patch erkannt: {format} – {Path.GetFileName(patchPath)}", "INFO");
        _dialog.Info($"Patch-Datei: {Path.GetFileName(patchPath)}\nFormat: {format}\n\nUm den Patch anzuwenden, wähle die Ziel-ROM aus.", "Patch-Engine");
    }

    private void HeaderRepair()
    {
        var path = _dialog.BrowseFile("ROM für Header-Reparatur wählen",
            "ROMs (*.nes;*.sfc;*.smc)|*.nes;*.sfc;*.smc|Alle (*.*)|*.*");
        if (path is null) return;
        path = Path.GetFullPath(path);
        var header = FeatureService.AnalyzeHeader(path);
        if (header is null) { _vm.AddLog("Header nicht lesbar.", "ERROR"); return; }

        if (header.Platform == "NES")
        {
            try
            {
                var headerBuf = new byte[16];
                using (var hfs = File.OpenRead(path))
                { if (hfs.Read(headerBuf, 0, 16) < 16) { _vm.AddLog("NES-Header: Datei zu klein.", "ERROR"); return; } }
                bool hasDirtyBytes = (headerBuf[12] != 0 || headerBuf[13] != 0 || headerBuf[14] != 0 || headerBuf[15] != 0);
                if (hasDirtyBytes)
                {
                    var confirm = _dialog.Confirm(
                        $"NES-Header hat unsaubere Bytes (12-15).\n\nDatei: {Path.GetFileName(path)}\n" +
                        $"Byte 12-15: {headerBuf[12]:X2} {headerBuf[13]:X2} {headerBuf[14]:X2} {headerBuf[15]:X2}\n\n" +
                        "Bytes 12-15 auf 0x00 setzen?\n(Backup wird erstellt)", "Header-Reparatur");
                    if (confirm)
                    {
                        var backupPath = path + $".{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                        File.Copy(path, backupPath, overwrite: false);
                        _vm.AddLog($"Backup erstellt: {backupPath}", "INFO");
                        using var patchFs = File.OpenWrite(path);
                        patchFs.Seek(12, SeekOrigin.Begin);
                        patchFs.Write(new byte[4], 0, 4);
                        _vm.AddLog("NES-Header repariert: Bytes 12-15 genullt.", "INFO");
                    }
                }
                else
                    _dialog.Info($"NES-Header ist sauber. Keine Reparatur nötig.\n\n{header.Details}", "Header-Reparatur");
            }
            catch (Exception ex) { _vm.AddLog($"Header-Reparatur fehlgeschlagen: {ex.Message}", "ERROR"); }
            return;
        }

        if (header.Platform == "SNES")
        {
            try
            {
                var fileInfo = new FileInfo(path);
                bool hasCopierHeader = fileInfo.Length % 1024 == 512;
                if (hasCopierHeader)
                {
                    var confirm = _dialog.Confirm(
                        $"SNES-ROM hat einen Copier-Header (512 Byte).\n\nDatei: {Path.GetFileName(path)}\n" +
                        $"Größe: {fileInfo.Length} Bytes ({fileInfo.Length % 1024} Byte Überschuss)\n\n" +
                        "Copier-Header (erste 512 Bytes) entfernen?\n(Backup wird erstellt)", "Header-Reparatur");
                    if (confirm)
                    {
                        var backupPath = path + $".{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                        File.Copy(path, backupPath, overwrite: false);
                        _vm.AddLog($"Backup erstellt: {backupPath}", "INFO");
                        var data = File.ReadAllBytes(path);
                        var trimmed = data[512..];
                        File.WriteAllBytes(path, trimmed);
                        _vm.AddLog($"SNES Copier-Header entfernt: {fileInfo.Length} → {trimmed.Length} Bytes.", "INFO");
                    }
                }
                else
                    _dialog.Info($"SNES-ROM hat keinen Copier-Header. Keine Reparatur nötig.\n\n{header.Details}", "Header-Reparatur");
            }
            catch (Exception ex) { _vm.AddLog($"Header-Reparatur fehlgeschlagen: {ex.Message}", "ERROR"); }
            return;
        }

        _dialog.ShowText("Header-Reparatur", $"Datei: {Path.GetFileName(path)}\n\nPlattform: {header.Platform}\nFormat: {header.Format}\n{header.Details}\n\nAutomatische Reparatur ist nur für NES und SNES verfügbar.");
    }

    // ═══ WORKFLOW & AUTOMATISIERUNG ═════════════════════════════════════

    private void SplitPanelPreview()
    {
        if (_vm.LastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Daten für Split-Panel.", "WARN"); return; }
        _dialog.ShowText("Split-Panel", FeatureService.BuildSplitPanelPreview(_vm.LastDedupeGroups));
    }

    private void FilterBuilder()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var input = _dialog.ShowInputBox(
            "Filter-Ausdruck eingeben (Feld=Wert, Feld>Wert, Feld<Wert):\n\nBeispiele:\n  region=US\n  category=JUNK\n  sizemb>100\n  extension=.chd\n  datmatch=true",
            "Filter-Builder", "region=US");
        if (string.IsNullOrWhiteSpace(input)) return;

        string field, op, value;
        if (input.Contains(">=")) { var p = input.Split(">=", 2); field = p[0].Trim().ToLowerInvariant(); op = ">="; value = p[1].Trim(); }
        else if (input.Contains("<=")) { var p = input.Split("<=", 2); field = p[0].Trim().ToLowerInvariant(); op = "<="; value = p[1].Trim(); }
        else if (input.Contains('>')) { var p = input.Split('>', 2); field = p[0].Trim().ToLowerInvariant(); op = ">"; value = p[1].Trim(); }
        else if (input.Contains('<')) { var p = input.Split('<', 2); field = p[0].Trim().ToLowerInvariant(); op = "<"; value = p[1].Trim(); }
        else if (input.Contains('=')) { var p = input.Split('=', 2); field = p[0].Trim().ToLowerInvariant(); op = "="; value = p[1].Trim(); }
        else { _vm.AddLog($"Ungültiger Filter-Ausdruck: {input}", "WARN"); return; }

        var filtered = _vm.LastCandidates.Where(c =>
        {
            string fieldValue = field switch
            {
                "region" => c.Region, "category" => c.Category, "extension" or "ext" => c.Extension,
                "gamekey" or "game" => c.GameKey, "type" or "consolekey" or "console" => c.ConsoleKey,
                "datmatch" or "dat" => c.DatMatch.ToString(),
                "sizemb" => (c.SizeBytes / 1048576.0).ToString("F1"),
                "sizebytes" or "size" => c.SizeBytes.ToString(),
                "filename" or "name" => Path.GetFileName(c.MainPath), _ => ""
            };
            if (op == "=") return fieldValue.Contains(value, StringComparison.OrdinalIgnoreCase);
            if (double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var numVal) &&
                double.TryParse(fieldValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fieldNum))
                return op switch { ">" => fieldNum > numVal, "<" => fieldNum < numVal, ">=" => fieldNum >= numVal, "<=" => fieldNum <= numVal, _ => false };
            return false;
        }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Filter-Builder: {field} {op} {value}");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Gesamt: {_vm.LastCandidates.Count}");
        sb.AppendLine($"  Gefiltert: {filtered.Count}\n");
        foreach (var r in filtered.Take(50))
            sb.AppendLine($"  {Path.GetFileName(r.MainPath),-45} [{r.Region}] {r.Extension} {r.Category} {FeatureService.FormatSize(r.SizeBytes)}");
        if (filtered.Count > 50)
            sb.AppendLine($"\n  … und {filtered.Count - 50} weitere");
        _dialog.ShowText("Filter-Builder", sb.ToString());
    }

    private void SortTemplates()
    {
        var templates = FeatureService.GetSortTemplates();
        var sb = new StringBuilder();
        sb.AppendLine("Sortierungs-Vorlagen\n");
        foreach (var (name, pattern) in templates)
            sb.AppendLine($"  {name,-20} → {pattern}");
        sb.AppendLine("\n  Legende: {console} = Konsolenname, {filename} = Dateiname");
        _dialog.ShowText("Sort-Templates", sb.ToString());
    }

    private void PipelineEngine()
    {
        if (_vm.LastRunResult is not null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Pipeline-Engine — Letzter Lauf");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine($"\n  Status: {_vm.LastRunResult.Status}");
            sb.AppendLine($"  Dauer:  {_vm.LastRunResult.DurationMs / 1000.0:F1}s\n");
            sb.AppendLine($"  {"Phase",-20} {"Status",-15} {"Details"}");
            sb.AppendLine($"  {new string('-', 20)} {new string('-', 15)} {new string('-', 30)}");
            sb.AppendLine($"  {"Scan",-20} {"OK",-15} {_vm.LastRunResult.TotalFilesScanned} Dateien");
            sb.AppendLine($"  {"Dedupe",-20} {"OK",-15} {_vm.LastRunResult.GroupCount} Gruppen, {_vm.LastRunResult.WinnerCount} Winner");
            var junkCount = _vm.LastCandidates.Count(c => c.Category == "JUNK");
            sb.AppendLine($"  {"Junk-Erkennung",-20} {"OK",-15} {junkCount} Junk-Dateien");
            if (_vm.LastRunResult.ConsoleSortResult is { }) sb.AppendLine($"  {"Konsolen-Sort",-20} {"OK",-15} sortiert");
            else sb.AppendLine($"  {"Konsolen-Sort",-20} {"Übersprungen",-15}");
            if (_vm.LastRunResult.ConvertedCount > 0) sb.AppendLine($"  {"Konvertierung",-20} {"OK",-15} {_vm.LastRunResult.ConvertedCount} konvertiert");
            else sb.AppendLine($"  {"Konvertierung",-20} {"Übersprungen",-15}");
            if (_vm.LastRunResult.MoveResult is { } mv) sb.AppendLine($"  {"Move",-20} {(mv.FailCount > 0 ? "WARNUNG" : "OK"),-15} {mv.MoveCount} verschoben, {mv.FailCount} Fehler");
            else sb.AppendLine($"  {"Move",-20} {"DryRun",-15} keine Änderungen");
            _dialog.ShowText("Pipeline-Engine", sb.ToString());
        }
        else
        {
            _dialog.ShowText("Pipeline-Engine", "Pipeline-Engine\n\nBedingte Multi-Step-Pipelines:\n\n" +
                "  1. Scan → Dateien erfassen\n  2. Dedupe → Duplikate erkennen\n  3. Sort → Nach Konsole sortieren\n" +
                "  4. Convert → Formate konvertieren\n  5. Verify → Konvertierung prüfen\n\n" +
                "Jeder Schritt kann übersprungen werden.\nDryRun-aware: Kein Schreibzugriff im DryRun-Modus.\n\n" +
                "Starte einen Lauf, um Pipeline-Ergebnisse zu sehen.");
        }
    }

    private void SchedulerAdvanced()
    {
        var input = _dialog.ShowInputBox(
            "Cron-Expression eingeben (5 Felder: Min Std Tag Mon Wochentag):\n\nBeispiele:\n0 3 * * * → Täglich um 3:00\n0 */6 * * * → Alle 6 Stunden\n0 0 * * 0 → Sonntags um Mitternacht",
            "Cron-Tester", "0 3 * * *");
        if (string.IsNullOrWhiteSpace(input)) return;
        var now = DateTime.Now;
        var matches = FeatureService.TestCronMatch(input, now);
        _vm.AddLog($"Cron-Tester: '{input}' → aktuell {(matches ? "aktiv" : "nicht aktiv")}", "INFO");
        _dialog.Info($"Cron-Expression: {input}\n\nAktuelle Zeit: {now:HH:mm}\nMatch: {(matches ? "JA" : "Nein")}\n\nHinweis: Dies ist ein Cron-Tester. Automatische Ausführung ist nicht implementiert.", "Cron-Tester");
    }

    private void RulePackSharing()
    {
        var doExport = _dialog.Confirm("Regel-Pakete\n\nJA = Exportieren (rules.json speichern)\nNEIN = Importieren (rules.json laden)", "Regel-Pakete");
        var dataDir = FeatureService.ResolveDataDirectory() ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var rulesPath = Path.Combine(dataDir, "rules.json");
        if (doExport)
        {
            if (!File.Exists(rulesPath))
            { _dialog.Info("Keine rules.json zum Exportieren gefunden.\n\nErstelle zuerst Regeln in data/rules.json.", "Export"); return; }
            var savePath = _dialog.SaveFile("Regeln exportieren", "JSON (*.json)|*.json", "rules-export.json");
            if (savePath is null) return;
            try { File.Copy(rulesPath, savePath, overwrite: true); _vm.AddLog($"Regeln exportiert: {savePath}", "INFO"); }
            catch (Exception ex) { _vm.AddLog($"Export fehlgeschlagen: {ex.Message}", "ERROR"); }
        }
        else
        {
            var importPath = _dialog.BrowseFile("Regel-Paket importieren", "JSON (*.json)|*.json");
            if (importPath is null) return;
            try
            {
                var json = File.ReadAllText(importPath);
                JsonDocument.Parse(json).Dispose();
                Directory.CreateDirectory(dataDir);
                File.Copy(importPath, rulesPath, overwrite: true);
                _vm.AddLog($"Regeln importiert: {Path.GetFileName(importPath)} nach {rulesPath}", "INFO");
            }
            catch (JsonException) { _vm.AddLog("Import fehlgeschlagen: Ungültiges JSON-Format.", "ERROR"); }
            catch (Exception ex) { _vm.AddLog($"Import fehlgeschlagen: {ex.Message}", "ERROR"); }
        }
    }

    private void ArcadeMergeSplit()
    {
        var datPath = _dialog.BrowseFile("MAME/FBNEO DAT wählen", "DAT (*.dat;*.xml)|*.dat;*.xml");
        if (datPath is null) return;
        _vm.AddLog($"Arcade Merge/Split: Analysiere {Path.GetFileName(datPath)}…", "INFO");
        try
        {
            var report = FeatureService.BuildArcadeMergeSplitReport(datPath);
            _dialog.ShowText("Arcade Merge/Split", report);
            _vm.AddLog("Arcade-Analyse abgeschlossen.", "INFO");
        }
        catch (Exception ex)
        {
            _vm.AddLog($"Arcade Merge/Split Fehler: {ex.Message}", "ERROR");
            _dialog.Error($"Fehler beim Parsen der DAT:\n\n{ex.Message}", "Arcade Merge/Split");
        }
    }

    // ═══ EXPORT & INTEGRATION ═══════════════════════════════════════════

    private void PdfReport()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var path = _dialog.SaveFile("PDF-Report speichern", "HTML (*.html)|*.html", "report.html");
        if (path is null) return;
        var summary = new ReportSummary
        {
            Mode = _vm.DryRun ? "DryRun" : "Move",
            TotalFiles = _vm.LastCandidates.Count,
            KeepCount = _vm.LastDedupeGroups.Count,
            MoveCount = _vm.LastDedupeGroups.Sum(g => g.Losers.Count),
            JunkCount = _vm.LastCandidates.Count(c => c.Category == "JUNK"),
            GroupCount = _vm.LastDedupeGroups.Count,
            Duration = TimeSpan.FromMilliseconds(_vm.LastRunResult?.DurationMs ?? 0)
        };
        var entries = _vm.LastCandidates.Select(c => new ReportEntry
        {
            GameKey = c.GameKey, Action = c.Category == "JUNK" ? "JUNK" : "KEEP",
            Category = c.Category, Region = c.Region, FilePath = c.MainPath,
            FileName = Path.GetFileName(c.MainPath), Extension = c.Extension,
            SizeBytes = c.SizeBytes, RegionScore = c.RegionScore, FormatScore = c.FormatScore,
            VersionScore = c.VersionScore, DatMatch = c.DatMatch
        }).ToList();
        try
        {
            ReportGenerator.WriteHtmlToFile(path, Path.GetDirectoryName(path) ?? ".", summary, entries);
            _vm.AddLog($"Report erstellt: {path} (Im Browser drucken → PDF)", "INFO");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { _vm.AddLog($"Report-Fehler: {ex.Message}", "ERROR"); }
    }

    private void LauncherIntegration()
    {
        if (_vm.LastDedupeGroups.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var path = _dialog.SaveFile("RetroArch Playlist exportieren", "Playlist (*.lpl)|*.lpl", "RomCleanup.lpl");
        if (path is null) return;
        var winners = _vm.LastDedupeGroups.Select(g => g.Winner).ToList();
        var json = FeatureService.ExportRetroArchPlaylist(winners, Path.GetFileNameWithoutExtension(path));
        File.WriteAllText(path, json);
        _vm.AddLog($"Playlist exportiert: {path} ({winners.Count} Einträge)", "INFO");
    }

    private void ToolImport()
    {
        var path = _dialog.BrowseFile("DAT-Datei importieren (ClrMamePro, RomVault, Logiqx)",
            "DAT (*.dat;*.xml)|*.dat;*.xml|Alle (*.*)|*.*");
        if (path is null) return;
        _vm.AddLog($"Tool-Import: {Path.GetFileName(path)}", "INFO");
        if (string.IsNullOrWhiteSpace(_vm.DatRoot))
        { _dialog.Error("DAT-Root ist nicht konfiguriert. Bitte zuerst den DAT-Root-Ordner setzen.", "Tool-Import"); return; }
        try
        {
            var safeName = Path.GetFileName(path);
            var targetPath = Path.GetFullPath(Path.Combine(_vm.DatRoot, safeName));
            if (!targetPath.StartsWith(Path.GetFullPath(_vm.DatRoot), StringComparison.OrdinalIgnoreCase))
            { _vm.AddLog("DAT-Import blockiert: Pfad außerhalb des DatRoot.", "ERROR"); return; }
            File.Copy(path, targetPath, overwrite: true);
            _vm.AddLog($"DAT importiert nach: {targetPath}", "INFO");
            _dialog.Info($"DAT erfolgreich importiert:\n\n  Quelle: {path}\n  Ziel: {targetPath}", "Tool-Import");
        }
        catch (Exception ex) { _vm.AddLog($"DAT-Import fehlgeschlagen: {ex.Message}", "ERROR"); }
    }

    // ═══ INFRASTRUKTUR & DEPLOYMENT ═════════════════════════════════════

    private void StorageTiering()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        _dialog.ShowText("Storage-Tiering", FeatureService.AnalyzeStorageTiers(_vm.LastCandidates));
    }

    private void NasOptimization()
    {
        if (_vm.Roots.Count == 0)
        { _vm.AddLog("Keine Roots konfiguriert.", "WARN"); return; }
        _dialog.ShowText("NAS-Optimierung", FeatureService.GetNasInfo(_vm.Roots.ToList()));
    }

    private void FtpSource()
    {
        var input = _dialog.ShowInputBox("FTP/SFTP-URL eingeben:\n\nFormat: ftp://host/pfad oder sftp://host/pfad",
            "FTP-Quelle", "ftp://");
        if (string.IsNullOrWhiteSpace(input) || input == "ftp://") return;
        var isValid = input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                      input.StartsWith("sftp://", StringComparison.OrdinalIgnoreCase);
        if (!isValid) { _vm.AddLog($"Ungültige FTP-URL: {input} (muss mit ftp:// oder sftp:// beginnen)", "ERROR"); return; }
        if (input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
        {
            var useSftp = _dialog.Confirm(
                "⚠ FTP überträgt Daten unverschlüsselt.\nZugangsdaten und Dateien können abgefangen werden.\n\nEmpfehlung: Verwende SFTP (sftp://) stattdessen.\n\nTrotzdem mit unverschlüsseltem FTP fortfahren?",
                "Sicherheitshinweis");
            if (!useSftp) return;
        }
        try
        {
            var uri = new Uri(input);
            var sb = new StringBuilder();
            sb.AppendLine("FTP-Quelle konfiguriert\n");
            sb.AppendLine($"  Protokoll: {uri.Scheme.ToUpperInvariant()}");
            sb.AppendLine($"  Host:      {uri.Host}");
            sb.AppendLine($"  Port:      {(uri.Port > 0 ? uri.Port : (uri.Scheme == "sftp" ? 22 : 21))}");
            sb.AppendLine($"  Pfad:      {uri.AbsolutePath}");
            sb.AppendLine("\n  ℹ FTP-Download ist noch nicht implementiert.\n  Aktuell wird die URL nur registriert und angezeigt.\n  Geplantes Feature: Dateien vor Verarbeitung lokal cachen.");
            _dialog.ShowText("FTP-Quelle", sb.ToString());
            _vm.AddLog($"FTP-Quelle registriert: {uri.Host}{uri.AbsolutePath}", "INFO");
        }
        catch (Exception ex) { _vm.AddLog($"FTP-URL ungültig: {ex.Message}", "ERROR"); }
    }

    private void CloudSync()
    {
        var oneDrive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive");
        var dropbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox");
        var sb = new StringBuilder();
        sb.AppendLine("Cloud-Sync Status\n");
        sb.AppendLine($"  OneDrive: {(Directory.Exists(oneDrive) ? "✓ Gefunden" : "✗ Nicht gefunden")}");
        sb.AppendLine($"  Dropbox:  {(Directory.Exists(dropbox) ? "✓ Gefunden" : "✗ Nicht gefunden")}");
        sb.AppendLine("\n  ℹ Nur Statusanzeige – Cloud-Sync ist in Planung.\n  Geplant: Metadaten-Sync (Einstellungen, Profile).\n  Keine ROM-Dateien werden hochgeladen.");
        _dialog.ShowText("Cloud-Sync (Vorschau)", sb.ToString());
    }

    private void PluginMarketplace()
    {
        var pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginDir))
        {
            Directory.CreateDirectory(pluginDir);
            _vm.AddLog($"Plugin-Verzeichnis erstellt: {pluginDir}", "INFO");
        }
        var manifests = Directory.GetFiles(pluginDir, "*.json", SearchOption.AllDirectories);
        var dlls = Directory.GetFiles(pluginDir, "*.dll", SearchOption.AllDirectories);
        var sb = new StringBuilder();
        sb.AppendLine("Plugin-Manager (Coming Soon)\n");
        sb.AppendLine("  ℹ Das Plugin-System ist in Planung und noch nicht funktionsfähig.");
        sb.AppendLine($"  Plugin-Verzeichnis: {pluginDir}\n");
        sb.AppendLine($"  Manifeste:   {manifests.Length}");
        sb.AppendLine($"  DLLs:        {dlls.Length}\n");
        if (manifests.Length == 0 && dlls.Length == 0)
        {
            sb.AppendLine("  Keine Plugins installiert.\n");
            sb.AppendLine("  Plugin-Struktur:\n    plugins/\n      mein-plugin/\n        manifest.json\n        MeinPlugin.dll\n");
            sb.AppendLine("  Manifest-Format:\n    {\n      \"name\": \"Mein Plugin\",\n      \"version\": \"1.0.0\",\n      \"type\": \"console|format|report\"\n    }");
        }
        else
        {
            foreach (var manifest in manifests)
            {
                try
                {
                    var json = File.ReadAllText(manifest);
                    using var doc = JsonDocument.Parse(json);
                    var name = doc.RootElement.TryGetProperty("name", out var np) ? np.GetString() : Path.GetFileName(manifest);
                    var ver = doc.RootElement.TryGetProperty("version", out var vp) ? vp.GetString() : "?";
                    var type = doc.RootElement.TryGetProperty("type", out var tp) ? tp.GetString() : "?";
                    sb.AppendLine($"  [{type}] {name} v{ver}");
                    sb.AppendLine($"         {Path.GetDirectoryName(manifest)}");
                }
                catch { sb.AppendLine($"  [?] {Path.GetFileName(manifest)} (manifest ungültig)"); }
            }
            if (dlls.Length > 0) { sb.AppendLine($"\n  DLLs:"); foreach (var dll in dlls) sb.AppendLine($"    {Path.GetFileName(dll)}"); }
        }
        _dialog.ShowText("Plugin-Manager", sb.ToString());
        if (_dialog.Confirm($"Plugin-Verzeichnis im Explorer öffnen?\n\n{pluginDir}", "Plugins"))
            Process.Start(new ProcessStartInfo(pluginDir) { UseShellExecute = true });
    }

    private void PortableMode()
    {
        var isPortable = FeatureService.IsPortableMode();
        var sb = new StringBuilder();
        sb.AppendLine("Portable-Modus\n");
        sb.AppendLine($"  Aktueller Modus: {(isPortable ? "PORTABEL" : "Standard (AppData)")}");
        sb.AppendLine($"  Programm-Verzeichnis: {AppContext.BaseDirectory}");
        if (isPortable) sb.AppendLine($"  Settings-Ordner: {Path.Combine(AppContext.BaseDirectory, ".romcleanup")}");
        else
        {
            sb.AppendLine($"  Settings-Ordner: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RomCleanupRegionDedupe")}");
            sb.AppendLine("\n  Tipp: Erstelle '.portable' im Programmverzeichnis für Portable-Modus.");
        }
        _dialog.ShowText("Portable-Modus", sb.ToString());
    }

    private void DockerContainer()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Docker-Konfiguration\n");
        sb.AppendLine("═══ Dockerfile ═══");
        sb.AppendLine(FeatureService.GenerateDockerfile());
        sb.AppendLine("\n═══ docker-compose.yml ═══");
        sb.AppendLine(FeatureService.GenerateDockerCompose());
        _dialog.ShowText("Docker", sb.ToString());
        var savePath = _dialog.SaveFile("Docker-Dateien speichern", "Dockerfile|Dockerfile|YAML (*.yml)|*.yml", "Dockerfile");
        if (savePath is not null)
        {
            var ext = Path.GetExtension(savePath).ToLowerInvariant();
            var content = ext == ".yml" ? FeatureService.GenerateDockerCompose() : FeatureService.GenerateDockerfile();
            File.WriteAllText(savePath, content);
            _vm.AddLog($"Docker-Datei gespeichert: {savePath}", "INFO");
        }
    }

    private void WindowsContextMenu()
    {
        var regScript = FeatureService.GetContextMenuRegistryScript();
        var path = _dialog.SaveFile("Registry-Skript speichern", "Registry (*.reg)|*.reg", "romcleanup-context-menu.reg");
        if (path is null) return;
        File.WriteAllText(path, regScript);
        _vm.AddLog($"Kontextmenü-Registry exportiert: {path}", "INFO");
        _dialog.Info($"Registry-Skript gespeichert:\n{path}\n\nDoppelklicke die .reg-Datei, um das Kontextmenü zu installieren.\n\n⚠ Das Skript enthält den absoluten Pfad zur aktuellen EXE-Datei.\nBei Verschiebung der Anwendung muss das Skript neu generiert werden.\n\nEinträge:\n• ROM Cleanup – DryRun Scan\n• ROM Cleanup – Move Sort", "Kontextmenü");
    }

    private void HardlinkMode()
    {
        if (_vm.LastDedupeGroups.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var estimate = FeatureService.GetHardlinkEstimate(_vm.LastDedupeGroups);
        var firstRoot = _vm.LastDedupeGroups.FirstOrDefault()?.Winner.MainPath;
        var isNtfs = false;
        if (firstRoot is not null)
        {
            try
            {
                var driveRoot = Path.GetPathRoot(firstRoot);
                if (driveRoot is not null) isNtfs = new DriveInfo(driveRoot).DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }
        _dialog.ShowText("Hardlink-Modus", $"Hardlink-Modus\n\n{estimate}\n\nNTFS-Unterstützung: {(isNtfs ? "Verfügbar" : "Nicht verfügbar")}\n\nHardlinks teilen den Speicherplatz auf Dateisystemebene.\nBeide Pfade zeigen auf dieselben Daten – kein zusätzlicher Speicher.");
    }

    private void MultiInstanceSync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Multi-Instanz-Synchronisation");
        sb.AppendLine(new string('═', 50));
        var locks = new List<(string path, string content)>();
        foreach (var root in _vm.Roots)
        {
            var lockFile = Path.Combine(root, ".romcleanup.lock");
            if (File.Exists(lockFile))
            {
                try { locks.Add((lockFile, File.ReadAllText(lockFile))); }
                catch { locks.Add((lockFile, "(nicht lesbar)")); }
            }
        }
        sb.AppendLine($"\n  Konfigurierte Roots: {_vm.Roots.Count}");
        sb.AppendLine($"  Aktive Locks:       {locks.Count}");
        if (locks.Count > 0)
        {
            sb.AppendLine("\n  Gefundene Lock-Dateien:");
            foreach (var (path, content) in locks) { sb.AppendLine($"    {path}"); sb.AppendLine($"      {content}"); }
        }
        else sb.AppendLine("\n  Keine aktiven Locks gefunden.");
        sb.AppendLine($"\n  Diese Instanz:\n    PID:      {Environment.ProcessId}\n    Hostname: {Environment.MachineName}\n    Status:   {(_vm.IsBusy ? "LÄUFT" : "Bereit")}");
        _dialog.ShowText("Multi-Instanz", sb.ToString());
        if (locks.Count > 0 && _dialog.Confirm($"{locks.Count} Lock-Datei(en) gefunden.\n\nAbgelaufene Locks entfernen?", "Multi-Instanz"))
        {
            var removed = 0;
            foreach (var (path, _) in locks)
            {
                try { File.Delete(path); removed++; } catch { }
            }
            _vm.AddLog($"Multi-Instanz: {removed} Lock(s) entfernt", "INFO");
        }
    }

    // ═══ WINDOW-LEVEL COMMANDS (require IWindowHost) ════════════════════

    private void CommandPalette()
    {
        var input = _dialog.ShowInputBox("Befehl suchen:", "Command-Palette", "");
        if (string.IsNullOrWhiteSpace(input)) return;
        var results = FeatureService.SearchCommands(input);
        if (results.Count == 0)
        { _vm.AddLog($"Kein Befehl gefunden für: {input}", "WARN"); return; }

        _dialog.ShowText("Command-Palette", FeatureService.BuildCommandPaletteReport(input, results));
        if (results[0].score == 0) ExecuteCommand(results[0].key);
    }

    private void ExecuteCommand(string key)
    {
        switch (key)
        {
            case "dryrun": if (!_vm.IsBusy) { _vm.DryRun = true; _vm.RunCommand.Execute(null); } break;
            case "move": if (!_vm.IsBusy) { _vm.DryRun = false; _vm.RunCommand.Execute(null); } break;
            case "cancel": _vm.CancelCommand.Execute(null); break;
            case "rollback": _vm.RollbackCommand.Execute(null); break;
            case "theme": _vm.ThemeToggleCommand.Execute(null); break;
            case "clear-log": _vm.ClearLogCommand.Execute(null); break;
            case "settings": _windowHost?.SelectTab(1); break;
            default: _vm.AddLog($"Befehl: {key}", "INFO"); break;
        }
    }

    private void MobileWebUI()
    {
        var apiProject = FeatureService.FindApiProjectPath();
        if (apiProject is not null)
        {
            if (_dialog.Confirm("REST API starten und Browser öffnen?\n\nhttp://127.0.0.1:5000", "Mobile Web UI"))
            {
                _windowHost?.StartApiProcess(apiProject);
                return;
            }
        }
        else
        {
            _dialog.ShowText("Mobile Web UI", "Mobile Web UI\n\n  API-Projekt nicht gefunden.\n\n" +
                "  Zum manuellen Start:\n    dotnet run --project src/RomCleanup.Api\n\n" +
                "  Dann im Browser öffnen:\n    http://127.0.0.1:5000");
        }
    }

    private void Accessibility()
    {
        if (_windowHost is null) return;
        var isHC = FeatureService.IsHighContrastActive();
        var currentSize = _windowHost.FontSize;

        var input = _dialog.ShowInputBox(
            $"Barrierefreiheit\n\n" +
            $"High-Contrast: {(isHC ? "AKTIV" : "Inaktiv")}\n" +
            $"Aktuelle Schriftgröße: {currentSize}\n\n" +
            "Neue Schriftgröße eingeben (10-24):",
            "Barrierefreiheit", currentSize.ToString("0"));
        if (string.IsNullOrWhiteSpace(input)) return;

        if (double.TryParse(input, System.Globalization.CultureInfo.InvariantCulture, out var newSize) && newSize >= 10 && newSize <= 24)
        {
            _windowHost.FontSize = newSize;
            _vm.AddLog($"Schriftgröße geändert: {newSize}", "INFO");
        }
        else
        {
            _vm.AddLog($"Ungültige Schriftgröße: {input} (erlaubt: 10-24)", "WARN");
        }
    }

    private void ThemeEngine()
    {
        var result = _dialog.YesNoCancel(
            $"Aktuelles Theme: {(_vm.ThemeToggleText.Contains("Dark") ? "Light" : "Dark")}\n\n" +
            "JA = Dark Theme\nNEIN = Light Theme\nAbbrechen = High-Contrast",
            "Theme-Engine");

        switch (result)
        {
            case ConfirmResult.Yes:
                _vm.ThemeToggleCommand.Execute(null);
                _vm.AddLog("Theme gewechselt: Dark", "INFO");
                break;
            case ConfirmResult.No:
                _vm.ThemeToggleCommand.Execute(null);
                _vm.AddLog("Theme gewechselt: Light", "INFO");
                break;
            case ConfirmResult.Cancel:
                _vm.ThemeToggleCommand.Execute(null);
                _vm.AddLog("Theme gewechselt (Toggle)", "INFO");
                break;
        }
    }
}