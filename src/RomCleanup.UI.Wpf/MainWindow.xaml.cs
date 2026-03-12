using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DragEventArgs = System.Windows.DragEventArgs;
using RomCleanup.UI.Wpf.Services;
using RomCleanup.UI.Wpf.Models;
using RomCleanup.UI.Wpf.ViewModels;

namespace RomCleanup.UI.Wpf;

public partial class MainWindow : Window, IWindowHost
{
    private readonly MainViewModel _vm;
    private readonly ThemeService _theme;
    private readonly SettingsService _settings = new();
    private readonly DispatcherTimer _settingsTimer;
    private Task? _activeRunTask;

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
        _settingsTimer.Tick += (_, _) => _settings.SaveFrom(_vm, _vm.LastAuditPath);
        _settingsTimer.Start();

        Loaded += OnLoaded;
        Closing += OnClosing;

        // Wire orchestration events
        _vm.RunRequested += OnRunRequested;
        _vm.RollbackRequested += OnRollbackRequested;

        // Drag-drop on root list
        listRoots.DragEnter += OnRootsDragEnter;
        listRoots.Drop += OnRootsDrop;

        // Browse buttons (VM command with parameter)
        foreach (var (name, param, cmd) in new (string, string, ICommand)[]
        {
            ("btnBrowseChdman",  "Chdman",  _vm.BrowseToolPathCommand),
            ("btnBrowseDolphin", "Dolphin", _vm.BrowseToolPathCommand),
            ("btnBrowse7z",      "7z",      _vm.BrowseToolPathCommand),
            ("btnBrowsePsxtract","Psxtract",_vm.BrowseToolPathCommand),
            ("btnBrowseCiso",    "Ciso",    _vm.BrowseToolPathCommand),
            ("btnBrowseDat",     "Dat",     _vm.BrowseFolderPathCommand),
            ("btnBrowseTrash",   "Trash",   _vm.BrowseFolderPathCommand),
            ("btnBrowseAudit",   "Audit",   _vm.BrowseFolderPathCommand),
            ("btnBrowsePs3",     "Ps3",     _vm.BrowseFolderPathCommand),
        })
        {
            if (FindName(name) is System.Windows.Controls.Button btn)
            { btn.CommandParameter = param; btn.Command = cmd; }
        }

        // ── Functional buttons ──────────────────────────────────────────
        btnRefreshReportPreview.Click += OnRefreshReportPreview;

        // ── Profile/Config buttons ──────────────────────────────────────
        btnProfileSave.Click += (_, _) =>
        {
            if (_settings.SaveFrom(_vm, _vm.LastAuditPath))
                _vm.AddLog("Einstellungen gespeichert.", "INFO");
            else
                _vm.AddLog("Einstellungen konnten nicht gespeichert werden.", "ERROR");
        };
        btnProfileLoad.Click += (_, _) => { _settings.LoadInto(_vm); _vm.RefreshStatus(); _vm.AddLog("Einstellungen geladen.", "INFO"); };

        // ── Quick actions + misc ────────────────────────────────────────
        btnQuickPreview.Command = _vm.QuickPreviewCommand;
        btnStartMove.Command = _vm.StartMoveCommand;
        btnRollbackQuick.Command = _vm.RollbackCommand;
        btnWatchApply.Click += OnWatchApply;

