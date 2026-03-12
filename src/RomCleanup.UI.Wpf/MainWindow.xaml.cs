using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
using RomCleanup.Core.Classification;
using RomCleanup.Infrastructure.Audit;
using RomCleanup.Infrastructure.Conversion;
using RomCleanup.Infrastructure.Dat;
using RomCleanup.Infrastructure.FileSystem;
using RomCleanup.Infrastructure.Hashing;
using RomCleanup.Infrastructure.Orchestration;
using RomCleanup.Infrastructure.Reporting;
using RomCleanup.Infrastructure.Tools;
using RomCleanup.UI.Wpf.Services;
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

    // System tray icon
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private IntPtr _trayIconHandle;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    // Conflict policy (Rename / Skip / Overwrite)
    private string _conflictPolicy = "Rename";

    // Watch-mode watchers and debounce timer
    private readonly List<FileSystemWatcher> _watchers = new();
    private DispatcherTimer? _watchDebounceTimer;
    private bool _watchPendingWhileBusy;
    private DateTime _watchFirstChangeUtc = DateTime.MaxValue;

    // Rollback undo/redo stacks
    private readonly Stack<string> _rollbackUndoStack = new();
    private readonly Stack<string> _rollbackRedoStack = new();

    // Detached API process from Mobile Web UI
    private Process? _apiProcess;
    // Guard against recursive OnClosing calls
    private bool _isClosing;

    public MainWindow()
    {
        _theme = new ThemeService();
        _vm = new MainViewModel(_theme);
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
        btnExportLog.Click += OnExportLog;
        btnRefreshReportPreview.Click += OnRefreshReportPreview;
        btnAutoFindTools.Click += OnAutoFindTools;

        // ── Profile/Config buttons ──────────────────────────────────────
        btnProfileSave.Click += (_, _) =>
        {
            if (_settings.SaveFrom(_vm, _lastAuditPath))
                _vm.AddLog("Einstellungen gespeichert.", "INFO");
            else
                _vm.AddLog("Einstellungen konnten nicht gespeichert werden.", "ERROR");
        };
        btnProfileLoad.Click += (_, _) => { _settings.LoadInto(_vm); _vm.RefreshStatus(); _vm.AddLog("Einstellungen geladen.", "INFO"); };
        btnProfileDelete.Click += OnProfileDelete;
        btnProfileImport.Click += OnProfileImport;
        btnConfigDiff.Click += OnConfigDiff;
        btnExportUnified.Click += OnExportUnified;
        btnConfigImport.Click += OnConfigImport;

        // ── Quick actions ───────────────────────────────────────────────
        btnQuickPreview.Click += (_, _) => { if (_vm.Roots.Count > 0 && !_vm.IsBusy) { _vm.DryRun = true; _vm.RunCommand.Execute(null); } };
        btnStartMove.Click += (_, _) => { if (_vm.Roots.Count > 0 && !_vm.IsBusy) { _vm.DryRun = false; _vm.RunCommand.Execute(null); } };

        // ── Konfiguration tab misc buttons ──────────────────────────────
        btnHealthScore.Click += OnHealthScore;
        btnCollectionDiff.Click += OnCollectionDiff;
        btnDuplicateInspector.Click += OnDuplicateInspector;
        btnDuplicateExport.Click += OnDuplicateExport;
        btnExportCsv.Click += OnExportCsv;
        btnExportExcel.Click += OnExportExcel;
        btnRollbackQuick.Click += (_, _) => { if (_vm.RollbackCommand is RelayCommand rc && rc.CanExecute(null)) rc.Execute(null); else _vm.AddLog("Kein Rollback möglich – keine Audit-Datei.", "WARN"); };
        btnRollbackUndo.Click += OnRollbackUndo;
        btnRollbackRedo.Click += OnRollbackRedo;
        btnWatchApply.Click += OnWatchApply;
        btnApplyLocale.Click += OnApplyLocale;
        btnPluginManager.Click += OnPluginManager;
        btnAutoProfile.Click += OnAutoProfile;
        btnConflictPolicy.Click += OnConflictPolicy;

        // ── Feature tab buttons (not yet implemented) ───────────────────
        WireFeatureButtons();
    }

    /// <summary>Wire all Feature-tab buttons with real handlers.</summary>
    private void WireFeatureButtons()
    {
        // Analyse & Berichte
        btnConversionEstimate.Click += OnConversionEstimate;
        btnJunkReport.Click += OnJunkReport;
        btnRomFilter.Click += OnRomFilter;
        btnDuplicateHeatmap.Click += OnDuplicateHeatmap;
        btnMissingRom.Click += OnMissingRom;
        btnCrossRootDupe.Click += OnCrossRootDupe;
        btnHeaderAnalysis.Click += OnHeaderAnalysis;
        btnCompleteness.Click += OnCompleteness;
        btnDryRunCompare.Click += OnDryRunCompare;
        btnTrendAnalysis.Click += OnTrendAnalysis;
        btnEmulatorCompat.Click += OnEmulatorCompat;

        // Konvertierung & Hashing
        btnConversionPipeline.Click += OnConversionPipeline;
        btnNKitConvert.Click += OnNKitConvert;
        btnConvertQueue.Click += OnConvertQueue;
        btnConversionVerify.Click += OnConversionVerify;
        btnFormatPriority.Click += OnFormatPriority;
        btnParallelHashing.Click += OnParallelHashing;
        btnGpuHashing.Click += OnGpuHashing;

        // DAT & Verifizierung
        btnDatAutoUpdate.Click += OnDatAutoUpdate;
        btnDatDiffViewer.Click += OnDatDiffViewer;
        btnTosecDat.Click += OnTosecDat;
        btnCustomDatEditor.Click += OnCustomDatEditor;
        btnHashDatabaseExport.Click += OnHashDatabaseExport;

        // Sammlungsverwaltung
        btnCollectionManager.Click += OnCollectionManager;
        btnCloneListViewer.Click += OnCloneListViewer;
        btnCoverScraper.Click += OnCoverScraper;
        btnGenreClassification.Click += OnGenreClassification;
        btnPlaytimeTracker.Click += OnPlaytimeTracker;
        btnCollectionSharing.Click += OnCollectionSharing;
        btnVirtualFolderPreview.Click += OnVirtualFolderPreview;

        // Sicherheit & Integrität
        btnIntegrityMonitor.Click += OnIntegrityMonitor;
        btnBackupManager.Click += OnBackupManager;
        btnQuarantine.Click += OnQuarantine;
        btnRuleEngine.Click += OnRuleEngine;
        btnPatchEngine.Click += OnPatchEngine;
        btnHeaderRepair.Click += OnHeaderRepair;

        // Workflow & Automatisierung
        btnCommandPalette.Click += OnCommandPalette;
        btnSplitPanelPreview.Click += OnSplitPanelPreview;
        btnFilterBuilder.Click += OnFilterBuilder;
        btnSortTemplates.Click += OnSortTemplates;
        btnPipelineEngine.Click += OnPipelineEngine;
        btnSystemTray.Click += OnSystemTray;
        btnSchedulerAdvanced.Click += OnSchedulerAdvanced;
        btnRulePackSharing.Click += OnRulePackSharing;
        btnArcadeMergeSplit.Click += OnArcadeMergeSplit;

        // Export & Integration
        btnPdfReport.Click += OnPdfReport;
        btnLauncherIntegration.Click += OnLauncherIntegration;
        btnToolImport.Click += OnToolImport;

        // Infrastruktur & Deployment
        btnStorageTiering.Click += OnStorageTiering;
        btnNasOptimization.Click += OnNasOptimization;
        btnFtpSource.Click += OnFtpSource;
        btnCloudSync.Click += OnCloudSync;
        btnPluginMarketplaceFeature.Click += OnPluginMarketplace;
        btnPortableMode.Click += OnPortableMode;
        btnDockerContainer.Click += OnDockerContainer;
        btnMobileWebUI.Click += OnMobileWebUI;
        btnWindowsContextMenu.Click += OnWindowsContextMenu;
        btnHardlinkMode.Click += OnHardlinkMode;
        btnMultiInstanceSync.Click += OnMultiInstanceSync;

        // UI & Erscheinungsbild
        btnAccessibility.Click += OnAccessibility;
        btnThemeEngine.Click += OnThemeEngine;
    }

    // ═══ LIFECYCLE ══════════════════════════════════════════════════════

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings.LoadInto(_vm);
        _lastAuditPath = _settings.LastAuditPath;
        _vm.RefreshStatus();

        // Auto-scroll log to bottom on new entries
        _vm.LogEntries.CollectionChanged += (_, _) =>
        {
            if (listLog.Items.Count > 0)
                listLog.ScrollIntoView(listLog.Items[^1]);
        };

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
            _isClosing = true;
            Close(); // Re-trigger close now that task is done
            return;
        }

        _settings.SaveFrom(_vm, _lastAuditPath);
        _trayIcon?.Dispose();
        _trayIcon = null;
        if (_trayIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_trayIconHandle);
            _trayIconHandle = IntPtr.Zero;
        }

        // Kill detached API process if running
        try { if (_apiProcess is { HasExited: false }) _apiProcess.Kill(entireProcessTree: true); } catch { }
        _apiProcess = null;

        // Dispose file watchers
        DisposeWatchers();
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
                _vm.IsBusy = false;
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
                // Build infrastructure
                var fs = new FileSystemAdapter();
                var audit = new AuditCsvStore();

                // Data directory resolution
                var dataDir = FeatureService.ResolveDataDirectory() ?? Path.Combine(Directory.GetCurrentDirectory(), "data");

                // ToolRunner
                var toolHashesPath = Path.Combine(dataDir, "tool-hashes.json");
                var toolRunner = new ToolRunnerAdapter(File.Exists(toolHashesPath) ? toolHashesPath : null);

                // ConsoleDetector
                ConsoleDetector? consoleDetector = null;
                var discHeaderDetector = new DiscHeaderDetector();
                var consolesJsonPath = Path.Combine(dataDir, "consoles.json");
                if (File.Exists(consolesJsonPath))
                {
                    var consolesJson = File.ReadAllText(consolesJsonPath);
                    consoleDetector = ConsoleDetector.LoadFromJson(consolesJson, discHeaderDetector);
                }

                // DAT setup
                DatIndex? datIndex = null;
                FileHashService? hashService = null;
                if (_vm.UseDat && !string.IsNullOrWhiteSpace(_vm.DatRoot) && Directory.Exists(_vm.DatRoot))
                {
                    var datRepo = new DatRepositoryAdapter();
                    hashService = new FileHashService();
                    var consoleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var datFile in Directory.GetFiles(_vm.DatRoot, "*.dat"))
                    {
                        var key = Path.GetFileNameWithoutExtension(datFile);
                        consoleMap.TryAdd(key, datFile);
                    }
                    if (consoleMap.Count > 0)
                    {
                        datIndex = datRepo.GetDatIndex(_vm.DatRoot, consoleMap, _vm.DatHashType);
                        Dispatcher.InvokeAsync(() =>
                            _vm.AddLog($"DAT geladen: {datIndex.TotalEntries} Hashes für {datIndex.ConsoleCount} Konsolen", "INFO"));
                    }
                }

                // FormatConverter (optional)
                FormatConverterAdapter? converter = null;
                if (_vm.ConvertEnabled)
                    converter = new FormatConverterAdapter(toolRunner);

                // Audit path
                string? ap = null;
                if (!_vm.DryRun && _vm.Roots.Count > 0)
                {
                    var auditDir = !string.IsNullOrWhiteSpace(_vm.AuditRoot)
                        ? _vm.AuditRoot
                        : GetSiblingDirectory(_vm.Roots[0], "audit-logs");
                    auditDir = Path.GetFullPath(auditDir);
                    ap = Path.Combine(auditDir, $"audit-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                }

                // Report path
                string? rp = null;
                if (_vm.Roots.Count > 0)
                {
                    var reportDir = GetSiblingDirectory(_vm.Roots[0], "reports");
                    reportDir = Path.GetFullPath(reportDir);
                    Directory.CreateDirectory(reportDir);
                    rp = Path.Combine(reportDir, $"report-{DateTime.Now:yyyyMMdd-HHmmss}.html");
                }

                // Build RunOptions
                var selectedExts = GetSelectedExtensions();
                var ro = new RunOptions
                {
                    Roots = _vm.Roots.ToList(),
                    Mode = _vm.DryRun ? "DryRun" : "Move",
                    PreferRegions = _vm.GetPreferredRegions(),
                    Extensions = selectedExts.Length > 0 ? selectedExts : RunOptions.DefaultExtensions,
                    RemoveJunk = true,
                    AggressiveJunk = _vm.AggressiveJunk,
                    SortConsole = _vm.SortConsole,
                    EnableDat = _vm.UseDat,
                    HashType = _vm.DatHashType,
                    ConvertFormat = _vm.ConvertEnabled ? "auto" : null,
                    TrashRoot = string.IsNullOrWhiteSpace(_vm.TrashRoot) ? null : _vm.TrashRoot,
                    AuditPath = ap,
                    ReportPath = rp,
                    ConflictPolicy = _conflictPolicy
                };

                // Build orchestrator with throttled progress (max 1 update/100ms)
                DateTime lastProgressUpdate = DateTime.MinValue;
                var orch = new RunOrchestrator(
                    fs, audit, consoleDetector, hashService, converter, datIndex,
                    onProgress: msg =>
                    {
                        var now = DateTime.UtcNow;
                        if ((now - lastProgressUpdate).TotalMilliseconds < 100) return;
                        lastProgressUpdate = now;
                        Dispatcher.InvokeAsync(() =>
                        {
                            _vm.ProgressText = msg;
                            // Extract phase/file info for performance panel
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

                return (orch, ro, ap, rp);
            }, ct);

            await Task.Run(() =>
            {
                var result = orchestrator.Execute(runOptions, ct);

                Dispatcher.InvokeAsync(() =>
                {
                    // Store results for feature buttons
                    _lastRunResult = result;
                    _lastCandidates = result.AllCandidates;
                    _lastDedupeGroups = result.DedupeGroups;

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

                    // Push to undo stack for rollback support
                    if (!_vm.DryRun && auditPath is not null && File.Exists(auditPath))
                    {
                        _rollbackUndoStack.Push(auditPath);
                        _rollbackRedoStack.Clear();
                    }
                });

                // Generate HTML report on background thread (not UI thread)
                if (reportPath is not null && result.DedupeGroups.Count > 0)
                {
                    try
                    {
                        var entries = result.DedupeGroups.SelectMany(g =>
                        {
                            var list = new List<ReportEntry>();
                            list.Add(new ReportEntry
                            {
                                GameKey = g.Winner.GameKey, Action = "KEEP", Category = g.Winner.Category,
                                Region = g.Winner.Region, FilePath = g.Winner.MainPath,
                                FileName = Path.GetFileName(g.Winner.MainPath),
                                Extension = g.Winner.Extension, SizeBytes = g.Winner.SizeBytes,
                                RegionScore = g.Winner.RegionScore, FormatScore = g.Winner.FormatScore,
                                VersionScore = g.Winner.VersionScore, DatMatch = g.Winner.DatMatch
                            });
                            foreach (var l in g.Losers)
                                list.Add(new ReportEntry
                                {
                                    GameKey = l.GameKey, Action = "MOVE", Category = l.Category,
                                    Region = l.Region, FilePath = l.MainPath,
                                    FileName = Path.GetFileName(l.MainPath),
                                    Extension = l.Extension, SizeBytes = l.SizeBytes,
                                    RegionScore = l.RegionScore, FormatScore = l.FormatScore,
                                    VersionScore = l.VersionScore, DatMatch = l.DatMatch
                                });
                            return list;
                        }).ToList();

                        var summary = new ReportSummary
                        {
                            Mode = runOptions.Mode,
                            TotalFiles = result.TotalFilesScanned,
                            KeepCount = result.WinnerCount,
                            MoveCount = result.LoserCount,
                            JunkCount = result.AllCandidates.Count(c => c.Category == "JUNK"),
                            BiosCount = result.AllCandidates.Count(c => c.Category == "BIOS"),
                            GroupCount = result.GroupCount,
                            Duration = TimeSpan.FromMilliseconds(result.DurationMs)
                        };

                        ReportGenerator.WriteHtmlToFile(
                            reportPath, Path.GetDirectoryName(reportPath) ?? ".", summary, entries);
                        Dispatcher.InvokeAsync(() => _vm.AddLog($"Report: {reportPath}", "INFO"));
                    }
                    catch (Exception rex)
                    {
                        Dispatcher.InvokeAsync(() => _vm.AddLog($"Report-Fehler: {rex.Message}", "WARN"));
                    }
                }
            }, ct);

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
            if (_watchPendingWhileBusy && _watchers.Count > 0)
            {
                _watchPendingWhileBusy = false;
                _watchDebounceTimer?.Stop();
                _watchDebounceTimer?.Start();
            }
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
            var roots = _vm.Roots.ToArray();
            var restored = await Task.Run(() =>
            {
                var audit = new AuditCsvStore();
                return audit.Rollback(auditPathCopy, roots, roots, dryRun: false);
            });
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

    private void OnExportLog(object sender, RoutedEventArgs e)
    {
        var path = DialogService.SaveFile("Log exportieren", "Textdateien (*.txt)|*.txt|Alle (*.*)|*.*", "log-export.txt");
        if (path is null) return;

        try
        {
            var lines = _vm.LogEntries.Select(entry => $"[{entry.Level}] {entry.Text}");
            File.WriteAllLines(path, lines);
            _vm.AddLog($"Log exportiert: {path}", "INFO");
        }
        catch (Exception ex)
        {
            _vm.AddLog($"Log-Export fehlgeschlagen: {ex.Message}", "ERROR");
        }
    }

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

    private async void OnAutoFindTools(object sender, RoutedEventArgs e)
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

    // ═══ PROFILE & CONFIG HANDLERS ═════════════════════════════════════

    private void OnProfileDelete(object sender, RoutedEventArgs e)
    {
        if (!DialogService.Confirm("Gespeicherte Einstellungen wirklich löschen?", "Profil löschen"))
            return;
        var settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RomCleanupRegionDedupe");
        var settingsPath = Path.Combine(settingsDir, "settings.json");
        if (File.Exists(settingsPath))
        {
            File.Delete(settingsPath);
            _vm.AddLog("Profil gelöscht.", "INFO");
        }
        else
            _vm.AddLog("Kein gespeichertes Profil gefunden.", "WARN");
    }

    private void OnProfileImport(object sender, RoutedEventArgs e)
    {
        var path = DialogService.BrowseFile("Profil importieren", "JSON (*.json)|*.json");
        if (path is null) return;
        try
        {
            // Validate JSON before import
            var json = File.ReadAllText(path);
            JsonDocument.Parse(json).Dispose();

            var settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RomCleanupRegionDedupe");
            Directory.CreateDirectory(settingsDir);
            var settingsPath = Path.Combine(settingsDir, "settings.json");

            // Create backup before overwriting
            if (File.Exists(settingsPath))
            {
                var backupPath = settingsPath + $".{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                File.Copy(settingsPath, backupPath, overwrite: false);
            }

            File.Copy(path, settingsPath, overwrite: true);
            _settings.LoadInto(_vm);
            _vm.RefreshStatus();
            _vm.AddLog($"Profil importiert: {Path.GetFileName(path)}", "INFO");
        }
        catch (JsonException)
        { _vm.AddLog("Import fehlgeschlagen: Ungültiges JSON-Format.", "ERROR"); }
        catch (Exception ex)
        { _vm.AddLog($"Import fehlgeschlagen: {ex.Message}", "ERROR"); }
    }

    private void OnConfigDiff(object sender, RoutedEventArgs e)
    {
        var current = GetCurrentConfigMap();
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RomCleanupRegionDedupe", "settings.json");
        if (!File.Exists(settingsPath))
        { DialogService.Info("Keine gespeicherte Konfiguration zum Vergleichen vorhanden.", "Config-Diff"); return; }

        var saved = new Dictionary<string, string>();
        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            FlattenJson(doc.RootElement, "", saved);
        }
        catch { DialogService.Error("Konfigurationsdatei konnte nicht gelesen werden.", "Fehler"); return; }

        var diffs = FeatureService.GetConfigDiff(current, saved);
        if (diffs.Count == 0)
        { DialogService.Info("Keine Unterschiede zwischen aktueller und gespeicherter Konfiguration.", "Config-Diff"); return; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Config-Diff (Aktuell vs. Gespeichert):\n");
        foreach (var d in diffs)
            sb.AppendLine($"  {d.Key}: \"{d.SavedValue}\" → \"{d.CurrentValue}\"");
        ShowTextDialog("Config-Diff", sb.ToString());
    }

    private void OnExportUnified(object sender, RoutedEventArgs e)
    {
        var path = DialogService.SaveFile("Konfiguration exportieren", "JSON (*.json)|*.json", "romcleanup-config.json");
        if (path is null) return;
        try
        {
            var config = GetCurrentConfigMap();
            File.WriteAllText(path, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            _vm.AddLog($"Konfiguration exportiert: {path} — Hinweis: Enthält lokale Pfade (Roots, ToolPaths). Vor dem Teilen prüfen.", "INFO");
        }
        catch (Exception ex)
        { _vm.AddLog($"Export fehlgeschlagen: {ex.Message}", "ERROR"); }
    }

    private void OnConfigImport(object sender, RoutedEventArgs e)
    {
        var path = DialogService.BrowseFile("Konfiguration importieren", "JSON (*.json)|*.json");
        if (path is null) return;
        try
        {
            // Validate JSON before import
            var json = File.ReadAllText(path);
            JsonDocument.Parse(json).Dispose();

            var settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RomCleanupRegionDedupe");
            Directory.CreateDirectory(settingsDir);
            var settingsPath = Path.Combine(settingsDir, "settings.json");

            // Create backup of existing settings before overwriting
            if (File.Exists(settingsPath))
            {
                var backupPath = settingsPath + $".{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                File.Copy(settingsPath, backupPath, overwrite: false);
                _vm.AddLog($"Settings-Backup erstellt: {Path.GetFileName(backupPath)}", "INFO");
            }

            File.Copy(path, settingsPath, overwrite: true);
            _settings.LoadInto(_vm);
            _vm.RefreshStatus();
            _vm.AddLog($"Konfiguration importiert: {Path.GetFileName(path)}", "INFO");
        }
        catch (JsonException)
        { _vm.AddLog("Import fehlgeschlagen: Ungültiges JSON-Format.", "ERROR"); }
        catch (Exception ex)
        { _vm.AddLog($"Import fehlgeschlagen: {ex.Message}", "ERROR"); }
    }

    // ═══ KONFIGURATION TAB HANDLERS ═════════════════════════════════════

    private void OnHealthScore(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten, um den Health-Score zu berechnen.", "WARN"); return; }
        var total = _lastCandidates.Count;
        var dupes = _lastDedupeGroups.Sum(g => g.Losers.Count);
        var junk = _lastCandidates.Count(c => c.Category == "JUNK");
        var verified = _lastCandidates.Count(c => c.DatMatch);
        var score = FeatureService.CalculateHealthScore(total, dupes, junk, verified);
        _vm.HealthScore = $"{score}%";
        ShowTextDialog("Health-Score", $"Sammlungs-Gesundheit: {score}/100\n\n" +
            $"Dateien: {total}\nDuplikate: {dupes} ({100.0 * dupes / total:F1}%)\n" +
            $"Junk: {junk} ({100.0 * junk / total:F1}%)\nVerifiziert: {verified} ({100.0 * verified / total:F1}%)");
    }

    private void OnCollectionDiff(object sender, RoutedEventArgs e)
    {
        var fileA = DialogService.BrowseFile("Ersten Report wählen", "HTML (*.html)|*.html|CSV (*.csv)|*.csv");
        if (fileA is null) return;
        var fileB = DialogService.BrowseFile("Zweiten Report wählen", "HTML (*.html)|*.html|CSV (*.csv)|*.csv");
        if (fileB is null) return;
        _vm.AddLog($"Collection-Diff: {Path.GetFileName(fileA)} vs. {Path.GetFileName(fileB)}", "INFO");

        // If both files are CSV, perform actual diff
        if (fileA.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            fileB.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var setA = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var setB = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in File.ReadLines(fileA).Skip(1))
                {
                    var mainPath = line.Split(';')[0].Trim('"');
                    if (!string.IsNullOrWhiteSpace(mainPath))
                        setA.Add(mainPath);
                }
                foreach (var line in File.ReadLines(fileB).Skip(1))
                {
                    var mainPath = line.Split(';')[0].Trim('"');
                    if (!string.IsNullOrWhiteSpace(mainPath))
                        setB.Add(mainPath);
                }

                var added = setB.Except(setA).ToList();
                var removed = setA.Except(setB).ToList();
                var same = setA.Intersect(setB).Count();

                var sb = new System.Text.StringBuilder();
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
                    foreach (var entry in added.Take(30))
                        sb.AppendLine($"    + {Path.GetFileName(entry)}");
                    if (added.Count > 30)
                        sb.AppendLine($"    … und {added.Count - 30} weitere");
                }

                if (removed.Count > 0)
                {
                    sb.AppendLine($"\n  --- Entfernt (erste {Math.Min(30, removed.Count)}) ---");
                    foreach (var entry in removed.Take(30))
                        sb.AppendLine($"    - {Path.GetFileName(entry)}");
                    if (removed.Count > 30)
                        sb.AppendLine($"    … und {removed.Count - 30} weitere");
                }

                ShowTextDialog("Collection-Diff", sb.ToString());
            }
            catch (Exception ex)
            {
                _vm.AddLog($"Collection-Diff Fehler: {ex.Message}", "ERROR");
            }
        }
        else
        {
            ShowTextDialog("Collection-Diff", $"Vergleich:\n  A: {fileA}\n  B: {fileB}\n\nReport-Dateien werden verglichen – detaillierter Diff erfordert CSV-Format.");
        }
    }

    private void OnDuplicateInspector(object sender, RoutedEventArgs e)
    {
        var sources = FeatureService.GetDuplicateInspector(_lastAuditPath);
        if (sources.Count == 0)
        { _vm.AddLog("Keine Duplikat-Daten vorhanden (erst Move/DryRun starten).", "WARN"); return; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Top Duplikat-Quellverzeichnisse:\n");
        foreach (var s in sources)
            sb.AppendLine($"  {s.Count,4}× │ {s.Directory}");
        ShowTextDialog("Duplikat-Inspektor", sb.ToString());
    }

    private void OnDuplicateExport(object sender, RoutedEventArgs e)
    {
        if (_lastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Duplikat-Daten zum Exportieren.", "WARN"); return; }
        var path = DialogService.SaveFile("Duplikate exportieren", "CSV (*.csv)|*.csv", "duplikate.csv");
        if (path is null) return;
        var losers = _lastDedupeGroups.SelectMany(g => g.Losers).ToList();
        var csv = FeatureService.ExportCollectionCsv(losers);
        File.WriteAllText(path, csv, System.Text.Encoding.UTF8);
        _vm.AddLog($"Duplikate exportiert: {path} ({losers.Count} Einträge)", "INFO");
    }

    private void OnExportCsv(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Keine Daten zum Exportieren.", "WARN"); return; }
        var path = DialogService.SaveFile("CSV Export", "CSV (*.csv)|*.csv", "sammlung.csv");
        if (path is null) return;
        var csv = FeatureService.ExportCollectionCsv(_lastCandidates);
        File.WriteAllText(path, "\uFEFF" + csv, System.Text.Encoding.UTF8); // BOM for Excel
        _vm.AddLog($"CSV exportiert: {path} ({_lastCandidates.Count} Einträge)", "INFO");
    }

    private void OnExportExcel(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Keine Daten zum Exportieren.", "WARN"); return; }
        var path = DialogService.SaveFile("Excel Export", "Excel XML (*.xml)|*.xml", "sammlung.xml");
        if (path is null) return;
        var xml = FeatureService.ExportExcelXml(_lastCandidates);
        File.WriteAllText(path, xml, System.Text.Encoding.UTF8);
        _vm.AddLog($"Excel exportiert: {path}", "INFO");
    }

    private void OnRollbackUndo(object sender, RoutedEventArgs e)
    {
        if (_rollbackUndoStack.Count == 0)
        { _vm.AddLog("Kein Rollback zum Rückgängig machen.", "WARN"); return; }
        var auditPath = _rollbackUndoStack.Pop();
        _rollbackRedoStack.Push(auditPath);
        _vm.AddLog($"Rollback rückgängig gemacht: {Path.GetFileName(auditPath)}", "INFO");
    }

    private void OnRollbackRedo(object sender, RoutedEventArgs e)
    {
        if (_rollbackRedoStack.Count == 0)
        { _vm.AddLog("Kein Redo-Rollback verfügbar.", "WARN"); return; }
        var auditPath = _rollbackRedoStack.Pop();
        _rollbackUndoStack.Push(auditPath);
        _vm.AddLog($"Rollback Redo: {Path.GetFileName(auditPath)}", "INFO");
    }

    private void OnWatchApply(object sender, RoutedEventArgs e)
    {
        if (_vm.Roots.Count == 0)
        { _vm.AddLog("Keine Root-Ordner für Watch-Mode.", "WARN"); return; }

        // If watchers are already active, toggle OFF
        if (_watchers.Count > 0)
        {
            DisposeWatchers();
            _vm.AddLog("Watch-Mode deaktiviert.", "INFO");
            DialogService.Info("Watch-Mode wurde deaktiviert.\n\nDateiüberwachung gestoppt.", "Watch-Mode");
            return;
        }

        // Create watchers for each root
        foreach (var root in _vm.Roots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Changed += OnWatcherFileChanged;
                watcher.Created += OnWatcherFileChanged;
                watcher.Deleted += OnWatcherFileChanged;
                watcher.Renamed += (s, re) => OnWatcherFileChanged(s, re);
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                _vm.AddLog($"Watcher-Fehler für {root}: {ex.Message}", "ERROR");
            }
        }

        // Set up debounce timer (5 seconds)
        _watchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _watchDebounceTimer.Tick += OnWatchDebounceTimerTick;

        _vm.AddLog($"Watch-Mode aktiviert für {_watchers.Count} Ordner. Änderungen werden überwacht.", "INFO");
        DialogService.Info($"Watch-Mode ist aktiv!\n\nÜberwachte Ordner:\n{string.Join("\n", _vm.Roots)}\n\nBei Dateiänderungen wird automatisch ein DryRun gestartet.\n\nErneut klicken zum Deaktivieren.",
            "Watch-Mode");
    }

    private void OnWatcherFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            // Track first change time for max-wait enforcement
            if (_watchFirstChangeUtc == DateTime.MaxValue)
                _watchFirstChangeUtc = DateTime.UtcNow;

            // If max wait (30s) exceeded, fire immediately instead of resetting debounce
            if ((DateTime.UtcNow - _watchFirstChangeUtc).TotalSeconds >= 30)
            {
                _watchDebounceTimer?.Stop();
                _watchFirstChangeUtc = DateTime.MaxValue;
                OnWatchDebounceTimerTick(null, EventArgs.Empty);
                return;
            }

            // Reset debounce timer on every change
            _watchDebounceTimer?.Stop();
            _watchDebounceTimer?.Start();
        });
    }

    private void OnWatchDebounceTimerTick(object? sender, EventArgs e)
    {
        _watchDebounceTimer?.Stop();
        _watchFirstChangeUtc = DateTime.MaxValue;
        if (_vm.IsBusy)
        {
            // Queue for later — run will be triggered when IsBusy clears
            _watchPendingWhileBusy = true;
            return;
        }
        if (_vm.Roots.Count > 0)
        {
            _vm.AddLog("Watch-Mode: Änderungen erkannt, starte DryRun…", "INFO");
            _vm.DryRun = true;
            _vm.RunCommand.Execute(null);
        }
    }

    private void DisposeWatchers()
    {
        _watchDebounceTimer?.Stop();
        _watchDebounceTimer = null;
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
    }

    private void OnApplyLocale(object sender, RoutedEventArgs e)
    {
        var locale = _vm.Locale ?? "de";
        var strings = FeatureService.LoadLocale(locale);
        if (strings.Count == 0)
        { _vm.AddLog($"Sprachdatei '{locale}' nicht gefunden.", "WARN"); return; }
        _vm.AddLog($"Sprache gewechselt: {locale} ({strings.Count} Strings geladen). Hinweis: Aktuell wird nur der Fenstertitel lokalisiert.", "INFO");
        if (strings.TryGetValue("App.Title", out var title))
            Title = title;
    }

    private void OnPluginManager(object sender, RoutedEventArgs e)
    {
        var pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginDir))
        {
            DialogService.Info("Kein Plugin-Verzeichnis gefunden.\n\nErstelle 'plugins/' im Programmverzeichnis, um Plugins zu verwenden.", "Plugin-Manager");
            return;
        }
        var manifests = Directory.GetFiles(pluginDir, "plugin.json", SearchOption.AllDirectories);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Plugin-Manager: {manifests.Length} Plugin(s) gefunden\n");
        foreach (var m in manifests)
        {
            var dir = Path.GetDirectoryName(m)!;
            sb.AppendLine($"  📦 {Path.GetFileName(dir)}");
            sb.AppendLine($"     Pfad: {dir}");
        }
        if (manifests.Length == 0)
            sb.AppendLine("  Keine Plugins installiert.");
        ShowTextDialog("Plugin-Manager", sb.ToString());
    }

    private void OnAutoProfile(object sender, RoutedEventArgs e)
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
        DialogService.Info($"Auto-Profil-Empfehlung:\n\n{profile}\n\n" +
            "Hinweis: Die Erkennung basiert auf Dateierweiterungen der ersten 200 Dateien pro Root-Ordner.", "Auto-Profil");
    }

    private void OnConflictPolicy(object sender, RoutedEventArgs e)
    {
        var result = DialogService.YesNoCancel(
            "Wie sollen Dateinamenkollisionen behandelt werden?\n\n" +
            "JA = Umbenennen (Suffix _1, _2 …)\nNEIN = Überspringen\nAbbrechen = Überschreiben",
            "Conflict-Policy");
        var policy = result switch
        {
            MessageBoxResult.Yes => "Rename",
            MessageBoxResult.No => "Skip",
            _ => "Overwrite"
        };
        _conflictPolicy = policy;
        _vm.AddLog($"Conflict-Policy gesetzt: {policy}", "INFO");
    }

    // ═══ ANALYSE & BERICHTE ═════════════════════════════════════════════

    private void OnConversionEstimate(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten, um Konvertierungs-Schätzungen zu berechnen.", "WARN"); return; }
        var est = FeatureService.GetConversionEstimate(_lastCandidates);
        var sb = new System.Text.StringBuilder();
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
        ShowTextDialog("Konvertierungs-Schätzung", sb.ToString());
    }

    private void OnJunkReport(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var report = FeatureService.BuildJunkReport(_lastCandidates, _vm.AggressiveJunk);
        ShowTextDialog("Junk-Bericht", report);
    }

    private void OnRomFilter(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Keine ROM-Daten geladen.", "WARN"); return; }
        var input = DialogService.ShowInputBox("Suchbegriff eingeben (Name, Region, Konsole, Format):", "ROM-Filter", "");
        if (string.IsNullOrWhiteSpace(input)) return;
        var results = FeatureService.SearchRomCollection(_lastCandidates, input);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"ROM-Filter: \"{input}\" → {results.Count} Treffer\n");
        foreach (var r in results.Take(50))
            sb.AppendLine($"  {Path.GetFileName(r.MainPath),-40} [{r.Region}] {r.Extension} {r.Category}");
        if (results.Count > 50)
            sb.AppendLine($"\n  … und {results.Count - 50} weitere");
        ShowTextDialog("ROM-Filter", sb.ToString());
    }

    private void OnDuplicateHeatmap(object sender, RoutedEventArgs e)
    {
        if (_lastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Deduplizierungs-Daten vorhanden.", "WARN"); return; }
        var heatmap = FeatureService.GetDuplicateHeatmap(_lastDedupeGroups);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Duplikat-Heatmap (nach Konsole)\n");
        foreach (var h in heatmap)
        {
            var bar = new string('█', (int)(h.DuplicatePercent / 5));
            sb.AppendLine($"  {h.Console,-25} {h.Duplicates,4} Dupes ({h.DuplicatePercent:F1}%) {bar}");
        }
        ShowTextDialog("Duplikat-Heatmap", sb.ToString());
    }

    private void OnMissingRom(object sender, RoutedEventArgs e)
    {
        if (!_vm.UseDat || string.IsNullOrWhiteSpace(_vm.DatRoot))
        { _vm.AddLog("DAT muss aktiviert und konfiguriert sein.", "WARN"); return; }

        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen DryRun mit aktiviertem DAT starten.", "WARN"); return; }

        var unverified = _lastCandidates.Where(c => !c.DatMatch).ToList();
        if (unverified.Count == 0)
        {
            DialogService.Info("Alle ROMs haben einen DAT-Match. Keine fehlenden ROMs erkannt.", "Fehlende ROMs");
            return;
        }

        // Group by first subdirectory after root
        var roots = _vm.Roots.Select(r => Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToList();

        string GetSubDir(string filePath)
        {
            var full = Path.GetFullPath(filePath);
            foreach (var root in roots)
            {
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = full[(root.Length + 1)..];
                    var sep = relative.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                    return sep > 0 ? relative[..sep] : "(Root)";
                }
            }
            return Path.GetDirectoryName(filePath) ?? "(Unbekannt)";
        }

        var byDir = unverified.GroupBy(c => GetSubDir(c.MainPath))
            .OrderByDescending(g => g.Count())
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Fehlende ROMs (ohne DAT-Match)");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Gesamt ohne DAT-Match: {unverified.Count} / {_lastCandidates.Count}");
        sb.AppendLine($"\n  Nach Verzeichnis:\n");
        foreach (var g in byDir)
            sb.AppendLine($"    {g.Count(),5}  {g.Key}");

        ShowTextDialog("Fehlende ROMs", sb.ToString());
    }

    private void OnCrossRootDupe(object sender, RoutedEventArgs e)
    {
        if (_vm.Roots.Count < 2)
        { _vm.AddLog("Mindestens 2 Root-Ordner für Cross-Root-Duplikate erforderlich.", "WARN"); return; }

        if (_lastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Deduplizierungs-Daten vorhanden. Erst einen DryRun starten.", "WARN"); return; }

        var roots = _vm.Roots.Select(r => Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToList();

        string? GetRoot(string filePath)
        {
            var full = Path.GetFullPath(filePath);
            return roots.FirstOrDefault(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase));
        }

        var crossRootGroups = new List<DedupeResult>();
        foreach (var g in _lastDedupeGroups)
        {
            var winnerRoot = GetRoot(g.Winner.MainPath);
            var allPaths = new[] { g.Winner }.Concat(g.Losers);
            var distinctRoots = allPaths.Select(c => GetRoot(c.MainPath)).Where(r => r is not null).Distinct().Count();
            if (distinctRoots > 1)
                crossRootGroups.Add(g);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Cross-Root-Duplikate");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Roots: {_vm.Roots.Count}");
        sb.AppendLine($"  Dedupe-Gruppen gesamt: {_lastDedupeGroups.Count}");
        sb.AppendLine($"  Cross-Root-Gruppen: {crossRootGroups.Count}\n");

        foreach (var g in crossRootGroups.Take(30))
        {
            sb.AppendLine($"  [{g.GameKey}]");
            sb.AppendLine($"    Winner: {g.Winner.MainPath}");
            foreach (var l in g.Losers)
                sb.AppendLine($"    Loser:  {l.MainPath}");
        }
        if (crossRootGroups.Count > 30)
            sb.AppendLine($"\n  … und {crossRootGroups.Count - 30} weitere Gruppen");
        if (crossRootGroups.Count == 0)
            sb.AppendLine("  Keine Cross-Root-Duplikate gefunden.");

        ShowTextDialog("Cross-Root-Duplikate", sb.ToString());
    }

    private void OnHeaderAnalysis(object sender, RoutedEventArgs e)
    {
        var path = DialogService.BrowseFile("ROM für Header-Analyse wählen",
            "ROM-Dateien (*.nes;*.sfc;*.gba;*.z64;*.v64;*.n64;*.smc)|*.nes;*.sfc;*.gba;*.z64;*.v64;*.n64;*.smc|Alle (*.*)|*.*");
        if (path is null) return;
        var header = FeatureService.AnalyzeHeader(path);
        if (header is null)
        { _vm.AddLog($"Header konnte nicht gelesen werden: {path}", "ERROR"); return; }
        ShowTextDialog("Header-Analyse", $"Datei: {Path.GetFileName(path)}\n\n" +
            $"Plattform: {header.Platform}\nFormat: {header.Format}\nDetails: {header.Details}");
    }

    private void OnCompleteness(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var verified = _lastCandidates.Count(c => c.DatMatch);
        var total = _lastCandidates.Count;
        var pct = total > 0 ? 100.0 * verified / total : 0;
        ShowTextDialog("Vollständigkeit", $"Sammlungs-Vollständigkeit\n\n" +
            $"Verifizierte Dateien: {verified} / {total} ({pct:F1}%)\n\n" +
            $"Für eine DAT-basierte Vollständigkeitsanalyse\naktiviere DAT-Verifizierung und starte einen DryRun.");
    }

    private void OnDryRunCompare(object sender, RoutedEventArgs e)
    {
        var fileA = DialogService.BrowseFile("Ersten DryRun-Report wählen", "CSV (*.csv)|*.csv|HTML (*.html)|*.html");
        var fileB = fileA is not null ? DialogService.BrowseFile("Zweiten DryRun-Report wählen", "CSV (*.csv)|*.csv|HTML (*.html)|*.html") : null;
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
                {
                    var mainPath = line.Split(';')[0].Trim('"');
                    if (!string.IsNullOrWhiteSpace(mainPath))
                        setA.Add(mainPath);
                }
                foreach (var line in File.ReadLines(fileB).Skip(1))
                {
                    var mainPath = line.Split(';')[0].Trim('"');
                    if (!string.IsNullOrWhiteSpace(mainPath))
                        setB.Add(mainPath);
                }

                var added = setB.Except(setA).ToList();
                var removed = setA.Except(setB).ToList();
                var same = setA.Intersect(setB).Count();

                var sb = new System.Text.StringBuilder();
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
                    foreach (var entry in added.Take(30))
                        sb.AppendLine($"    + {Path.GetFileName(entry)}");
                    if (added.Count > 30)
                        sb.AppendLine($"    … und {added.Count - 30} weitere");
                }

                if (removed.Count > 0)
                {
                    sb.AppendLine($"\n  --- Entfernt (erste {Math.Min(30, removed.Count)}) ---");
                    foreach (var entry in removed.Take(30))
                        sb.AppendLine($"    - {Path.GetFileName(entry)}");
                    if (removed.Count > 30)
                        sb.AppendLine($"    … und {removed.Count - 30} weitere");
                }

                ShowTextDialog("DryRun-Vergleich", sb.ToString());
            }
            catch (Exception ex)
            {
                _vm.AddLog($"DryRun-Vergleich Fehler: {ex.Message}", "ERROR");
            }
        }
        else
        {
            ShowTextDialog("DryRun-Vergleich", $"Vergleich:\n  A: {fileA}\n  B: {fileB}\n\n" +
                "Detaillierter Vergleich erfordert CSV-Reports.\nExportiere Reports als CSV und vergleiche erneut.");
        }
    }

    private void OnTrendAnalysis(object sender, RoutedEventArgs e)
    {
        // Save current snapshot if we have data
        if (_lastCandidates.Count > 0)
        {
            var dupes = _lastDedupeGroups.Sum(g => g.Losers.Count);
            var junk = _lastCandidates.Count(c => c.Category == "JUNK");
            var verified = _lastCandidates.Count(c => c.DatMatch);
            var totalSize = _lastCandidates.Sum(c => c.SizeBytes);
            FeatureService.SaveTrendSnapshot(_lastCandidates.Count, totalSize, verified, dupes, junk);
        }
        var history = FeatureService.LoadTrendHistory();
        var report = FeatureService.FormatTrendReport(history);
        ShowTextDialog("Trend-Analyse", report);
    }

    private void OnEmulatorCompat(object sender, RoutedEventArgs e)
    {
        ShowTextDialog("Emulator-Kompatibilität", FeatureService.FormatEmulatorCompat());
    }

    // ═══ KONVERTIERUNG & HASHING ════════════════════════════════════════

    private void OnConversionPipeline(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var est = FeatureService.GetConversionEstimate(_lastCandidates);
        _vm.AddLog($"Konvertierungs-Pipeline: {est.Details.Count} Dateien, Ersparnis ~{FeatureService.FormatSize(est.SavedBytes)}", "INFO");
        DialogService.Info($"Konvertierungs-Pipeline bereit:\n\n{est.Details.Count} Dateien konvertierbar\n" +
            $"Geschätzte Ersparnis: {FeatureService.FormatSize(est.SavedBytes)}\n\n" +
            "Aktiviere 'Konvertierung' und starte einen Move-Lauf.", "Konvertierungs-Pipeline");
    }

    private void OnNKitConvert(object sender, RoutedEventArgs e)
    {
        var path = DialogService.BrowseFile("NKit-Image wählen", "NKit (*.nkit.iso;*.nkit.gcz;*.nkit)|*.nkit.iso;*.nkit.gcz;*.nkit|Alle (*.*)|*.*");
        if (path is null) return;
        var isNkit = path.Contains(".nkit", StringComparison.OrdinalIgnoreCase);
        _vm.AddLog($"NKit erkannt: {isNkit}, Datei: {Path.GetFileName(path)}", isNkit ? "INFO" : "WARN");

        // Search for nkit tool using ToolRunnerAdapter pattern
        try
        {
            var runner = new ToolRunnerAdapter(null);
            var nkitPath = runner.FindTool("nkit");

            if (nkitPath is not null)
            {
                DialogService.Info(
                    $"NKit-Tool gefunden: {nkitPath}\n\n" +
                    $"Image: {Path.GetFileName(path)}\n" +
                    $"NKit-Format: {(isNkit ? "Ja" : "Nein")}\n\n" +
                    "Konvertierungs-Anleitung:\n" +
                    "  NKit → ISO: NKit.exe recover <Datei>\n" +
                    "  NKit → RVZ: Erst recover, dann dolphintool convert\n\n" +
                    "Empfohlenes Zielformat: RVZ (GameCube/Wii)",
                    "NKit-Konvertierung");
            }
            else
            {
                DialogService.Info(
                    $"NKit-Tool nicht gefunden.\n\n" +
                    $"Image: {Path.GetFileName(path)}\n\n" +
                    "Download: https://vimm.net/vault/nkit\n\n" +
                    "Nach dem Download das Tool in den PATH aufnehmen\n" +
                    "oder im Programmverzeichnis ablegen.",
                    "NKit-Konvertierung");
            }
        }
        catch (Exception ex)
        {
            _vm.AddLog($"NKit-Tool-Suche fehlgeschlagen: {ex.Message}", "WARN");
            DialogService.Info($"NKit-Image: {Path.GetFileName(path)}\n\nKonvertierung nach ISO/RVZ erfordert das Tool 'NKit'.\n" +
                "Download: https://vimm.net/vault/nkit", "NKit-Konvertierung");
        }
    }

    private void OnConvertQueue(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Keine Dateien in der Konvert-Warteschlange.", "WARN"); return; }
        var est = FeatureService.GetConversionEstimate(_lastCandidates);

        var sb = new System.Text.StringBuilder();
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
        {
            sb.AppendLine("  Keine konvertierbaren Dateien gefunden.");
        }

        ShowTextDialog("Konvert-Warteschlange", sb.ToString());
    }

    private void OnConversionVerify(object sender, RoutedEventArgs e)
    {
        var dir = DialogService.BrowseFolder("Konvertierte Dateien prüfen");
        if (dir is null) return;
        var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".chd" or ".rvz" or ".7z").ToList();
        var (passed, failed, missing) = FeatureService.VerifyConversions(files);
        ShowTextDialog("Konvertierung verifizieren", $"Verifizierung: {dir}\n\n" +
            $"Bestanden: {passed}\nFehlgeschlagen: {failed}\nFehlend: {missing}\nGesamt: {files.Count}");
    }

    private void OnFormatPriority(object sender, RoutedEventArgs e)
    {
        ShowTextDialog("Format-Priorität", FeatureService.FormatFormatPriority());
    }

    private void OnParallelHashing(object sender, RoutedEventArgs e)
    {
        var cores = Environment.ProcessorCount;
        var optimal = Math.Max(1, cores - 1);
        var input = DialogService.ShowInputBox(
            $"CPU-Kerne: {cores}\nAktuell: {optimal} Threads\n\n" +
            "Gewünschte Thread-Anzahl eingeben (1-{cores}):",
            "Parallel-Hashing Konfiguration", optimal.ToString());
        if (string.IsNullOrWhiteSpace(input)) return;

        if (int.TryParse(input, out var threads) && threads >= 1 && threads <= cores * 2)
        {
            Environment.SetEnvironmentVariable("ROMCLEANUP_HASH_THREADS", threads.ToString());
            _vm.AddLog($"Parallel-Hashing: {threads} Threads konfiguriert (experimentell – wird in einer zukünftigen Version unterstützt)", "INFO");
            ShowTextDialog("Parallel-Hashing", $"Parallel-Hashing Konfiguration\n\n" +
                $"CPU-Kerne: {cores}\nThreads (neu): {threads}\n\n" +
                "Die Änderung wird beim nächsten Hash-Vorgang wirksam.");
        }
        else
        {
            _vm.AddLog($"Ungültige Thread-Anzahl: {input} (erlaubt: 1-{cores * 2})", "WARN");
        }
    }

    private void OnGpuHashing(object sender, RoutedEventArgs e)
    {
        var openCl = File.Exists(Path.Combine(Environment.SystemDirectory, "OpenCL.dll"));
        var currentSetting = Environment.GetEnvironmentVariable("ROMCLEANUP_GPU_HASHING") ?? "off";
        var isEnabled = currentSetting.Equals("on", StringComparison.OrdinalIgnoreCase);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("GPU-Hashing Konfiguration\n");
        sb.AppendLine($"  OpenCL verfügbar: {(openCl ? "Ja" : "Nein")}");
        sb.AppendLine($"  CPU-Kerne:        {Environment.ProcessorCount}");
        sb.AppendLine($"  Aktueller Status: {(isEnabled ? "AKTIVIERT" : "Deaktiviert")}");

        if (!openCl)
        {
            sb.AppendLine("\n  GPU-Hashing benötigt OpenCL-Treiber.");
            sb.AppendLine("  Installiere aktuelle GPU-Treiber für Unterstützung.");
            ShowTextDialog("GPU-Hashing", sb.ToString());
            return;
        }

        sb.AppendLine("\n  GPU-Hashing kann SHA1/SHA256-Berechnungen");
        sb.AppendLine("  um 5-20x beschleunigen (experimentell).");

        ShowTextDialog("GPU-Hashing", sb.ToString());

        var toggle = isEnabled ? "deaktivieren" : "aktivieren";
        if (DialogService.Confirm($"GPU-Hashing {toggle}?\n\nAktuell: {(isEnabled ? "AN" : "AUS")}", "GPU-Hashing"))
        {
            var newValue = isEnabled ? "off" : "on";
            Environment.SetEnvironmentVariable("ROMCLEANUP_GPU_HASHING", newValue);
            _vm.AddLog($"GPU-Hashing: {(isEnabled ? "deaktiviert" : "aktiviert")} (experimentell – wird in einer zukünftigen Version unterstützt)", "INFO");
        }
    }

    // ═══ DAT & VERIFIZIERUNG ════════════════════════════════════════════

    private void OnDatAutoUpdate(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.DatRoot))
        { _vm.AddLog("DAT-Root nicht konfiguriert.", "WARN"); return; }

        if (!Directory.Exists(_vm.DatRoot))
        { _vm.AddLog($"DAT-Root existiert nicht: {_vm.DatRoot}", "ERROR"); return; }

        _vm.AddLog("DAT Auto-Update: Prüfe lokale DAT-Dateien…", "INFO");

        // Load catalog
        var dataDir = FeatureService.ResolveDataDirectory() ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var catalogPath = Path.Combine(dataDir, "dat-catalog.json");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("DAT Auto-Update");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  DAT-Root: {_vm.DatRoot}");

        // Scan existing DAT files
        var localDats = Directory.GetFiles(_vm.DatRoot, "*.dat", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_vm.DatRoot, "*.xml", SearchOption.AllDirectories))
            .ToList();

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
            if (localDats.Count > 20)
                sb.AppendLine($"    … und {localDats.Count - 20} weitere");
        }

        // Load catalog if available
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
            catch (Exception ex)
            {
                sb.AppendLine($"\n  Katalog-Fehler: {ex.Message}");
            }
        }
        else
        {
            sb.AppendLine($"\n  Katalog nicht gefunden: {catalogPath}");
        }

        var oldDats = localDats.Where(d => (DateTime.Now - File.GetLastWriteTime(d)).TotalDays > 180).ToList();
        if (oldDats.Count > 0)
            sb.AppendLine($"\n  ⚠ {oldDats.Count} DATs sind älter als 6 Monate!");

        ShowTextDialog("DAT Auto-Update", sb.ToString());
        _vm.AddLog($"DAT-Status: {localDats.Count} lokale DATs, {(localDats.Count > 0 ? $"älteste: {(DateTime.Now - File.GetLastWriteTime(localDats.OrderBy(d => File.GetLastWriteTime(d)).First())).TotalDays:0} Tage" : "keine")}", "INFO");
    }

    private void OnDatDiffViewer(object sender, RoutedEventArgs e)
    {
        var fileA = DialogService.BrowseFile("Alte DAT-Datei wählen", "DAT (*.dat;*.xml)|*.dat;*.xml");
        if (fileA is null) return;
        var fileB = DialogService.BrowseFile("Neue DAT-Datei wählen", "DAT (*.dat;*.xml)|*.dat;*.xml");
        if (fileB is null) return;
        _vm.AddLog($"DAT-Diff: {Path.GetFileName(fileA)} vs. {Path.GetFileName(fileB)}", "INFO");

        try
        {
            var diff = FeatureService.CompareDatFiles(fileA, fileB);

            var sb = new System.Text.StringBuilder();
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
                foreach (var name in diff.Added.Take(30))
                    sb.AppendLine($"    + {name}");
                if (diff.Added.Count > 30)
                    sb.AppendLine($"    … und {diff.Added.Count - 30} weitere");
            }

            if (diff.Removed.Count > 0)
            {
                sb.AppendLine($"\n  --- Entfernt (erste {Math.Min(30, diff.Removed.Count)}) ---");
                foreach (var name in diff.Removed.Take(30))
                    sb.AppendLine($"    - {name}");
                if (diff.Removed.Count > 30)
                    sb.AppendLine($"    … und {diff.Removed.Count - 30} weitere");
            }

            ShowTextDialog("DAT-Diff-Viewer", sb.ToString());
        }
        catch (Exception ex)
        {
            _vm.AddLog($"DAT-Diff Fehler: {ex.Message}", "ERROR");
            ShowTextDialog("DAT-Diff-Viewer", $"Fehler beim Parsen der DAT-Dateien:\n\n{ex.Message}\n\n" +
                "Stelle sicher, dass beide Dateien gültiges Logiqx-XML enthalten.");
        }
    }

    private void OnTosecDat(object sender, RoutedEventArgs e)
    {
        var path = DialogService.BrowseFile("TOSEC-DAT wählen", "DAT (*.dat;*.xml)|*.dat;*.xml");
        if (path is null) return;
        _vm.AddLog($"TOSEC-DAT geladen: {Path.GetFileName(path)}", "INFO");

        if (string.IsNullOrWhiteSpace(_vm.DatRoot))
        {
            DialogService.Error("DAT-Root ist nicht konfiguriert. Bitte zuerst den DAT-Root-Ordner setzen.", "TOSEC-DAT");
            return;
        }

        try
        {
            var safeName = Path.GetFileName(path);
            var targetPath = Path.GetFullPath(Path.Combine(_vm.DatRoot, safeName));
            if (!targetPath.StartsWith(Path.GetFullPath(_vm.DatRoot), StringComparison.OrdinalIgnoreCase))
            {
                _vm.AddLog("TOSEC-DAT Import blockiert: Pfad außerhalb des DatRoot.", "ERROR");
                return;
            }
            File.Copy(path, targetPath, overwrite: true);
            _vm.AddLog($"TOSEC-DAT kopiert nach: {targetPath}", "INFO");
            DialogService.Info($"TOSEC-DAT erfolgreich importiert:\n\n  Quelle: {path}\n  Ziel: {targetPath}", "TOSEC-DAT");
        }
        catch (Exception ex)
        {
            _vm.AddLog($"TOSEC-DAT Import fehlgeschlagen: {ex.Message}", "ERROR");
        }
    }

    private void OnCustomDatEditor(object sender, RoutedEventArgs e)
    {
        var gameName = DialogService.ShowInputBox("Spielname eingeben:", "Custom-DAT-Editor", "");
        if (string.IsNullOrWhiteSpace(gameName)) return;

        var romName = DialogService.ShowInputBox("ROM-Dateiname eingeben:", "Custom-DAT-Editor", $"{gameName}.zip");
        if (string.IsNullOrWhiteSpace(romName)) return;

        var crc32 = DialogService.ShowInputBox("CRC32-Hash eingeben (hex):", "Custom-DAT-Editor", "00000000");
        if (string.IsNullOrWhiteSpace(crc32)) return;
        if (!Regex.IsMatch(crc32, @"^[0-9A-Fa-f]{8}$"))
        { _vm.AddLog($"Ungültiger CRC32-Hash: '{crc32}' — erwartet: 8 Hex-Zeichen.", "WARN"); return; }

        var sha1 = DialogService.ShowInputBox("SHA1-Hash eingeben (hex):", "Custom-DAT-Editor", "");
        if (string.IsNullOrWhiteSpace(sha1)) sha1 = "";
        if (sha1.Length > 0 && !Regex.IsMatch(sha1, @"^[0-9A-Fa-f]{40}$"))
        { _vm.AddLog($"Ungültiger SHA1-Hash: '{sha1}' — erwartet: 40 Hex-Zeichen.", "WARN"); return; }

        // Generate Logiqx XML entry
        var xmlEntry = $"  <game name=\"{System.Security.SecurityElement.Escape(gameName)}\">\n" +
                       $"    <description>{System.Security.SecurityElement.Escape(gameName)}</description>\n" +
                       $"    <rom name=\"{System.Security.SecurityElement.Escape(romName)}\" size=\"0\" crc=\"{crc32}\"" +
                       (sha1.Length > 0 ? $" sha1=\"{sha1}\"" : "") + " />\n" +
                       $"  </game>";

        if (!string.IsNullOrWhiteSpace(_vm.DatRoot) && Directory.Exists(_vm.DatRoot))
        {
            try
            {
                var customDatPath = Path.Combine(_vm.DatRoot, "custom.dat");
                if (File.Exists(customDatPath))
                {
                    // Append before closing </datafile> tag — atomic write via temp+move
                    var content = File.ReadAllText(customDatPath);
                    var closeTag = "</datafile>";
                    var idx = content.LastIndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        content = content[..idx] + xmlEntry + "\n" + closeTag;
                    }
                    else
                    {
                        content += "\n" + xmlEntry;
                    }
                    var tempPath = customDatPath + ".tmp";
                    File.WriteAllText(tempPath, content);
                    File.Move(tempPath, customDatPath, overwrite: true);
                }
                else
                {
                    // Create new DAT file
                    var fullXml = "<?xml version=\"1.0\"?>\n" +
                                  "<!DOCTYPE datafile SYSTEM \"http://www.logiqx.com/Dats/datafile.dtd\">\n" +
                                  "<datafile>\n" +
                                  "  <header>\n" +
                                  "    <name>Custom DAT</name>\n" +
                                  "    <description>Benutzerdefinierte DAT-Einträge</description>\n" +
                                  "  </header>\n" +
                                  xmlEntry + "\n" +
                                  "</datafile>";
                    File.WriteAllText(customDatPath, fullXml);
                }
                _vm.AddLog($"Custom-DAT-Eintrag gespeichert: {customDatPath}", "INFO");
            }
            catch (Exception ex)
            {
                _vm.AddLog($"Custom-DAT Fehler: {ex.Message}", "ERROR");
            }
        }
        else
        {
            _vm.AddLog("DatRoot nicht gesetzt – Eintrag wird nur angezeigt.", "WARN");
        }

        ShowTextDialog("Custom-DAT-Editor", $"Generierter Logiqx-XML-Eintrag:\n\n{xmlEntry}");
    }

    private void OnHashDatabaseExport(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Keine Daten für Hash-Export.", "WARN"); return; }
        var path = DialogService.SaveFile("Hash-Datenbank exportieren", "JSON (*.json)|*.json", "hash-database.json");
        if (path is null) return;
        var entries = _lastCandidates.Select(c => new { c.MainPath, c.GameKey, c.Extension, c.Region, c.DatMatch, c.SizeBytes }).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        _vm.AddLog($"Hash-Datenbank exportiert: {path} ({entries.Count} Einträge)", "INFO");
    }

    // ═══ SAMMLUNGSVERWALTUNG ════════════════════════════════════════════

    private void OnCollectionManager(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var byConsole = _lastCandidates.GroupBy(c => FeatureService.ClassifyGenre(c.GameKey))
            .OrderByDescending(g => g.Count()).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Smart Collection Manager\n");
        sb.AppendLine($"Gesamt: {_lastCandidates.Count} ROMs\n");
        foreach (var g in byConsole)
            sb.AppendLine($"  {g.Key,-20} {g.Count(),5} ROMs");
        ShowTextDialog("Smart Collection", sb.ToString());
    }

    private void OnCloneListViewer(object sender, RoutedEventArgs e)
    {
        if (_lastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Gruppen vorhanden.", "WARN"); return; }
        ShowTextDialog("Clone-Liste", FeatureService.BuildCloneTree(_lastDedupeGroups));
    }

    private void OnCoverScraper(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }

        var coverDir = DialogService.BrowseFolder("Cover-Ordner wählen (enthält Cover-Bilder)");
        if (coverDir is null) return;

        var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
        var coverFiles = Directory.GetFiles(coverDir, "*.*", SearchOption.AllDirectories)
            .Where(f => imageExts.Contains(Path.GetExtension(f)))
            .ToList();

        if (coverFiles.Count == 0)
        { DialogService.Info($"Keine Cover-Bilder gefunden in:\n{coverDir}", "Cover-Scraper"); return; }

        // Match covers to games by normalized GameKey for fuzzy matching
        var gameKeys = _lastCandidates
            .Select(c => RomCleanup.Core.GameKeys.GameKeyNormalizer.Normalize(
                Path.GetFileNameWithoutExtension(c.MainPath)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matched = new List<string>();
        var unmatched = new List<string>();
        foreach (var cover in coverFiles)
        {
            var coverName = Path.GetFileNameWithoutExtension(cover);
            var normalizedCover = RomCleanup.Core.GameKeys.GameKeyNormalizer.Normalize(coverName);
            if (gameKeys.Contains(normalizedCover))
                matched.Add(coverName);
            else
                unmatched.Add(coverName);
        }

        var sb = new System.Text.StringBuilder();
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
            foreach (var m in matched.Take(15))
                sb.AppendLine($"    ✓ {m}");
        }
        if (unmatched.Count > 0)
        {
            sb.AppendLine($"\n  --- Nicht zugeordnet (erste {Math.Min(15, unmatched.Count)}) ---");
            foreach (var u in unmatched.Take(15))
                sb.AppendLine($"    ? {u}");
        }

        ShowTextDialog("Cover-Scraper", sb.ToString());
        _vm.AddLog($"Cover-Scan: {matched.Count} zugeordnet, {unmatched.Count} nicht zugeordnet", "INFO");
    }

    private void OnGenreClassification(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var genres = _lastCandidates.GroupBy(c => FeatureService.ClassifyGenre(c.GameKey))
            .OrderByDescending(g => g.Count()).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Genre-Klassifikation\n");
        foreach (var g in genres)
        {
            sb.AppendLine($"  {g.Key,-20} {g.Count(),5} Spiele");
            foreach (var item in g.Take(3))
                sb.AppendLine($"    • {Path.GetFileNameWithoutExtension(item.MainPath)}");
        }
        ShowTextDialog("Genre-Klassifikation", sb.ToString());
    }

    private void OnPlaytimeTracker(object sender, RoutedEventArgs e)
    {
        var dir = DialogService.BrowseFolder("RetroArch-Spielzeit-Ordner wählen (runtime_log)");
        if (dir is null) return;
        var lrtlFiles = Directory.GetFiles(dir, "*.lrtl", SearchOption.AllDirectories);
        if (lrtlFiles.Length == 0)
        { DialogService.Info("Keine .lrtl Spielzeit-Dateien gefunden.", "Spielzeit-Tracker"); return; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Spielzeit-Tracker: {lrtlFiles.Length} Dateien\n");
        sb.AppendLine("Hinweis: Es werden nur RetroArch .lrtl-Dateien unterstützt.\n");
        foreach (var f in lrtlFiles.Take(20))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var lines = File.ReadAllLines(f);
            sb.AppendLine($"  {name}: {lines.Length} Einträge");
        }
        ShowTextDialog("Spielzeit-Tracker", sb.ToString());
    }

    private void OnCollectionSharing(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Keine Daten zum Teilen.", "WARN"); return; }
        var path = DialogService.SaveFile("Sammlung exportieren", "JSON (*.json)|*.json|HTML (*.html)|*.html", "meine-sammlung.json");
        if (path is null) return;
        var entries = _lastCandidates.Where(c => c.Category == "GAME")
            .Select(c => new { Name = Path.GetFileNameWithoutExtension(c.MainPath), c.Region, c.Extension, SizeMB = c.SizeBytes / 1048576.0 }).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        _vm.AddLog($"Sammlung exportiert: {path} ({entries.Count} Spiele, keine Pfade/Hashes)", "INFO");
    }

    private void OnVirtualFolderPreview(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        ShowTextDialog("Virtuelle Ordner", FeatureService.BuildVirtualFolderPreview(_lastCandidates));
    }

    // ═══ SICHERHEIT & INTEGRITÄT ════════════════════════════════════════

    private async void OnIntegrityMonitor(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }

        var createBaseline = DialogService.Confirm("Integritäts-Baseline erstellen oder prüfen?\n\nJA = Neue Baseline erstellen\nNEIN = Gegen Baseline prüfen",
            "Integritäts-Monitor");

        if (createBaseline)
        {
            _vm.AddLog("Erstelle Integritäts-Baseline…", "INFO");
            var paths = _lastCandidates.Select(c => c.MainPath).ToList();
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
                ShowTextDialog("Integritäts-Check", $"Ergebnis:\n\n" +
                    $"Intakt: {check.Intact.Count}\nGeändert: {check.Changed.Count}\nFehlend: {check.Missing.Count}\n" +
                    $"Bit-Rot-Risiko: {(check.BitRotRisk ? "⚠ JA" : "Nein")}");
            }
            catch (Exception ex) { _vm.AddLog($"Integritäts-Fehler: {ex.Message}", "ERROR"); }
        }
    }

    private void OnBackupManager(object sender, RoutedEventArgs e)
    {
        var backupRoot = DialogService.BrowseFolder("Backup-Zielordner wählen");
        if (backupRoot is null) return;

        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Keine Dateien für Backup.", "WARN"); return; }

        var winners = _lastDedupeGroups.Select(g => g.Winner.MainPath).ToList();
        if (!DialogService.Confirm($"{winners.Count} Winner-Dateien sichern nach:\n{backupRoot}", "Backup bestätigen"))
            return;

        try
        {
            var sessionDir = FeatureService.CreateBackup(winners, backupRoot, "winners");
            _vm.AddLog($"Backup erstellt: {sessionDir} ({winners.Count} Dateien)", "INFO");
        }
        catch (Exception ex) { _vm.AddLog($"Backup-Fehler: {ex.Message}", "ERROR"); }
    }

    private void OnQuarantine(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var quarantined = _lastCandidates.Where(c =>
            c.Category == "JUNK" || (!c.DatMatch && c.Region == "UNKNOWN")).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Quarantäne-Kandidaten: {quarantined.Count}\n");
        sb.AppendLine("Kriterien: Junk-Kategorie ODER (kein DAT-Match + Unbekannte Region)\n");
        foreach (var q in quarantined.Take(30))
            sb.AppendLine($"  {Path.GetFileName(q.MainPath),-50} [{q.Category}] {q.Region}");
        if (quarantined.Count > 30)
            sb.AppendLine($"\n  … und {quarantined.Count - 30} weitere");
        ShowTextDialog("Quarantäne", sb.ToString());
    }

    private void OnRuleEngine(object sender, RoutedEventArgs e)
    {
        var dataDir = FeatureService.ResolveDataDirectory() ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var rulesPath = Path.Combine(dataDir, "rules.json");

        if (File.Exists(rulesPath))
        {
            try
            {
                var json = File.ReadAllText(rulesPath);
                using var doc = JsonDocument.Parse(json);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Regel-Engine");
                sb.AppendLine(new string('═', 50));
                sb.AppendLine($"\n  Datei: {rulesPath}\n");

                int idx = 0;
                foreach (var rule in doc.RootElement.EnumerateArray())
                {
                    idx++;
                    var name = rule.TryGetProperty("name", out var np) ? np.GetString() : $"Regel {idx}";
                    var priority = rule.TryGetProperty("priority", out var pp) ? pp.GetInt32() : 0;
                    var action = rule.TryGetProperty("action", out var ap) ? ap.GetString() : "?";

                    sb.AppendLine($"  [{idx}] {name}  (Priorität: {priority}, Aktion: {action})");

                    if (rule.TryGetProperty("conditions", out var conds))
                    {
                        foreach (var cond in conds.EnumerateArray())
                        {
                            var field = cond.TryGetProperty("field", out var fp) ? fp.GetString() : "?";
                            var op = cond.TryGetProperty("operator", out var opp) ? opp.GetString() : "?";
                            var val = cond.TryGetProperty("value", out var vp) ? vp.GetString() : "?";
                            sb.AppendLine($"      Bedingung: {field} {op} {val}");
                        }
                    }
                    sb.AppendLine();
                }

                if (idx == 0)
                    sb.AppendLine("  Keine Regeln definiert.");

                ShowTextDialog("Regel-Engine", sb.ToString());
            }
            catch (Exception ex)
            {
                _vm.AddLog($"Fehler beim Laden der Regeln: {ex.Message}", "ERROR");
            }
        }
        else
        {
            ShowTextDialog("Regel-Engine", "Benutzerdefinierte Regeln\n\n" +
                "Erstelle Regeln mit Bedingungen und Aktionen:\n\n" +
                "Bedingungen: Region, Format, Größe, Name, Konsole, DAT-Status\n" +
                "Operatoren: eq, neq, contains, gt, lt, regex\n" +
                "Aktionen: junk, keep, quarantine\n\n" +
                "Regeln werden nach Priorität (höher = zuerst) ausgewertet.\n" +
                "Die erste passende Regel gewinnt.\n\n" +
                "Keine rules.json gefunden.\n" +
                "Konfiguration in data/rules.json");
        }
    }

    private void OnPatchEngine(object sender, RoutedEventArgs e)
    {
        var patchPath = DialogService.BrowseFile("Patch-Datei wählen", "Patches (*.ips;*.bps;*.ups)|*.ips;*.bps;*.ups|Alle (*.*)|*.*");
        if (patchPath is null) return;
        var format = FeatureService.DetectPatchFormat(patchPath);
        if (format is null)
        { _vm.AddLog($"Unbekanntes Patch-Format: {Path.GetFileName(patchPath)}", "WARN"); return; }
        _vm.AddLog($"Patch erkannt: {format} – {Path.GetFileName(patchPath)}", "INFO");
        DialogService.Info($"Patch-Datei: {Path.GetFileName(patchPath)}\nFormat: {format}\n\n" +
            $"Um den Patch anzuwenden, wähle die Ziel-ROM aus.", "Patch-Engine");
    }

    private void OnHeaderRepair(object sender, RoutedEventArgs e)
    {
        var path = DialogService.BrowseFile("ROM für Header-Reparatur wählen",
            "ROMs (*.nes;*.sfc;*.smc)|*.nes;*.sfc;*.smc|Alle (*.*)|*.*");
        if (path is null) return;
        // Normalize path to prevent traversal
        path = Path.GetFullPath(path);
        var header = FeatureService.AnalyzeHeader(path);
        if (header is null)
        { _vm.AddLog("Header nicht lesbar.", "ERROR"); return; }

        var ext = Path.GetExtension(path).ToLowerInvariant();

        // NES: check for dirty bytes 12-15
        if (header.Platform == "NES")
        {
            try
            {
                // Only read header (16 bytes) for dirty-byte check, not entire file
                var headerBuf = new byte[16];
                using (var hfs = File.OpenRead(path))
                {
                    if (hfs.Read(headerBuf, 0, 16) < 16)
                    {
                        _vm.AddLog("NES-Header: Datei zu klein.", "ERROR");
                        return;
                    }
                }
                bool hasDirtyBytes =
                    (headerBuf[12] != 0 || headerBuf[13] != 0 || headerBuf[14] != 0 || headerBuf[15] != 0);

                if (hasDirtyBytes)
                {
                    var confirm = DialogService.Confirm(
                        $"NES-Header hat unsaubere Bytes (12-15).\n\n" +
                        $"Datei: {Path.GetFileName(path)}\n" +
                        $"Byte 12-15: {headerBuf[12]:X2} {headerBuf[13]:X2} {headerBuf[14]:X2} {headerBuf[15]:X2}\n\n" +
                        "Bytes 12-15 auf 0x00 setzen?\n(Backup wird erstellt)",
                        "Header-Reparatur");
                    if (confirm)
                    {
                        // Create timestamped backup
                        var backupPath = path + $".{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
                        File.Copy(path, backupPath, overwrite: false);
                        _vm.AddLog($"Backup erstellt: {backupPath}", "INFO");

                        // Patch only bytes 12-15 in place
                        using var patchFs = File.OpenWrite(path);
                        patchFs.Seek(12, SeekOrigin.Begin);
                        patchFs.Write(new byte[4], 0, 4);
                        _vm.AddLog($"NES-Header repariert: Bytes 12-15 genullt.", "INFO");
                    }
                }
                else
                {
                    DialogService.Info($"NES-Header ist sauber. Keine Reparatur nötig.\n\n{header.Details}", "Header-Reparatur");
                }
            }
            catch (Exception ex)
            {
                _vm.AddLog($"Header-Reparatur fehlgeschlagen: {ex.Message}", "ERROR");
            }
            return;
        }

        // SNES: check for copier header (file size % 1024 == 512)
        if (header.Platform == "SNES")
        {
            try
            {
                var fileInfo = new FileInfo(path);
                bool hasCopierHeader = fileInfo.Length % 1024 == 512;

                if (hasCopierHeader)
                {
                    var confirm = DialogService.Confirm(
                        $"SNES-ROM hat einen Copier-Header (512 Byte).\n\n" +
                        $"Datei: {Path.GetFileName(path)}\n" +
                        $"Größe: {fileInfo.Length} Bytes ({fileInfo.Length % 1024} Byte Überschuss)\n\n" +
                        "Copier-Header (erste 512 Bytes) entfernen?\n(Backup wird erstellt)",
                        "Header-Reparatur");
                    if (confirm)
                    {
                        // Create timestamped backup
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
                {
                    DialogService.Info($"SNES-ROM hat keinen Copier-Header. Keine Reparatur nötig.\n\n{header.Details}", "Header-Reparatur");
                }
            }
            catch (Exception ex)
            {
                _vm.AddLog($"Header-Reparatur fehlgeschlagen: {ex.Message}", "ERROR");
            }
            return;
        }

        ShowTextDialog("Header-Reparatur", $"Datei: {Path.GetFileName(path)}\n\n" +
            $"Plattform: {header.Platform}\nFormat: {header.Format}\n{header.Details}\n\n" +
            "Automatische Reparatur ist nur für NES und SNES verfügbar.");
    }

    // ═══ WORKFLOW & AUTOMATISIERUNG ═════════════════════════════════════

    private void OnCommandPalette(object sender, RoutedEventArgs e)
    {
        var input = DialogService.ShowInputBox("Befehl suchen:", "Command-Palette", "");
        if (string.IsNullOrWhiteSpace(input)) return;
        var results = FeatureService.SearchCommands(input);
        if (results.Count == 0)
        { _vm.AddLog($"Kein Befehl gefunden für: {input}", "WARN"); return; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Ergebnisse für \"{input}\":\n");
        foreach (var r in results)
            sb.AppendLine($"  {r.shortcut,-12} {r.name}");
        ShowTextDialog("Command-Palette", sb.ToString());

        // Execute first match if exact
        var first = results[0];
        if (first.score == 0) ExecuteCommand(first.key);
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

    private void OnSplitPanelPreview(object sender, RoutedEventArgs e)
    {
        if (_lastDedupeGroups.Count == 0)
        { _vm.AddLog("Keine Daten für Split-Panel.", "WARN"); return; }
        ShowTextDialog("Split-Panel", FeatureService.BuildSplitPanelPreview(_lastDedupeGroups));
    }

    private void OnFilterBuilder(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }

        var input = DialogService.ShowInputBox(
            "Filter-Ausdruck eingeben (Feld=Wert, Feld>Wert, Feld<Wert):\n\n" +
            "Beispiele:\n  region=US\n  category=JUNK\n  sizemb>100\n  extension=.chd\n  datmatch=true",
            "Filter-Builder", "region=US");
        if (string.IsNullOrWhiteSpace(input)) return;

        // Parse "field operator value"
        string field;
        string op;
        string value;

        if (input.Contains(">="))
        {
            var parts = input.Split(">=", 2);
            field = parts[0].Trim().ToLowerInvariant();
            op = ">=";
            value = parts[1].Trim();
        }
        else if (input.Contains("<="))
        {
            var parts = input.Split("<=", 2);
            field = parts[0].Trim().ToLowerInvariant();
            op = "<=";
            value = parts[1].Trim();
        }
        else if (input.Contains('>'))
        {
            var parts = input.Split('>', 2);
            field = parts[0].Trim().ToLowerInvariant();
            op = ">";
            value = parts[1].Trim();
        }
        else if (input.Contains('<'))
        {
            var parts = input.Split('<', 2);
            field = parts[0].Trim().ToLowerInvariant();
            op = "<";
            value = parts[1].Trim();
        }
        else if (input.Contains('='))
        {
            var parts = input.Split('=', 2);
            field = parts[0].Trim().ToLowerInvariant();
            op = "=";
            value = parts[1].Trim();
        }
        else
        {
            _vm.AddLog($"Ungültiger Filter-Ausdruck: {input}", "WARN");
            return;
        }

        var filtered = _lastCandidates.Where(c =>
        {
            string fieldValue = field switch
            {
                "region" => c.Region,
                "category" => c.Category,
                "extension" or "ext" => c.Extension,
                "gamekey" or "game" => c.GameKey,
                "type" => c.Type,
                "datmatch" or "dat" => c.DatMatch.ToString(),
                "sizemb" => (c.SizeBytes / 1048576.0).ToString("F1"),
                "sizebytes" or "size" => c.SizeBytes.ToString(),
                "filename" or "name" => Path.GetFileName(c.MainPath),
                _ => ""
            };

            if (op == "=")
                return fieldValue.Contains(value, StringComparison.OrdinalIgnoreCase);

            if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var numVal) &&
                double.TryParse(fieldValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var fieldNum))
            {
                return op switch
                {
                    ">" => fieldNum > numVal,
                    "<" => fieldNum < numVal,
                    ">=" => fieldNum >= numVal,
                    "<=" => fieldNum <= numVal,
                    _ => false
                };
            }

            return false;
        }).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Filter-Builder: {field} {op} {value}");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Gesamt: {_lastCandidates.Count}");
        sb.AppendLine($"  Gefiltert: {filtered.Count}\n");

        foreach (var r in filtered.Take(50))
            sb.AppendLine($"  {Path.GetFileName(r.MainPath),-45} [{r.Region}] {r.Extension} {r.Category} {FeatureService.FormatSize(r.SizeBytes)}");
        if (filtered.Count > 50)
            sb.AppendLine($"\n  … und {filtered.Count - 50} weitere");

        ShowTextDialog("Filter-Builder", sb.ToString());
    }

    private void OnSortTemplates(object sender, RoutedEventArgs e)
    {
        var templates = FeatureService.GetSortTemplates();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Sortierungs-Vorlagen\n");
        foreach (var (name, pattern) in templates)
            sb.AppendLine($"  {name,-20} → {pattern}");
        sb.AppendLine("\n  Legende: {console} = Konsolenname, {filename} = Dateiname");
        ShowTextDialog("Sort-Templates", sb.ToString());
    }

    private void OnPipelineEngine(object sender, RoutedEventArgs e)
    {
        if (_lastRunResult is not null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Pipeline-Engine — Letzter Lauf");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine($"\n  Status: {_lastRunResult.Status}");
            sb.AppendLine($"  Dauer:  {_lastRunResult.DurationMs / 1000.0:F1}s\n");

            sb.AppendLine($"  {"Phase",-20} {"Status",-15} {"Details"}");
            sb.AppendLine($"  {new string('-', 20)} {new string('-', 15)} {new string('-', 30)}");

            sb.AppendLine($"  {"Scan",-20} {"OK",-15} {_lastRunResult.TotalFilesScanned} Dateien");
            sb.AppendLine($"  {"Dedupe",-20} {"OK",-15} {_lastRunResult.GroupCount} Gruppen, {_lastRunResult.WinnerCount} Winner");

            var junkCount = _lastCandidates.Count(c => c.Category == "JUNK");
            sb.AppendLine($"  {"Junk-Erkennung",-20} {"OK",-15} {junkCount} Junk-Dateien");

            if (_lastRunResult.ConsoleSortResult is { } cs)
                sb.AppendLine($"  {"Konsolen-Sort",-20} {"OK",-15} sortiert");
            else
                sb.AppendLine($"  {"Konsolen-Sort",-20} {"Übersprungen",-15}");

            if (_lastRunResult.ConvertedCount > 0)
                sb.AppendLine($"  {"Konvertierung",-20} {"OK",-15} {_lastRunResult.ConvertedCount} konvertiert");
            else
                sb.AppendLine($"  {"Konvertierung",-20} {"Übersprungen",-15}");

            if (_lastRunResult.MoveResult is { } mv)
                sb.AppendLine($"  {"Move",-20} {(mv.FailCount > 0 ? "WARNUNG" : "OK"),-15} {mv.MoveCount} verschoben, {mv.FailCount} Fehler");
            else
                sb.AppendLine($"  {"Move",-20} {"DryRun",-15} keine Änderungen");

            ShowTextDialog("Pipeline-Engine", sb.ToString());
        }
        else
        {
            ShowTextDialog("Pipeline-Engine", "Pipeline-Engine\n\n" +
                "Bedingte Multi-Step-Pipelines:\n\n" +
                "  1. Scan → Dateien erfassen\n" +
                "  2. Dedupe → Duplikate erkennen\n" +
                "  3. Sort → Nach Konsole sortieren\n" +
                "  4. Convert → Formate konvertieren\n" +
                "  5. Verify → Konvertierung prüfen\n\n" +
                "Jeder Schritt kann übersprungen werden.\n" +
                "DryRun-aware: Kein Schreibzugriff im DryRun-Modus.\n\n" +
                "Starte einen Lauf, um Pipeline-Ergebnisse zu sehen.");
        }
    }

    private void OnSystemTray(object sender, RoutedEventArgs e)
    {
        if (_trayIcon is not null)
        {
            // Already active — toggle: minimize to tray
            WindowState = WindowState.Minimized;
            return;
        }

        // Create tray icon with programmatic bitmap (32x32 blue square with "R")
        var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.FromArgb(40, 100, 210));
            using var font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            g.DrawString("R", font, brush, 2, 2);
        }

        var hicon = bitmap.GetHicon();
        _trayIconHandle = hicon;
        var icon = System.Drawing.Icon.FromHandle(hicon);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Anzeigen", null, (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            });
        });
        menu.Items.Add("DryRun starten", null, (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                _vm.DryRun = true;
                _vm.RunCommand.Execute(null);
            });
        });
        menu.Items.Add("Status", null, (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                var status = _vm.IsBusy ? "Lauf aktiv..." : "Bereit";
                _trayIcon?.ShowBalloonTip(3000, "RomCleanup Status", status, System.Windows.Forms.ToolTipIcon.Info);
            });
        });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) =>
        {
            Dispatcher.InvokeAsync(() => Close());
        });

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "RomCleanup",
            Visible = true,
            ContextMenuStrip = menu
        };

        _trayIcon.DoubleClick += (_, _) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            });
        };

        // Avoid registering the handler multiple times
        StateChanged -= OnWindowStateChanged;
        StateChanged += OnWindowStateChanged;

        _trayIcon.ShowBalloonTip(2000, "RomCleanup", "In den System-Tray minimiert.", System.Windows.Forms.ToolTipIcon.Info);
        WindowState = WindowState.Minimized;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _trayIcon is not null)
        {
            Hide();
            _trayIcon.ShowBalloonTip(2000, "RomCleanup", "Anwendung läuft im Hintergrund.", System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    private void OnSchedulerAdvanced(object sender, RoutedEventArgs e)
    {
        var input = DialogService.ShowInputBox(
            "Cron-Expression eingeben (5 Felder: Min Std Tag Mon Wochentag):\n\n" +
            "Beispiele:\n0 3 * * * → Täglich um 3:00\n0 */6 * * * → Alle 6 Stunden\n0 0 * * 0 → Sonntags um Mitternacht",
            "Cron-Tester", "0 3 * * *");
        if (string.IsNullOrWhiteSpace(input)) return;
        var now = DateTime.Now;
        var matches = FeatureService.TestCronMatch(input, now);
        _vm.AddLog($"Cron-Tester: '{input}' → aktuell {(matches ? "aktiv" : "nicht aktiv")}", "INFO");
        DialogService.Info($"Cron-Expression: {input}\n\nAktuelle Zeit: {now:HH:mm}\nMatch: {(matches ? "JA" : "Nein")}\n\n" +
            "Hinweis: Dies ist ein Cron-Tester. Automatische Ausführung ist nicht implementiert.", "Cron-Tester");
    }

    private void OnRulePackSharing(object sender, RoutedEventArgs e)
    {
        var doExport = DialogService.Confirm(
            "Regel-Pakete\n\nJA = Exportieren (rules.json speichern)\nNEIN = Importieren (rules.json laden)",
            "Regel-Pakete");

        var dataDir = FeatureService.ResolveDataDirectory() ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var rulesPath = Path.Combine(dataDir, "rules.json");

        if (doExport)
        {
            // Export
            if (!File.Exists(rulesPath))
            {
                DialogService.Info("Keine rules.json zum Exportieren gefunden.\n\nErstelle zuerst Regeln in data/rules.json.", "Export");
                return;
            }
            var savePath = DialogService.SaveFile("Regeln exportieren", "JSON (*.json)|*.json", "rules-export.json");
            if (savePath is null) return;
            try
            {
                File.Copy(rulesPath, savePath, overwrite: true);
                _vm.AddLog($"Regeln exportiert: {savePath}", "INFO");
            }
            catch (Exception ex)
            {
                _vm.AddLog($"Export fehlgeschlagen: {ex.Message}", "ERROR");
            }
        }
        else
        {
            // Import
            var importPath = DialogService.BrowseFile("Regel-Paket importieren", "JSON (*.json)|*.json");
            if (importPath is null) return;
            try
            {
                // Validate JSON
                var json = File.ReadAllText(importPath);
                JsonDocument.Parse(json).Dispose();

                Directory.CreateDirectory(dataDir);
                File.Copy(importPath, rulesPath, overwrite: true);
                _vm.AddLog($"Regeln importiert: {Path.GetFileName(importPath)} nach {rulesPath}", "INFO");
            }
            catch (JsonException)
            {
                _vm.AddLog("Import fehlgeschlagen: Ungültiges JSON-Format.", "ERROR");
            }
            catch (Exception ex)
            {
                _vm.AddLog($"Import fehlgeschlagen: {ex.Message}", "ERROR");
            }
        }
    }

    private void OnArcadeMergeSplit(object sender, RoutedEventArgs e)
    {
        var datPath = DialogService.BrowseFile("MAME/FBNEO DAT wählen", "DAT (*.dat;*.xml)|*.dat;*.xml");
        if (datPath is null) return;

        _vm.AddLog($"Arcade Merge/Split: Analysiere {Path.GetFileName(datPath)}…", "INFO");

        try
        {
            var doc = SafeLoadXDocument(datPath);
            var games = doc.Descendants("game").ToList();
            if (games.Count == 0)
                games = doc.Descendants("machine").ToList(); // MAME format

            var parents = games.Where(g => g.Attribute("cloneof") == null).ToList();
            var clones = games.Where(g => g.Attribute("cloneof") != null).ToList();

            // Group clones by parent
            var cloneMap = clones.GroupBy(g => g.Attribute("cloneof")?.Value ?? "")
                .ToDictionary(g => g.Key, g => g.ToList());

            var totalRoms = games.Sum(g => g.Descendants("rom").Count());
            var largestParent = parents.OrderByDescending(p =>
            {
                var name = p.Attribute("name")?.Value ?? "";
                return cloneMap.TryGetValue(name, out var c) ? c.Count : 0;
            }).FirstOrDefault();
            var largestName = largestParent?.Attribute("name")?.Value ?? "?";
            var largestCloneCount = cloneMap.TryGetValue(largestName, out var lc) ? lc.Count : 0;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Arcade Merge/Split Analyse");
            sb.AppendLine(new string('═', 50));
            sb.AppendLine($"\n  DAT: {Path.GetFileName(datPath)}");
            sb.AppendLine($"  Einträge gesamt: {games.Count}");
            sb.AppendLine($"  Parents:         {parents.Count}");
            sb.AppendLine($"  Clones:          {clones.Count}");
            sb.AppendLine($"  ROMs gesamt:     {totalRoms}");
            sb.AppendLine($"\n  Größte Familie: {largestName} ({largestCloneCount} Clones)");
            sb.AppendLine($"\n  Set-Typ-Empfehlung:");
            sb.AppendLine($"    Non-Merged: {parents.Count + clones.Count} Sets (portabel, groß)");
            sb.AppendLine($"    Split:      {parents.Count + clones.Count} Sets (Clones nur diff)");
            sb.AppendLine($"    Merged:     {parents.Count} Sets (Parents enthalten alles)");

            // Show top 10 parents with most clones
            var top10 = parents
                .Select(p => new { Name = p.Attribute("name")?.Value ?? "?",
                    Clones = cloneMap.TryGetValue(p.Attribute("name")?.Value ?? "", out var cc) ? cc.Count : 0 })
                .OrderByDescending(x => x.Clones)
                .Take(10);
            sb.AppendLine($"\n  Top 10 Parents (meiste Clones):");
            foreach (var p in top10)
                sb.AppendLine($"    {p.Name,-30} {p.Clones} Clones");

            ShowTextDialog("Arcade Merge/Split", sb.ToString());
            _vm.AddLog($"Arcade-Analyse: {parents.Count} Parents, {clones.Count} Clones", "INFO");
        }
        catch (Exception ex)
        {
            _vm.AddLog($"Arcade Merge/Split Fehler: {ex.Message}", "ERROR");
            DialogService.Error($"Fehler beim Parsen der DAT:\n\n{ex.Message}", "Arcade Merge/Split");
        }
    }

    // ═══ EXPORT & INTEGRATION ═══════════════════════════════════════════

    private void OnPdfReport(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var path = DialogService.SaveFile("PDF-Report speichern", "HTML (*.html)|*.html", "report.html");
        if (path is null) return;
        // Generate rich HTML that can be printed as PDF
        var summary = new ReportSummary
        {
            Mode = _vm.DryRun ? "DryRun" : "Move",
            TotalFiles = _lastCandidates.Count,
            KeepCount = _lastDedupeGroups.Count,
            MoveCount = _lastDedupeGroups.Sum(g => g.Losers.Count),
            JunkCount = _lastCandidates.Count(c => c.Category == "JUNK"),
            GroupCount = _lastDedupeGroups.Count,
            Duration = TimeSpan.FromMilliseconds(_lastRunResult?.DurationMs ?? 0)
        };
        var entries = _lastCandidates.Select(c => new ReportEntry
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

    private void OnLauncherIntegration(object sender, RoutedEventArgs e)
    {
        if (_lastDedupeGroups.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var path = DialogService.SaveFile("RetroArch Playlist exportieren", "Playlist (*.lpl)|*.lpl", "RomCleanup.lpl");
        if (path is null) return;
        var winners = _lastDedupeGroups.Select(g => g.Winner).ToList();
        var json = FeatureService.ExportRetroArchPlaylist(winners, Path.GetFileNameWithoutExtension(path));
        File.WriteAllText(path, json);
        _vm.AddLog($"Playlist exportiert: {path} ({winners.Count} Einträge)", "INFO");
    }

    private void OnToolImport(object sender, RoutedEventArgs e)
    {
        var path = DialogService.BrowseFile("DAT-Datei importieren (ClrMamePro, RomVault, Logiqx)",
            "DAT (*.dat;*.xml)|*.dat;*.xml|Alle (*.*)|*.*");
        if (path is null) return;
        _vm.AddLog($"Tool-Import: {Path.GetFileName(path)}", "INFO");

        if (string.IsNullOrWhiteSpace(_vm.DatRoot))
        {
            DialogService.Error("DAT-Root ist nicht konfiguriert. Bitte zuerst den DAT-Root-Ordner setzen.", "Tool-Import");
            return;
        }

        try
        {
            var safeName = Path.GetFileName(path);
            var targetPath = Path.GetFullPath(Path.Combine(_vm.DatRoot, safeName));
            if (!targetPath.StartsWith(Path.GetFullPath(_vm.DatRoot), StringComparison.OrdinalIgnoreCase))
            {
                _vm.AddLog("DAT-Import blockiert: Pfad außerhalb des DatRoot.", "ERROR");
                return;
            }
            File.Copy(path, targetPath, overwrite: true);
            _vm.AddLog($"DAT importiert nach: {targetPath}", "INFO");
            DialogService.Info($"DAT erfolgreich importiert:\n\n  Quelle: {path}\n  Ziel: {targetPath}", "Tool-Import");
        }
        catch (Exception ex)
        {
            _vm.AddLog($"DAT-Import fehlgeschlagen: {ex.Message}", "ERROR");
        }
    }

    // ═══ INFRASTRUKTUR & DEPLOYMENT ═════════════════════════════════════

    private void OnStorageTiering(object sender, RoutedEventArgs e)
    {
        if (_lastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        ShowTextDialog("Storage-Tiering", FeatureService.AnalyzeStorageTiers(_lastCandidates));
    }

    private void OnNasOptimization(object sender, RoutedEventArgs e)
    {
        if (_vm.Roots.Count == 0)
        { _vm.AddLog("Keine Roots konfiguriert.", "WARN"); return; }
        ShowTextDialog("NAS-Optimierung", FeatureService.GetNasInfo(_vm.Roots.ToList()));
    }

    private void OnFtpSource(object sender, RoutedEventArgs e)
    {
        var input = DialogService.ShowInputBox(
            "FTP/SFTP-URL eingeben:\n\nFormat: ftp://host/pfad oder sftp://host/pfad",
            "FTP-Quelle", "ftp://");
        if (string.IsNullOrWhiteSpace(input) || input == "ftp://") return;

        var isValid = input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                      input.StartsWith("sftp://", StringComparison.OrdinalIgnoreCase);

        if (!isValid)
        {
            _vm.AddLog($"Ungültige FTP-URL: {input} (muss mit ftp:// oder sftp:// beginnen)", "ERROR");
            return;
        }

        // Warn about unencrypted FTP
        if (input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
        {
            var useSftp = DialogService.Confirm(
                "⚠ FTP überträgt Daten unverschlüsselt.\n" +
                "Zugangsdaten und Dateien können abgefangen werden.\n\n" +
                "Empfehlung: Verwende SFTP (sftp://) stattdessen.\n\n" +
                "Trotzdem mit unverschlüsseltem FTP fortfahren?",
                "Sicherheitshinweis");
            if (!useSftp) return;
        }

        // Parse URL to extract host and path
        try
        {
            var uri = new Uri(input);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("FTP-Quelle konfiguriert\n");
            sb.AppendLine($"  Protokoll: {uri.Scheme.ToUpperInvariant()}");
            sb.AppendLine($"  Host:      {uri.Host}");
            sb.AppendLine($"  Port:      {(uri.Port > 0 ? uri.Port : (uri.Scheme == "sftp" ? 22 : 21))}");
            sb.AppendLine($"  Pfad:      {uri.AbsolutePath}");
            sb.AppendLine("\n  ℹ FTP-Download ist noch nicht implementiert.");
            sb.AppendLine("  Aktuell wird die URL nur registriert und angezeigt.");
            sb.AppendLine("  Geplantes Feature: Dateien vor Verarbeitung lokal cachen.");

            ShowTextDialog("FTP-Quelle", sb.ToString());
            _vm.AddLog($"FTP-Quelle registriert: {uri.Host}{uri.AbsolutePath}", "INFO");
        }
        catch (Exception ex)
        {
            _vm.AddLog($"FTP-URL ungültig: {ex.Message}", "ERROR");
        }
    }

    private void OnCloudSync(object sender, RoutedEventArgs e)
    {
        var oneDrive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive");
        var dropbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Dropbox");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Cloud-Sync Status\n");
        sb.AppendLine($"  OneDrive: {(Directory.Exists(oneDrive) ? "✓ Gefunden" : "✗ Nicht gefunden")}");
        sb.AppendLine($"  Dropbox:  {(Directory.Exists(dropbox) ? "✓ Gefunden" : "✗ Nicht gefunden")}");
        sb.AppendLine("\n  ℹ Nur Statusanzeige – Cloud-Sync ist in Planung.");
        sb.AppendLine("  Geplant: Metadaten-Sync (Einstellungen, Profile).\n  Keine ROM-Dateien werden hochgeladen.");
        ShowTextDialog("Cloud-Sync (Vorschau)", sb.ToString());
    }

    private void OnPluginMarketplace(object sender, RoutedEventArgs e)
    {
        var pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginDir))
        {
            Directory.CreateDirectory(pluginDir);
            _vm.AddLog($"Plugin-Verzeichnis erstellt: {pluginDir}", "INFO");
        }

        var manifests = Directory.GetFiles(pluginDir, "*.json", SearchOption.AllDirectories);
        var dlls = Directory.GetFiles(pluginDir, "*.dll", SearchOption.AllDirectories);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Plugin-Manager (Coming Soon)\n");
        sb.AppendLine("  ℹ Das Plugin-System ist in Planung und noch nicht funktionsfähig.");
        sb.AppendLine($"  Plugin-Verzeichnis: {pluginDir}\n");
        sb.AppendLine($"  Manifeste:   {manifests.Length}");
        sb.AppendLine($"  DLLs:        {dlls.Length}\n");

        if (manifests.Length == 0 && dlls.Length == 0)
        {
            sb.AppendLine("  Keine Plugins installiert.\n");
            sb.AppendLine("  Plugin-Struktur:");
            sb.AppendLine("    plugins/");
            sb.AppendLine("      mein-plugin/");
            sb.AppendLine("        manifest.json");
            sb.AppendLine("        MeinPlugin.dll\n");
            sb.AppendLine("  Manifest-Format:");
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"Mein Plugin\",");
            sb.AppendLine("      \"version\": \"1.0.0\",");
            sb.AppendLine("      \"type\": \"console|format|report\"");
            sb.AppendLine("    }");
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
                catch
                {
                    sb.AppendLine($"  [?] {Path.GetFileName(manifest)} (manifest ungültig)");
                }
            }
            if (dlls.Length > 0)
            {
                sb.AppendLine($"\n  DLLs:");
                foreach (var dll in dlls)
                    sb.AppendLine($"    {Path.GetFileName(dll)}");
            }
        }

        // Offer to open plugin directory
        ShowTextDialog("Plugin-Manager", sb.ToString());
        if (DialogService.Confirm($"Plugin-Verzeichnis im Explorer öffnen?\n\n{pluginDir}", "Plugins"))
            Process.Start(new ProcessStartInfo(pluginDir) { UseShellExecute = true });
    }

    private void OnPortableMode(object sender, RoutedEventArgs e)
    {
        var isPortable = FeatureService.IsPortableMode();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Portable-Modus\n");
        sb.AppendLine($"  Aktueller Modus: {(isPortable ? "PORTABEL" : "Standard (AppData)")}");
        sb.AppendLine($"  Programm-Verzeichnis: {AppContext.BaseDirectory}");
        if (isPortable)
            sb.AppendLine($"  Settings-Ordner: {Path.Combine(AppContext.BaseDirectory, ".romcleanup")}");
        else
        {
            sb.AppendLine($"  Settings-Ordner: {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RomCleanupRegionDedupe")}");
            sb.AppendLine("\n  Tipp: Erstelle '.portable' im Programmverzeichnis für Portable-Modus.");
        }
        ShowTextDialog("Portable-Modus", sb.ToString());
    }

    private void OnDockerContainer(object sender, RoutedEventArgs e)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Docker-Konfiguration\n");
        sb.AppendLine("═══ Dockerfile ═══");
        sb.AppendLine(FeatureService.GenerateDockerfile());
        sb.AppendLine("\n═══ docker-compose.yml ═══");
        sb.AppendLine(FeatureService.GenerateDockerCompose());
        ShowTextDialog("Docker", sb.ToString());

        var savePath = DialogService.SaveFile("Docker-Dateien speichern", "Dockerfile|Dockerfile|YAML (*.yml)|*.yml", "Dockerfile");
        if (savePath is not null)
        {
            var ext = Path.GetExtension(savePath).ToLowerInvariant();
            var content = ext == ".yml" ? FeatureService.GenerateDockerCompose() : FeatureService.GenerateDockerfile();
            File.WriteAllText(savePath, content);
            _vm.AddLog($"Docker-Datei gespeichert: {savePath}", "INFO");
        }
    }

    private void OnMobileWebUI(object sender, RoutedEventArgs e)
    {
        // Find the API project relative to the application
        var baseDir = AppContext.BaseDirectory;
        var apiProject = Path.Combine(baseDir, "..", "..", "..", "..", "..", "src", "RomCleanup.Api", "RomCleanup.Api.csproj");
        if (!File.Exists(apiProject))
            apiProject = Path.Combine(Directory.GetCurrentDirectory(), "src", "RomCleanup.Api", "RomCleanup.Api.csproj");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Mobile Web UI\n");

        if (File.Exists(apiProject))
        {
            sb.AppendLine($"  API-Projekt: {Path.GetFullPath(apiProject)}");
            sb.AppendLine($"  URL: http://127.0.0.1:5000\n");

            if (DialogService.Confirm("REST API starten und Browser öffnen?\n\nhttp://127.0.0.1:5000", "Mobile Web UI"))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"run --project \"{Path.GetFullPath(apiProject)}\"",
                        UseShellExecute = false,
                        CreateNoWindow = false,
                    };
                    // Kill previous API process if still running
                    try { if (_apiProcess is { HasExited: false }) _apiProcess.Kill(entireProcessTree: true); } catch { }
                    _apiProcess = Process.Start(psi);
                    _vm.AddLog("REST API gestartet: http://127.0.0.1:5000", "INFO");

                    // Give the API a moment to start, then open browser
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
            sb.AppendLine("  API-Projekt nicht gefunden.\n");
            sb.AppendLine("  Zum manuellen Start:");
            sb.AppendLine("    dotnet run --project src/RomCleanup.Api\n");
            sb.AppendLine("  Dann im Browser öffnen:");
            sb.AppendLine("    http://127.0.0.1:5000");
        }

        ShowTextDialog("Mobile Web UI", sb.ToString());
    }

    private void OnWindowsContextMenu(object sender, RoutedEventArgs e)
    {
        var regScript = FeatureService.GetContextMenuRegistryScript();
        var path = DialogService.SaveFile("Registry-Skript speichern", "Registry (*.reg)|*.reg", "romcleanup-context-menu.reg");
        if (path is null) return;
        File.WriteAllText(path, regScript);
        _vm.AddLog($"Kontextmenü-Registry exportiert: {path}", "INFO");
        DialogService.Info($"Registry-Skript gespeichert:\n{path}\n\n" +
            "Doppelklicke die .reg-Datei, um das Kontextmenü zu installieren.\n\n" +
            "⚠ Das Skript enthält den absoluten Pfad zur aktuellen EXE-Datei.\n" +
            "Bei Verschiebung der Anwendung muss das Skript neu generiert werden.\n\n" +
            "Einträge:\n• ROM Cleanup – DryRun Scan\n• ROM Cleanup – Move Sort", "Kontextmenü");
    }

    private void OnHardlinkMode(object sender, RoutedEventArgs e)
    {
        if (_lastDedupeGroups.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var estimate = FeatureService.GetHardlinkEstimate(_lastDedupeGroups);
        var firstRoot = _lastDedupeGroups.FirstOrDefault()?.Winner.MainPath;
        var isNtfs = false;
        if (firstRoot is not null)
        {
            try
            {
                var driveRoot = Path.GetPathRoot(firstRoot);
                if (driveRoot is not null)
                {
                    var driveInfo = new DriveInfo(driveRoot);
                    isNtfs = driveInfo.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { /* DriveInfo may fail for network paths */ }
        }
        ShowTextDialog("Hardlink-Modus", $"Hardlink-Modus\n\n{estimate}\n\n" +
            $"NTFS-Unterstützung: {(isNtfs ? "Verfügbar" : "Nicht verfügbar")}\n\n" +
            "Hardlinks teilen den Speicherplatz auf Dateisystemebene.\n" +
            "Beide Pfade zeigen auf dieselben Daten – kein zusätzlicher Speicher.");
    }

    private void OnMultiInstanceSync(object sender, RoutedEventArgs e)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Multi-Instanz-Synchronisation");
        sb.AppendLine(new string('═', 50));

        // Check for lock files in all configured roots
        var locks = new List<(string path, string content)>();
        foreach (var root in _vm.Roots)
        {
            var lockFile = Path.Combine(root, ".romcleanup.lock");
            if (File.Exists(lockFile))
            {
                try
                {
                    var content = File.ReadAllText(lockFile);
                    locks.Add((lockFile, content));
                }
                catch { locks.Add((lockFile, "(nicht lesbar)")); }
            }
        }

        sb.AppendLine($"\n  Konfigurierte Roots: {_vm.Roots.Count}");
        sb.AppendLine($"  Aktive Locks:       {locks.Count}");

        if (locks.Count > 0)
        {
            sb.AppendLine("\n  Gefundene Lock-Dateien:");
            foreach (var (path, content) in locks)
            {
                sb.AppendLine($"    {path}");
                sb.AppendLine($"      {content}");
            }
        }
        else
        {
            sb.AppendLine("\n  Keine aktiven Locks gefunden.");
        }

        // Create sync manifest for this instance
        var currentPid = Environment.ProcessId;
        var hostname = Environment.MachineName;
        sb.AppendLine($"\n  Diese Instanz:");
        sb.AppendLine($"    PID:      {currentPid}");
        sb.AppendLine($"    Hostname: {hostname}");
        sb.AppendLine($"    Status:   {(_vm.IsBusy ? "LÄUFT" : "Bereit")}");

        ShowTextDialog("Multi-Instanz", sb.ToString());

        // Offer to create or clear locks
        if (locks.Count > 0 && DialogService.Confirm($"{locks.Count} Lock-Datei(en) gefunden.\n\nAbgelaufene Locks entfernen?", "Multi-Instanz"))
        {
            var removed = 0;
            foreach (var (path, _) in locks)
            {
                try { File.Delete(path); removed++; }
                catch { /* in use */ }
            }
            _vm.AddLog($"Multi-Instanz: {removed} Lock(s) entfernt", "INFO");
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

    private static void ShowTextDialog(string title, string content, UIElement? returnFocusTo = null)
    {
        var app = Application.Current;
        var window = new Window
        {
            Title = title,
            Width = 700, Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = app.TryFindResource("BrushBackground") as System.Windows.Media.Brush
                ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x2E)),
            Owner = app.MainWindow
        };
        var textBox = new TextBox
        {
            Text = content,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            Background = app.TryFindResource("BrushSurface") as System.Windows.Media.Brush
                ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x16, 0x21, 0x3E)),
            Foreground = app.TryFindResource("BrushTextPrimary") as System.Windows.Media.Brush
                ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEA, 0xEA, 0xEA)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16),
            Margin = new Thickness(8)
        };
        window.Content = textBox;
        window.ShowDialog();
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

    private static void FlattenJson(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    FlattenJson(prop.Value, string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}", result);
                break;
            case JsonValueKind.Array:
                int i = 0;
                foreach (var item in element.EnumerateArray())
                    FlattenJson(item, $"{prefix}[{i++}]", result);
                break;
            default:
                result[prefix] = element.ToString();
                break;
        }
    }

    /// <summary>
    /// Resolve a sibling directory next to the given root path.
    /// UNC-safe: uses Path.GetDirectoryName instead of Path.Combine(.., name)
    /// which breaks on UNC share roots like \\server\share.
    /// Falls back to a subdirectory within root if no parent is available (e.g. drive root C:\).
    /// </summary>
    private static string GetSiblingDirectory(string rootPath, string siblingName)
    {
        var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(fullRoot);

        // If parent is null (drive root like C:\ or UNC root \\server\share),
        // put the directory inside the root instead of escaping above it
        if (string.IsNullOrEmpty(parent))
            return Path.Combine(fullRoot, siblingName);

        return Path.Combine(parent, siblingName);
    }

    /// <summary>
    /// Collect checked extension checkboxes. Returns empty if none are checked (= use defaults).
    /// TASK-031/084/085: Wire extension filter checkboxes to actual run options.
    /// </summary>
    private string[] GetSelectedExtensions()
    {
        var map = new (System.Windows.Controls.CheckBox cb, string ext)[]
        {
            (chkExtChd, ".chd"), (chkExtIso, ".iso"), (chkExtCue, ".cue"), (chkExtGdi, ".gdi"),
            (chkExtImg, ".img"), (chkExtBin, ".bin"), (chkExtCso, ".cso"), (chkExtPbp, ".pbp"),
            (chkExtZip, ".zip"), (chkExt7z, ".7z"),   (chkExtRar, ".rar"),
            (chkExtNes, ".nes"), (chkExtGba, ".gba"), (chkExtNds, ".nds"), (chkExtNsp, ".nsp"),
            (chkExtXci, ".xci"), (chkExtWbfs, ".wbfs"), (chkExtRvz, ".rvz"),
        };

        var selected = map
            .Where(x => x.cb.IsChecked == true)
            .Select(x => x.ext)
            .ToArray();

        return selected;
    }

    /// <summary>
    /// Safely load an XDocument with XXE/DTD processing disabled.
    /// </summary>
    private static XDocument SafeLoadXDocument(string path)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader);
    }
}
