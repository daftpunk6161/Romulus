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

    // Detached API process from Mobile Web UI
    private Process? _apiProcess;
    // Guard against recursive OnClosing calls
    private bool _isClosing;
    // Named handler so we can unsubscribe in cleanup
    private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _logScrollHandler;

    public MainWindow()
    {
        _theme = new ThemeService();
        _vm = new MainViewModel(_theme, new WpfDialogService(), _settings);
        DataContext = _vm;

        InitializeComponent();

        // Periodic settings save every 5 minutes (P3-BUG-051 / UX-07)
        _settingsTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _settingsTimer.Tick += (_, _) => _vm.SaveSettings();
        _settingsTimer.Start();

        Loaded += OnLoaded;
        Closing += OnClosing;

        // Wire orchestration events
        _vm.RunRequested += OnRunRequested;

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
        btnProfileSave.Command = _vm.SaveSettingsCommand;
        btnProfileLoad.Command = _vm.LoadSettingsCommand;

        // ── Quick actions + misc ────────────────────────────────────────
        btnStartMove.Command = _vm.StartMoveCommand;

        // ── Feature tab buttons ─────────────────────────────────────────
        var featureCommands = new FeatureCommandService(_vm, _settings, new WpfDialogService(), this);
        featureCommands.RegisterCommands();
        _vm.WireToolItemCommands();
        AutoWireFeatureButtons();
    }

    /// <summary>Wire remaining named buttons (Einstellungen tab) using naming convention: btn{Key} → FeatureCommands[Key].</summary>
    private void AutoWireFeatureButtons()
    {
        string[] keys =
        [
            // Einstellungen tab named buttons
            "ExportLog", "AutoFindTools",
            "ProfileDelete", "ProfileImport", "ConfigDiff", "ExportUnified", "ConfigImport",
            "ApplyLocale",
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
        _vm.LoadInitialSettings();

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

            _vm.SaveSettings();
            CleanupResources();
            _isClosing = true;
            Close(); // Re-trigger close now that task is done
            return;
        }

        _vm.SaveSettings();
        CleanupResources();
    }

    /// <summary>Release all resources — called from both OnClosing paths (normal + busy-cancel).</summary>
    private void CleanupResources()
    {
        // Stop periodic save timer
        _settingsTimer.Stop();

        // Unsubscribe VM events to prevent leaks
        _vm.RunRequested -= OnRunRequested;
        if (_logScrollHandler is not null)
            _vm.LogEntries.CollectionChanged -= _logScrollHandler;

        // System tray
        _trayService?.Dispose();
        _trayService = null;

        // Kill detached API process if running
        try { if (_apiProcess is { HasExited: false }) _apiProcess.Kill(entireProcessTree: true); } catch { }
        try { _apiProcess?.Dispose(); } catch { }
        _apiProcess = null;

        // Dispose file watchers (owned by VM)
        _vm.CleanupWatchers();
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
        _activeRunTask = ExecuteAndRefreshAsync();
        try { await _activeRunTask; }
        finally { _activeRunTask = null; }
    }

    private async Task ExecuteAndRefreshAsync()
    {
        await _vm.ExecuteRunAsync();
        if (_vm.CurrentRunState is RunState.Completed or RunState.CompletedDryRun)
            RefreshReportPreview();
    }

    // ═══ FUNCTIONAL BUTTON HANDLERS ═════════════════════════════════════

    private void OnRefreshReportPreview(object sender, RoutedEventArgs e) => RefreshReportPreview();

    /// <summary>Load the last report into the WebView2 preview and update error summary.
    /// NOTE: WebView2 navigation is a direct UI call — no bindable Source property.
    /// This is an acceptable MVVM exception per ADR-0003.</summary>
    private async void RefreshReportPreview()
    {
        if (string.IsNullOrEmpty(_vm.LastReportPath) || !File.Exists(_vm.LastReportPath))
        {
            _vm.ErrorSummaryItems.Clear();
            _vm.ErrorSummaryItems.Add("Kein Report vorhanden.");
            await EnsureWebView2Initialized();
            webReportPreview.NavigateToString(
                "<html><body style='background:#1a1a2e;color:#888;font-family:Consolas;padding:16px'>" +
                "<p>Kein Report vorhanden. Erst einen Lauf starten.</p></body></html>");
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(_vm.LastReportPath);
            await EnsureWebView2Initialized();
            webReportPreview.Source = new Uri(fullPath);
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

    /// <summary>Ensure the WebView2 runtime is initialized before first navigation.
    /// CoreWebView2 is null until EnsureCoreWebView2Async completes.</summary>
    private async Task EnsureWebView2Initialized()
    {
        if (webReportPreview.CoreWebView2 is not null) return;
        try
        {
            await webReportPreview.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            _vm.AddLog($"WebView2-Runtime nicht verf\u00fcgbar: {ex.Message}", "ERROR");
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