        // ── Feature tab buttons ─────────────────────────────────────────
        var featureCommands = new FeatureCommandService(_vm, _settings, new WpfDialogService(), this);
        featureCommands.RegisterCommands();
        AutoWireFeatureButtons();
    }

    /// <summary>Wire ALL feature command buttons (config-tab + feature-tab) using naming convention: btn{Key} → FeatureCommands[Key].</summary>
    private void AutoWireFeatureButtons()
    {
        string[] keys =
        [
            // Config-tab buttons
            "ExportLog", "AutoFindTools",
            "ProfileDelete", "ProfileImport", "ConfigDiff", "ExportUnified", "ConfigImport",
            "HealthScore", "CollectionDiff", "DuplicateInspector", "DuplicateExport",
            "ExportCsv", "ExportExcel", "RollbackUndo", "RollbackRedo",
            "ApplyLocale", "PluginManager", "AutoProfile",
            // Analyse & Berichte
            "ConversionEstimate", "JunkReport", "RomFilter", "DuplicateHeatmap",
            "MissingRom", "CrossRootDupe", "HeaderAnalysis", "Completeness",
            "DryRunCompare", "TrendAnalysis", "EmulatorCompat",
            // Konvertierung & Hashing
            "ConversionPipeline", "NKitConvert", "ConvertQueue", "ConversionVerify",
            "FormatPriority", "ParallelHashing", "GpuHashing",
            // DAT & Verifizierung
            "DatAutoUpdate", "DatDiffViewer", "TosecDat", "CustomDatEditor", "HashDatabaseExport",
            // Sammlungsverwaltung
            "CollectionManager", "CloneListViewer", "CoverScraper", "GenreClassification",
            "PlaytimeTracker", "CollectionSharing", "VirtualFolderPreview",
            // Sicherheit & Integrität
            "IntegrityMonitor", "BackupManager", "Quarantine", "RuleEngine",
            "PatchEngine", "HeaderRepair",
            // Workflow & Automatisierung
            "CommandPalette", "SplitPanelPreview", "FilterBuilder", "SortTemplates",
            "PipelineEngine", "SystemTray", "SchedulerAdvanced", "RulePackSharing", "ArcadeMergeSplit",
            // Export & Integration
            "PdfReport", "LauncherIntegration", "ToolImport",
            // Infrastruktur & Deployment
            "StorageTiering", "NasOptimization", "FtpSource", "CloudSync",
            "PluginMarketplaceFeature", "PortableMode", "DockerContainer", "MobileWebUI",
            "WindowsContextMenu", "HardlinkMode", "MultiInstanceSync",
            // UI & Erscheinungsbild
            "Accessibility", "ThemeEngine",
        ];

        foreach (var key in keys)
        {
            if (FindName($"btn{key}") is System.Windows.Controls.Button btn)
                BindFeatureCommand(btn, key);
        }
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
        _vm.LastAuditPath = _settings.LastAuditPath;
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

            _settings.SaveFrom(_vm, _vm.LastAuditPath);
            CleanupResources();
            _isClosing = true;
            Close(); // Re-trigger close now that task is done
            return;
        }

        _settings.SaveFrom(_vm, _vm.LastAuditPath);
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
            _vm.ApplyRunResult(result);

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
                _vm.PopulateErrorSummary();
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

        if (string.IsNullOrEmpty(_vm.LastAuditPath) || !File.Exists(_vm.LastAuditPath))
        {
            _vm.AddLog("Keine Audit-Datei gefunden — Rollback nicht möglich.", "WARN");
            return;
        }

        try
        {
            var auditPathCopy = _vm.LastAuditPath;
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
            _vm.PopulateErrorSummary();
            _vm.AddLog($"Report-Vorschau geladen: {Path.GetFileName(fullPath)}", "INFO");
        }
        catch (Exception ex)
        {
            _vm.ErrorSummaryItems.Clear();
            _vm.ErrorSummaryItems.Add($"Fehler: {ex.Message}");
            _vm.AddLog($"Report-Vorschau fehlgeschlagen: {ex.Message}", "ERROR");
        }
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

    // ═══ IWindowHost IMPLEMENTATION ═════════════════════════════════════

    double IWindowHost.FontSize
    {
        get => FontSize;
        set => FontSize = value;
    }

    void IWindowHost.SelectTab(int index) => tabMain.SelectedIndex = index;

    void IWindowHost.ShowTextDialog(string title, string content) =>
        ResultDialog.ShowText(title, content, this);

    void IWindowHost.ToggleSystemTray()
    {
        _trayService ??= new TrayService(this, _vm);
        _trayService.Toggle();
    }

    void IWindowHost.StartApiProcess(string projectPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\"",
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
    }

    void IWindowHost.StopApiProcess()
    {
        try { if (_apiProcess is { HasExited: false }) _apiProcess.Kill(entireProcessTree: true); } catch { }
        try { _apiProcess?.Dispose(); } catch { }
        _apiProcess = null;
    }


}
