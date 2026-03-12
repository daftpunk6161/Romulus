using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DragEventArgs = System.Windows.DragEventArgs;
using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Orchestration;
using RomCleanup.Infrastructure.Reporting;
using RomCleanup.Infrastructure.Tools;
using RomCleanup.UI.Wpf.Services;
using RomCleanup.UI.Wpf.Models;
using RomCleanup.UI.Wpf.ViewModels;

namespace RomCleanup.UI.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ThemeService _theme;
    private readonly SettingsService _settings = new();
    private readonly DispatcherTimer _settingsTimer;
    private string? _lastAuditPath;
    private Task? _activeRunTask;

    // Run result tracking for feature buttons
    private IReadOnlyList<RomCandidate> _lastCandidates = Array.Empty<RomCandidate>();
    private IReadOnlyList<DedupeResult> _lastDedupeGroups = Array.Empty<DedupeResult>();
    private RunResult? _lastRunResult;

    // System tray service
    private TrayService? _trayService;

    // Watch-mode service
    private readonly WatchService _watchService = new();

    // Detached API process from Mobile Web UI
    private Process? _apiProcess;
    // Guard against recursive OnClosing calls
    private bool _isClosing;
    // Named handler so we can unsubscribe in cleanup
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _logScrollHandler;

    public MainWindow()
    {
        _theme = new ThemeService();
        _vm = new MainViewModel(_theme, new WpfDialogService());
        DataContext = _vm;

        InitializeComponent();

        // Periodic settings save every 5 minutes (P3-BUG-051 / UX-07)
        _settingsTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _settingsTimer.Tick += (_, _) => _settings.SaveFrom(_vm, _lastAuditPath);
        _settingsTimer.Start();

        Loaded += OnLoaded;
        Closing += OnClosing;

        // Wire orchestration events
        _vm.RunRequested += OnRunRequested;
        _vm.RollbackRequested += OnRollbackRequested;

        // Drag-drop on root list
        listRoots.DragEnter += OnRootsDragEnter;
        listRoots.Drop += OnRootsDrop;

        // Browse buttons (code-behind — not bindable in lightweight MVVM)
        btnBrowseChdman.Click += (_, _) => BrowseToolPath(path => _vm.ToolChdman = path);
        btnBrowseDolphin.Click += (_, _) => BrowseToolPath(path => _vm.ToolDolphin = path);
        btnBrowse7z.Click += (_, _) => BrowseToolPath(path => _vm.Tool7z = path);
        btnBrowsePsxtract.Click += (_, _) => BrowseToolPath(path => _vm.ToolPsxtract = path);
        btnBrowseCiso.Click += (_, _) => BrowseToolPath(path => _vm.ToolCiso = path);
        btnBrowseDat.Click += (_, _) => BrowseFolderPath(path => _vm.DatRoot = path);
        btnBrowseTrash.Click += (_, _) => BrowseFolderPath(path => _vm.TrashRoot = path);
        btnBrowseAudit.Click += (_, _) => BrowseFolderPath(path => _vm.AuditRoot = path);
        btnBrowsePs3.Click += (_, _) => BrowseFolderPath(path => _vm.Ps3DupesRoot = path);

        // ── Functional buttons ──────────────────────────────────────────
        BindFeatureCommand(btnExportLog, "ExportLog");
        btnRefreshReportPreview.Click += OnRefreshReportPreview;
        BindFeatureCommand(btnAutoFindTools, "AutoFindTools");

        // ── Profile/Config buttons ──────────────────────────────────────
        btnProfileSave.Click += (_, _) =>
        {
            if (_settings.SaveFrom(_vm, _lastAuditPath))
                _vm.AddLog("Einstellungen gespeichert.", "INFO");
            else
                _vm.AddLog("Einstellungen konnten nicht gespeichert werden.", "ERROR");
        };
        btnProfileLoad.Click += (_, _) => { _settings.LoadInto(_vm); _vm.RefreshStatus(); _vm.AddLog("Einstellungen geladen.", "INFO"); };
        BindFeatureCommand(btnProfileDelete, "ProfileDelete");
        BindFeatureCommand(btnProfileImport, "ProfileImport");
        BindFeatureCommand(btnConfigDiff, "ConfigDiff");
        BindFeatureCommand(btnExportUnified, "ExportUnified");
        BindFeatureCommand(btnConfigImport, "ConfigImport");

        // ── Quick actions ───────────────────────────────────────────────
        btnQuickPreview.Click += (_, _) => { if (_vm.Roots.Count > 0 && !_vm.IsBusy) { _vm.DryRun = true; _vm.RunCommand.Execute(null); } };
        btnStartMove.Click += (_, _) => { if (_vm.Roots.Count > 0 && !_vm.IsBusy) { _vm.DryRun = false; _vm.RunCommand.Execute(null); } };

        // ── Konfiguration tab misc buttons ──────────────────────────────
        BindFeatureCommand(btnHealthScore, "HealthScore");
        BindFeatureCommand(btnCollectionDiff, "CollectionDiff");
        BindFeatureCommand(btnDuplicateInspector, "DuplicateInspector");
        BindFeatureCommand(btnDuplicateExport, "DuplicateExport");
        BindFeatureCommand(btnExportCsv, "ExportCsv");
        BindFeatureCommand(btnExportExcel, "ExportExcel");
        btnRollbackQuick.Click += (_, _) => { if (_vm.RollbackCommand is RelayCommand rc && rc.CanExecute(null)) rc.Execute(null); else _vm.AddLog("Kein Rollback möglich – keine Audit-Datei.", "WARN"); };
        BindFeatureCommand(btnRollbackUndo, "RollbackUndo");
        BindFeatureCommand(btnRollbackRedo, "RollbackRedo");
        btnWatchApply.Click += OnWatchApply;
        BindFeatureCommand(btnApplyLocale, "ApplyLocale");
        BindFeatureCommand(btnPluginManager, "PluginManager");
        BindFeatureCommand(btnAutoProfile, "AutoProfile");

        // ── Feature tab buttons ─────────────────────────────────────────
        var featureCommands = new FeatureCommandService(_vm, _settings, new WpfDialogService());
        featureCommands.RegisterCommands();
        WireFeatureButtons();
    }

    /// <summary>Wire all Feature-tab buttons with real handlers.</summary>
    private void WireFeatureButtons()
    {
        // ── VM-bound Feature Commands (TASK-111) ────────────────────────
        // Analyse & Berichte
        BindFeatureCommand(btnConversionEstimate, "ConversionEstimate");
        BindFeatureCommand(btnJunkReport, "JunkReport");
        BindFeatureCommand(btnRomFilter, "RomFilter");
        BindFeatureCommand(btnDuplicateHeatmap, "DuplicateHeatmap");
        BindFeatureCommand(btnMissingRom, "MissingRom");
        BindFeatureCommand(btnCrossRootDupe, "CrossRootDupe");
        BindFeatureCommand(btnHeaderAnalysis, "HeaderAnalysis");
        BindFeatureCommand(btnCompleteness, "Completeness");
        BindFeatureCommand(btnDryRunCompare, "DryRunCompare");
        BindFeatureCommand(btnTrendAnalysis, "TrendAnalysis");
        BindFeatureCommand(btnEmulatorCompat, "EmulatorCompat");

        // Konvertierung & Hashing
        BindFeatureCommand(btnConversionPipeline, "ConversionPipeline");
        BindFeatureCommand(btnNKitConvert, "NKitConvert");
        BindFeatureCommand(btnConvertQueue, "ConvertQueue");
        BindFeatureCommand(btnConversionVerify, "ConversionVerify");
        BindFeatureCommand(btnFormatPriority, "FormatPriority");
        BindFeatureCommand(btnParallelHashing, "ParallelHashing");
        BindFeatureCommand(btnGpuHashing, "GpuHashing");

        // DAT & Verifizierung
        BindFeatureCommand(btnDatAutoUpdate, "DatAutoUpdate");
        BindFeatureCommand(btnDatDiffViewer, "DatDiffViewer");
        BindFeatureCommand(btnTosecDat, "TosecDat");
        BindFeatureCommand(btnCustomDatEditor, "CustomDatEditor");
        BindFeatureCommand(btnHashDatabaseExport, "HashDatabaseExport");

        // Sammlungsverwaltung
        BindFeatureCommand(btnCollectionManager, "CollectionManager");
        BindFeatureCommand(btnCloneListViewer, "CloneListViewer");
        BindFeatureCommand(btnCoverScraper, "CoverScraper");
        BindFeatureCommand(btnGenreClassification, "GenreClassification");
        BindFeatureCommand(btnPlaytimeTracker, "PlaytimeTracker");
        BindFeatureCommand(btnCollectionSharing, "CollectionSharing");
        BindFeatureCommand(btnVirtualFolderPreview, "VirtualFolderPreview");

        // Sicherheit & Integrität
        BindFeatureCommand(btnIntegrityMonitor, "IntegrityMonitor");
        BindFeatureCommand(btnBackupManager, "BackupManager");
        BindFeatureCommand(btnQuarantine, "Quarantine");
        BindFeatureCommand(btnRuleEngine, "RuleEngine");
        BindFeatureCommand(btnPatchEngine, "PatchEngine");
        BindFeatureCommand(btnHeaderRepair, "HeaderRepair");

        // Workflow & Automatisierung
        btnCommandPalette.Click += OnCommandPalette;  // needs tabMain TabControl
        BindFeatureCommand(btnSplitPanelPreview, "SplitPanelPreview");
        BindFeatureCommand(btnFilterBuilder, "FilterBuilder");
        BindFeatureCommand(btnSortTemplates, "SortTemplates");
        BindFeatureCommand(btnPipelineEngine, "PipelineEngine");
        btnSystemTray.Click += OnSystemTray;  // needs _trayService + Window
        BindFeatureCommand(btnSchedulerAdvanced, "SchedulerAdvanced");
        BindFeatureCommand(btnRulePackSharing, "RulePackSharing");
        BindFeatureCommand(btnArcadeMergeSplit, "ArcadeMergeSplit");

        // Export & Integration
        BindFeatureCommand(btnPdfReport, "PdfReport");
        BindFeatureCommand(btnLauncherIntegration, "LauncherIntegration");
        BindFeatureCommand(btnToolImport, "ToolImport");

        // Infrastruktur & Deployment
        BindFeatureCommand(btnStorageTiering, "StorageTiering");
        BindFeatureCommand(btnNasOptimization, "NasOptimization");
        BindFeatureCommand(btnFtpSource, "FtpSource");
        BindFeatureCommand(btnCloudSync, "CloudSync");
        BindFeatureCommand(btnPluginMarketplaceFeature, "PluginMarketplaceFeature");
        BindFeatureCommand(btnPortableMode, "PortableMode");
        BindFeatureCommand(btnDockerContainer, "DockerContainer");
        btnMobileWebUI.Click += OnMobileWebUI;   // needs _apiProcess + Dispatcher
        BindFeatureCommand(btnWindowsContextMenu, "WindowsContextMenu");
        BindFeatureCommand(btnHardlinkMode, "HardlinkMode");
        BindFeatureCommand(btnMultiInstanceSync, "MultiInstanceSync");

        // UI & Erscheinungsbild (stay in code-behind — need Window properties)
        btnAccessibility.Click += OnAccessibility;
        btnThemeEngine.Click += OnThemeEngine;
    }

    private static void BindFeatureCommand(System.Windows.Controls.Button button, string key)
    {
        var vm = button.DataContext as MainViewModel;
        // DataContext may not be set yet during constructor — use Loaded fallback
        if (vm?.FeatureCommands.TryGetValue(key, out var cmd) == true)
        {
            button.Command = cmd;
            return;
        }
        button.Loaded += (_, _) =>
        {
            if (button.DataContext is MainViewModel lateVm &&
                lateVm.FeatureCommands.TryGetValue(key, out var lateCmd))
                button.Command = lateCmd;
        };
    }

    // ═══ LIFECYCLE ══════════════════════════════════════════════════════

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings.LoadInto(_vm);
        _lastAuditPath = _settings.LastAuditPath;
        _vm.RefreshStatus();

        // Auto-scroll log to bottom on new entries
        _logScrollHandler = (_, _) =>
        {
            if (listLog.Items.Count > 0)
                listLog.ScrollIntoView(listLog.Items[^1]);
        };
        _vm.LogEntries.CollectionChanged += _logScrollHandler;

        // Auto-load last report preview if available
        if (!string.IsNullOrEmpty(_vm.LastReportPath) && File.Exists(_vm.LastReportPath))
            RefreshReportPreview();
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Guard against recursive calls when Close() is called from within OnClosing
        if (_isClosing) return;

        // P0-VULN-B1: Prevent window close if operation is running
        if (_vm.IsBusy)
        {
            var confirmed = DialogService.Confirm(
                "Ein Lauf ist aktiv. Abbrechen und beenden?",
                "Lauf aktiv");

            if (!confirmed)
            {
                e.Cancel = true;
                return;
            }

            // User chose to close — cancel the operation and wait for it to finish
            _vm.CancelCommand.Execute(null);
            e.Cancel = true; // Cancel this close; re-close after task completes

            var runTask = _activeRunTask;
            if (runTask is not null)
            {
                try { await runTask; } catch { /* already handled in RunCoreAsync */ }
            }

            _settings.SaveFrom(_vm, _lastAuditPath);
            CleanupResources();
            _isClosing = true;
            Close(); // Re-trigger close now that task is done
            return;
        }

        _settings.SaveFrom(_vm, _lastAuditPath);
        CleanupResources();
    }

    /// <summary>Release all resources — called from both OnClosing paths (normal + busy-cancel).</summary>
    private void CleanupResources()
    {
        // Stop periodic save timer
        _settingsTimer.Stop();

        // Unsubscribe VM events to prevent leaks
        _vm.RunRequested -= OnRunRequested;
        _vm.RollbackRequested -= OnRollbackRequested;
        if (_logScrollHandler is not null)
            _vm.LogEntries.CollectionChanged -= _logScrollHandler;

        // System tray
        _trayService?.Dispose();
        _trayService = null;

        // Kill detached API process if running
        try { if (_apiProcess is { HasExited: false }) _apiProcess.Kill(entireProcessTree: true); } catch { }
        try { _apiProcess?.Dispose(); } catch { }
        _apiProcess = null;

        // Dispose file watchers
        _watchService.Dispose();
    }

    // ═══ DRAG & DROP ════════════════════════════════════════════════════
    // NOTE: WPF DragDrop API has no MVVM binding support — code-behind is the standard approach.
    // This is an acceptable exception per ADR-0003.

    private void OnRootsDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Link
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnRootsDrop(object sender, DragEventArgs e)
    {
        if (_vm.IsBusy) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        foreach (var path in paths)
        {
            if (Directory.Exists(path) && !_vm.Roots.Contains(path))
                _vm.Roots.Add(path);
        }
    }

    // ═══ BROWSE HELPERS ═════════════════════════════════════════════════

    private static void BrowseToolPath(Action<string> setter)
    {
        var path = DialogService.BrowseFile("Executable auswählen", "Executables (*.exe)|*.exe|Alle (*.*)|*.*");
        if (path is not null) setter(path);
    }

    private static void BrowseFolderPath(Action<string> setter)
    {
        var path = DialogService.BrowseFolder("Ordner auswählen");
        if (path is not null) setter(path);
    }

    // ═══ RUN ORCHESTRATION ══════════════════════════════════════════════

    private async void OnRunRequested(object? sender, EventArgs e)
    {
        var task = RunCoreAsync();
        _activeRunTask = task;
        try { await task; }
        finally { _activeRunTask = null; }
    }

    private async Task RunCoreAsync()
    {
        // P0-003: Confirm before destructive Move operations
        if (!_vm.DryRun && _vm.ConfirmMove)
        {
            var confirmed = DialogService.Confirm(
                $"Modus 'Move' verschiebt Dateien in den Papierkorb.\n"
                + $"Roots: {string.Join(", ", _vm.Roots)}\n\nFortfahren?",
                "Move bestätigen");
            if (!confirmed)
            {
                _vm.CurrentRunState = RunState.Idle;
                return;
            }
        }

        var ct = _vm.CreateRunCancellation();
        try
        {
            _vm.AddLog("Initialisierung…", "INFO");

            // Move all blocking I/O (file reads, DAT loading) off the UI thread
            var (orchestrator, runOptions, auditPath, reportPath) = await Task.Run(() =>
            {
                // Throttled progress callback (max 1 update/100ms)
                DateTime lastProgressUpdate = DateTime.MinValue;
                return RunService.BuildOrchestrator(_vm, msg =>
                {
                    var now = DateTime.UtcNow;
                    if ((now - lastProgressUpdate).TotalMilliseconds < 100) return;
                    lastProgressUpdate = now;
                    Dispatcher.InvokeAsync(() =>
                    {
                        _vm.ProgressText = msg;
                        if (msg.StartsWith("[") && msg.Contains(']'))
                        {
                            var phase = msg[..(msg.IndexOf(']') + 1)];
                            _vm.PerfPhase = $"Phase: {phase}";
                            var rest = msg[(msg.IndexOf(']') + 1)..].Trim();
                            if (rest.Length > 0) _vm.PerfFile = $"Datei: {rest}";
                        }
                        _vm.AddLog(msg, "INFO");
                    });
                });
            }, ct);

            // Run pipeline on background thread, return result to UI thread
            var svcResult = await Task.Run(
                () => RunService.ExecuteRun(orchestrator, runOptions, auditPath, reportPath, ct), ct);

            // All UI updates on the UI thread — no fire-and-forget Dispatcher.InvokeAsync
            var result = svcResult.Result;
            _lastRunResult = result;
            _lastCandidates = result.AllCandidates;
            _lastDedupeGroups = result.DedupeGroups;

            // Sync to VM for FeatureCommand access (TASK-111)
            _vm.LastRunResult = result;
            _vm.LastCandidates = result.AllCandidates;
            _vm.LastDedupeGroups = result.DedupeGroups;

            _vm.Progress = 100;
            _vm.DashWinners = result.WinnerCount.ToString();
            _vm.DashDupes = result.LoserCount.ToString();
            var junkCount = result.AllCandidates.Count(c => c.Category == "JUNK");
            _vm.DashJunk = junkCount.ToString();
            _vm.DashDuration = $"{result.DurationMs / 1000.0:F1}s";
            var total = result.AllCandidates.Count;
            _vm.HealthScore = total > 0
                ? $"{100.0 * result.WinnerCount / total:F0}%"
                : "–";

            if (result.Status == "blocked")
            {
                _vm.AddLog($"Preflight blockiert: {result.Preflight?.Reason}", "ERROR");
            }
            else
            {
                _vm.AddLog($"Scan: {result.TotalFilesScanned} Dateien", "INFO");
                _vm.AddLog($"Dedupe: Keep={result.WinnerCount}, Move={result.LoserCount}, Junk={junkCount}", "INFO");
                if (result.MoveResult is { } mv)
                    _vm.AddLog($"Verschoben: {mv.MoveCount}, Fehler: {mv.FailCount}", mv.FailCount > 0 ? "WARN" : "INFO");
                if (result.ConvertedCount > 0)
                    _vm.AddLog($"Konvertiert: {result.ConvertedCount}", "INFO");
            }

            _lastAuditPath = auditPath;
            _vm.LastAuditPath = auditPath;

            if (!_vm.DryRun && auditPath is not null && File.Exists(auditPath))
                _vm.PushRollbackUndo(auditPath);

            if (svcResult.ReportPath is not null)
                _vm.AddLog($"Report: {svcResult.ReportPath}", "INFO");

            if (!ct.IsCancellationRequested)
            {
                _vm.AddLog("Lauf abgeschlossen.", "INFO");
                _vm.CompleteRun(true, reportPath);
                RefreshReportPreview();
                PopulateErrorSummary();
            }
            else
            {
                _vm.AddLog("Lauf abgebrochen.", "WARN");
                _vm.CompleteRun(false);
            }
        }
        catch (OperationCanceledException)
        {
            _vm.AddLog("Lauf abgebrochen.", "WARN");
            _vm.CompleteRun(false);
        }
        catch (Exception ex)
        {
            _vm.AddLog($"Fehler: {ex.Message}", "ERROR");
            _vm.CompleteRun(false);
        }
        finally
        {
            // Process queued watch-mode events
            _watchService.FlushPendingIfNeeded();
        }
    }

    private async void OnRollbackRequested(object? sender, EventArgs e)
    {
        if (!DialogService.Confirm("Letzten Lauf rückgängig machen?", "Rollback bestätigen"))
            return;

        if (string.IsNullOrEmpty(_lastAuditPath) || !File.Exists(_lastAuditPath))
        {
            _vm.AddLog("Keine Audit-Datei gefunden — Rollback nicht möglich.", "WARN");
            return;
        }

        try
        {
            var auditPathCopy = _lastAuditPath;
            var roots = _vm.Roots.ToList();
            var restored = await Task.Run(() => RollbackService.Execute(auditPathCopy, roots));
            _vm.AddLog($"Rollback: {restored.Count} Dateien wiederhergestellt.", "INFO");
            _vm.CanRollback = false;
            _vm.ShowMoveCompleteBanner = false;
        }
        catch (Exception ex)
        {
            _vm.AddLog($"Rollback-Fehler: {ex.Message}", "ERROR");
        }
    }

    // ═══ LOG AUTO-SCROLL ════════════════════════════════════════════════

    // Auto-scroll log ListBox to bottom when new items arrive.
    // Wired once via collection change in OnLoaded would be cleaner,
    // but for simplicity we can also use an attached behavior later.
    // For now, the ListBox virtualizes so scrolling is efficient.

    // ═══ FUNCTIONAL BUTTON HANDLERS ═════════════════════════════════════

    private void OnRefreshReportPreview(object sender, RoutedEventArgs e) => RefreshReportPreview();

    /// <summary>Load the last report into the WebBrowser preview and update error summary.
    /// NOTE: WebBrowser.Navigate() is a direct UI call — WPF WebBrowser has no bindable Source property.
    /// This is an acceptable MVVM exception per ADR-0003.</summary>
    private void RefreshReportPreview()
    {
        if (string.IsNullOrEmpty(_vm.LastReportPath) || !File.Exists(_vm.LastReportPath))
        {
            _vm.ErrorSummaryItems.Clear();
            _vm.ErrorSummaryItems.Add("Kein Report vorhanden.");
            webReportPreview.NavigateToString(
                "<html><body style='background:#1a1a2e;color:#888;font-family:Consolas;padding:16px'>" +
                "<p>Kein Report vorhanden. Erst einen Lauf starten.</p></body></html>");
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(_vm.LastReportPath);
            webReportPreview.Navigate(new Uri(fullPath));
            PopulateErrorSummary();
            _vm.AddLog($"Report-Vorschau geladen: {Path.GetFileName(fullPath)}", "INFO");
        }
        catch (Exception ex)
        {
            _vm.ErrorSummaryItems.Clear();
            _vm.ErrorSummaryItems.Add($"Fehler: {ex.Message}");
            _vm.AddLog($"Report-Vorschau fehlgeschlagen: {ex.Message}", "ERROR");
        }
    }

    /// <summary>Populate the error/warning summary from log entries and run results.</summary>
    private void PopulateErrorSummary()
    {
        _vm.ErrorSummaryItems.Clear();

        // Collect warnings and errors from log
        var issues = _vm.LogEntries
            .Where(e => e.Level is "WARN" or "ERROR")
            .Select(e => $"[{e.Level}] {e.Text}")
            .ToList();

        // Add run result details if available
        if (_lastRunResult is not null)
        {
            if (_lastRunResult.Status == "blocked")
                issues.Insert(0, $"[BLOCKED] Preflight: {_lastRunResult.Preflight?.Reason}");

            if (_lastRunResult.MoveResult is { FailCount: > 0 } mv)
                issues.Insert(0, $"[ERROR] {mv.FailCount} Dateien konnten nicht verschoben werden");

            var junk = _lastCandidates.Count(c => c.Category == "JUNK");
            if (junk > 0)
                issues.Insert(0, $"[WARN] {junk} Junk-Dateien erkannt");

            var unverified = _lastCandidates.Count(c => !c.DatMatch);
            if (unverified > 0 && _lastCandidates.Count > 0)
                issues.Insert(0, $"[INFO] {unverified}/{_lastCandidates.Count} Dateien ohne DAT-Verifizierung");
        }

        if (issues.Count == 0)
        {
            _vm.ErrorSummaryItems.Add("✓ Keine Fehler oder Warnungen.");
            if (_lastRunResult is not null)
                _vm.ErrorSummaryItems.Add($"Report geladen: {_lastRunResult.WinnerCount} Winner, {_lastRunResult.LoserCount} Dupes");
            return;
        }

        foreach (var issue in issues.Take(50))
            _vm.ErrorSummaryItems.Add(issue);
        if (issues.Count > 50)
            _vm.ErrorSummaryItems.Add($"… und {issues.Count - 50} weitere");
    }

    private void OnWatchApply(object sender, RoutedEventArgs e)
    {
        if (_vm.Roots.Count == 0)
        { _vm.AddLog("Keine Root-Ordner für Watch-Mode.", "WARN"); return; }

        _watchService.IsBusyCheck = () => _vm.IsBusy;
        _watchService.RunTriggered -= OnWatchRunTriggered;
        _watchService.RunTriggered += OnWatchRunTriggered;

        var count = _watchService.Start(_vm.Roots);
        if (count == 0)
        {
            // Toggled off
            _vm.AddLog("Watch-Mode deaktiviert.", "INFO");
            DialogService.Info("Watch-Mode wurde deaktiviert.\n\nDateiüberwachung gestoppt.", "Watch-Mode");
        }
        else
        {
            _vm.AddLog($"Watch-Mode aktiviert für {count} Ordner. Änderungen werden überwacht.", "INFO");
            DialogService.Info($"Watch-Mode ist aktiv!\n\nÜberwachte Ordner:\n{string.Join("\n", _vm.Roots)}\n\nBei Dateiänderungen wird automatisch ein DryRun gestartet.\n\nErneut klicken zum Deaktivieren.",
                "Watch-Mode");
        }
    }

    private void OnWatchRunTriggered()
    {
        if (_vm.Roots.Count > 0)
        {
            _vm.AddLog("Watch-Mode: Änderungen erkannt, starte DryRun…", "INFO");
            _vm.DryRun = true;
            _vm.RunCommand.Execute(null);
        }
    }

    // ═══ WORKFLOW & AUTOMATISIERUNG ═════════════════════════════════════

    private void OnCommandPalette(object sender, RoutedEventArgs e)
    {
        var input = DialogService.ShowInputBox("Befehl suchen:", "Command-Palette", "");
        if (string.IsNullOrWhiteSpace(input)) return;
        var results = FeatureService.SearchCommands(input);
        if (results.Count == 0)
        { _vm.AddLog($"Kein Befehl gefunden für: {input}", "WARN"); return; }

        ShowTextDialog("Command-Palette", FeatureService.BuildCommandPaletteReport(input, results));
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
            case "theme": _theme.Toggle(); break;
            case "clear-log": _vm.ClearLogCommand.Execute(null); break;
            case "settings": tabMain.SelectedIndex = 1; break;
            default: _vm.AddLog($"Befehl: {key}", "INFO"); break;
        }
    }

    private void OnSystemTray(object sender, RoutedEventArgs e)
    {
        _trayService ??= new TrayService(this, _vm);
        _trayService.Toggle();
    }

    private void OnMobileWebUI(object sender, RoutedEventArgs e)
    {
        var apiProject = FeatureService.FindApiProjectPath();

        if (apiProject is not null)
        {
            if (DialogService.Confirm("REST API starten und Browser öffnen?\n\nhttp://127.0.0.1:5000", "Mobile Web UI"))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"run --project \"{apiProject}\"",
                        UseShellExecute = false,
                        CreateNoWindow = false,
                    };
                    try { if (_apiProcess is { HasExited: false }) _apiProcess.Kill(entireProcessTree: true); } catch { }
                    try { _apiProcess?.Dispose(); } catch { }
                    _apiProcess = Process.Start(psi);
                    _vm.AddLog("REST API gestartet: http://127.0.0.1:5000", "INFO");
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            try { Process.Start(new ProcessStartInfo("http://127.0.0.1:5000/health") { UseShellExecute = true }); }
                            catch { /* browser launch failed */ }
                        });
                    });
                    return;
                }
                catch (Exception ex)
                {
                    _vm.AddLog($"API-Start fehlgeschlagen: {ex.Message}", "ERROR");
                }
            }
        }
        else
        {
            ShowTextDialog("Mobile Web UI", "Mobile Web UI\n\n  API-Projekt nicht gefunden.\n\n" +
                "  Zum manuellen Start:\n    dotnet run --project src/RomCleanup.Api\n\n" +
                "  Dann im Browser öffnen:\n    http://127.0.0.1:5000");
        }
    }

    // ═══ UI & ERSCHEINUNGSBILD ══════════════════════════════════════════

    private void OnAccessibility(object sender, RoutedEventArgs e)
    {
        var isHC = FeatureService.IsHighContrastActive();
        var currentSize = FontSize;

        var input = DialogService.ShowInputBox(
            $"Barrierefreiheit\n\n" +
            $"High-Contrast: {(isHC ? "AKTIV" : "Inaktiv")}\n" +
            $"Aktuelle Schriftgröße: {currentSize}\n\n" +
            "Neue Schriftgröße eingeben (10-24):",
            "Barrierefreiheit", currentSize.ToString("0"));
        if (string.IsNullOrWhiteSpace(input)) return;

        if (double.TryParse(input, System.Globalization.CultureInfo.InvariantCulture, out var newSize) && newSize >= 10 && newSize <= 24)
        {
            FontSize = newSize;
            _vm.AddLog($"Schriftgröße geändert: {newSize}", "INFO");
        }
        else
        {
            _vm.AddLog($"Ungültige Schriftgröße: {input} (erlaubt: 10-24)", "WARN");
        }
    }

    private void OnThemeEngine(object sender, RoutedEventArgs e)
    {
        var result = DialogService.YesNoCancel(
            $"Aktuelles Theme: {(_theme.IsDark ? "Dark" : "Light")}\n\n" +
            "JA = Dark Theme\nNEIN = Light Theme\nAbbrechen = High-Contrast",
            "Theme-Engine");

        switch (result)
        {
            case MessageBoxResult.Yes:
                _theme.ApplyTheme(true);
                _vm.AddLog("Theme gewechselt: Dark", "INFO");
                break;
            case MessageBoxResult.No:
                _theme.ApplyTheme(false);
                _vm.AddLog("Theme gewechselt: Light", "INFO");
                break;
            case MessageBoxResult.Cancel:
                // Toggle as high-contrast approximation
                _theme.Toggle();
                _vm.AddLog($"Theme gewechselt: {(_theme.IsDark ? "Dark" : "Light")} (High-Contrast nicht separat verfügbar)", "INFO");
                break;
        }
    }

    // ═══ HELPER METHODS ═════════════════════════════════════════════════

    /// <summary>
    /// Delegates to ResultDialog.ShowText — provides Copy/Export buttons and proper theming.
    /// Kept as wrapper to avoid touching 50+ call sites.
    /// </summary>
    private static void ShowTextDialog(string title, string content, UIElement? returnFocusTo = null)
    {
        ResultDialog.ShowText(title, content, Application.Current?.MainWindow);
        returnFocusTo?.Focus();
    }

    private Dictionary<string, string> GetCurrentConfigMap()
    {
        return new Dictionary<string, string>
        {
            ["sortConsole"] = _vm.SortConsole.ToString(),
            ["aliasKeying"] = _vm.AliasKeying.ToString(),
            ["aggressiveJunk"] = _vm.AggressiveJunk.ToString(),
            ["dryRun"] = _vm.DryRun.ToString(),
            ["useDat"] = _vm.UseDat.ToString(),
            ["datRoot"] = _vm.DatRoot ?? "",
            ["datHashType"] = _vm.DatHashType ?? "SHA1",
            ["convertEnabled"] = _vm.ConvertEnabled.ToString(),
            ["trashRoot"] = _vm.TrashRoot ?? "",
            ["auditRoot"] = _vm.AuditRoot ?? "",
            ["toolChdman"] = _vm.ToolChdman ?? "",
            ["toolDolphin"] = _vm.ToolDolphin ?? "",
            ["tool7z"] = _vm.Tool7z ?? "",
            ["locale"] = _vm.Locale ?? "de",
            ["logLevel"] = _vm.LogLevel ?? "Info"
        };
    }
}
