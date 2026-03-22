using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Orchestration;
using RomCleanup.UI.Wpf.Models;
using RomCleanup.UI.Wpf.Services;
using ConflictPolicy = RomCleanup.UI.Wpf.Models.ConflictPolicy;
using RunState = RomCleanup.UI.Wpf.Models.RunState;

namespace RomCleanup.UI.Wpf.ViewModels;

/// <summary>
/// Main ViewModel — clean MVVM with Commands and computed status indicators.
/// No direct UI element access. All data flows through bindings.
/// Partial class: core + Settings + Filters + RunPipeline.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IThemeService _theme;
    private readonly IDialogService _dialog;
    private readonly ISettingsService _settings;
    private readonly IRunService _runService;
    private readonly ILocalizationService _loc;
    private readonly SynchronizationContext? _syncContext;
    private readonly WatchService _watchService = new();
    private CancellationTokenSource? _cts;
    // V2-THR-H02: Lock for consistent CTS access between OnCancel and CreateRunCancellation
    private readonly object _ctsLock = new();

    // ═══ CHILD VIEWMODELS (GUI-021: shell ViewModel pattern) ════════════
    /// <summary>Configuration, paths, regions, filters, validation.</summary>
    public SetupViewModel Setup { get; }
    /// <summary>Tool catalog, quick access, search, categories.</summary>
    public ToolsViewModel Tools { get; }
    /// <summary>Run pipeline state, progress, dashboard, rollback.</summary>
    public RunViewModel Run { get; }
    /// <summary>MissionControl dashboard: Sources, Intent, Health, LastRun.</summary>
    public MissionControlViewModel MissionControl { get; }
    /// <summary>Library area: Analysis state, sub-tabs, search/filter.</summary>
    public LibraryViewModel Library { get; }
    /// <summary>Config area: Profile state, validation summary, sub-tabs.</summary>
    public ConfigViewModel Config { get; }
    /// <summary>Context-dependent Inspector for the Context Wing.</summary>
    public InspectorViewModel Inspector { get; }
    /// <summary>System area: Activity Log, Appearance, About/Health.</summary>
    public SystemViewModel SystemArea { get; }
    /// <summary>Command Palette overlay (Ctrl+K): fuzzy search + execute.</summary>
    public CommandPaletteViewModel CommandPalette { get; }

    public MainViewModel() : this(new ThemeService(), new WpfDialogService()) { }

    public MainViewModel(IThemeService theme, IDialogService dialog, ISettingsService? settings = null, IRunService? runService = null, ILocalizationService? loc = null)
    {
        _theme = theme;
        _dialog = dialog;
        _settings = settings ?? new SettingsService();
        _runService = runService ?? new RunService();
        _loc = loc ?? new LocalizationService();
        _syncContext = SynchronizationContext.Current;

        // ── Child ViewModels (GUI-021) ────────────────────────────────
        Setup = new SetupViewModel(_theme, _dialog, _settings, _loc);
        Tools = new ToolsViewModel(_loc);
        Run = new RunViewModel(_loc);
        Inspector = new InspectorViewModel(_loc);
        MissionControl = new MissionControlViewModel(_loc);
        Library = new LibraryViewModel(_loc);
        Config = new ConfigViewModel(_loc);
        SystemArea = new SystemViewModel(_loc);
        CommandPalette = new CommandPaletteViewModel(_loc);

        // Wire child VM events
        Setup.StatusRefreshRequested += () => RefreshStatus();
        Run.CommandRequeryRequested += DeferCommandRequery;
        Run.RunRequested += (_, _) => OnRun();

        // Wire collection changes to status refresh
        Roots.CollectionChanged += OnRootsChanged;
        PropertyChanged += OnConfigurationPropertyChanged;

        // ── Commands (CommunityToolkit.Mvvm.Input) ────────────────────
        RunCommand = new RelayCommand(OnRun, () => CanStartCurrentRun);
        CancelCommand = new RelayCommand(OnCancel, () => IsBusy);
        RollbackCommand = new AsyncRelayCommand(OnRollbackAsync, () => !IsBusy && CanRollback);
        AddRootCommand = new RelayCommand(OnAddRoot, () => !IsBusy);
        RemoveRootCommand = new RelayCommand(OnRemoveRoot, () => !IsBusy && SelectedRoot is not null);
        OpenReportCommand = new RelayCommand(OnOpenReport, () => !string.IsNullOrEmpty(LastReportPath));
        ClearLogCommand = new RelayCommand(() => LogEntries.Clear());
        ThemeToggleCommand = new RelayCommand(OnThemeToggle);
        GameKeyPreviewCommand = new RelayCommand(OnGameKeyPreview, () => !string.IsNullOrWhiteSpace(GameKeyPreviewInput));

        // Presets
        PresetSafeDryRunCommand = new RelayCommand(OnPresetSafeDryRun);
        PresetFullSortCommand = new RelayCommand(OnPresetFullSort);
        PresetConvertCommand = new RelayCommand(OnPresetConvert);

        // Browse commands (parameter = property name to set)
        BrowseToolPathCommand = new RelayCommand<string>(OnBrowseToolPath);
        BrowseFolderPathCommand = new RelayCommand<string>(OnBrowseFolderPath);

        // Settings commands
        SaveSettingsCommand = new RelayCommand(OnSaveSettings);
        LoadSettingsCommand = new RelayCommand(OnLoadSettings);
        WatchApplyCommand = new RelayCommand(ToggleWatchMode, () => !IsBusy);

        // Quick workflow commands
        QuickPreviewCommand = new RelayCommand(
            () => { DryRun = true; RunCommand.Execute(null); },
            () => Roots.Count > 0 && !IsBusy && !HasBlockingValidationErrors);
        ConvertOnlyCommand = new RelayCommand(
            () => { ConvertOnly = true; DryRun = false; RunCommand.Execute(null); },
            () => Roots.Count > 0 && !IsBusy && !HasBlockingValidationErrors);
        RequestStartMoveCommand = new RelayCommand(
            () => ShowMoveInlineConfirm = true,
            () => ShowStartMoveButton && !ShowMoveInlineConfirm);
        CancelStartMoveCommand = new RelayCommand(
            () => ShowMoveInlineConfirm = false,
            () => ShowMoveInlineConfirm);
        StartMoveCommand = new RelayCommand(
            () =>
            {
                ShowMoveInlineConfirm = false;
                if (HasBlockingValidationErrors)
                {
                    var blockingValidationMessage = GetBlockingValidationMessage();
                    AddLog(blockingValidationMessage, "WARN");
                    _dialog.Info(blockingValidationMessage, "Start gesperrt");
                    return;
                }

                if (!CanStartMoveWithCurrentPreview)
                {
                    AddLog(MoveApplyGateText, "WARN");
                    _dialog.Info(MoveApplyGateText, "Move gesperrt");
                    return;
                }

                DryRun = false;
                RunCommand.Execute(null);
            },
            () => CanStartMoveWithCurrentPreview && !HasBlockingValidationErrors);

        // GUI-063: Navigation history commands
        NavBackCommand = new RelayCommand(NavGoBack, () => CanNavBack);
        NavForwardCommand = new RelayCommand(NavGoForward, () => CanNavForward);
        GoToSetupCommand = new RelayCommand(() => NavigateTo("Config"));
        GoToAnalyseCommand = new RelayCommand(() => NavigateTo("Library"));
        GoToConfigCommand = new RelayCommand(() => NavigateTo("Config"));
        GoToLibraryCommand = new RelayCommand(() => NavigateTo("Library"));
        GoToToolsCommand = new RelayCommand(() => NavigateTo("Tools"));
        ToggleContextWingCommand = new RelayCommand(() => ShowContextWing = !ShowContextWing);

        // GUI-081: First-Run Wizard commands
        WizardNextCommand = new RelayCommand(WizardNext);
        WizardBackCommand = new RelayCommand(WizardBack, () => WizardStep > 0);
        WizardSkipCommand = new RelayCommand(WizardSkip);

        // GUI-101: Shortcut cheatsheet toggle
        ToggleShortcutSheetCommand = new RelayCommand(() => ShowShortcutSheet = !ShowShortcutSheet);

        // GUI-Phase4 4.1: Command Palette toggle (Ctrl+K)
        ToggleCommandPaletteCommand = new RelayCommand(() => CommandPalette.IsOpen = !CommandPalette.IsOpen);

        // GUI-Phase4 4.4: Detail Drawer toggle (Ctrl+J)
        ToggleDetailDrawerCommand = new RelayCommand(() => ShowDetailDrawer = !ShowDetailDrawer);

        // Extension filter collection (UX-004)
        InitExtensionFilters();
        WirePreviewGateObservers();

        // Console filter collection (Runde 7: replaces 30 x:Name checkboxes)
        InitConsoleFilters();

        // Tool items collection (RD-004: Werkzeuge tab)
        InitToolItems();

        // Feature commands (TASK-111: replaces Click event handlers)
        InitFeatureCommands();

        // Wire watch-mode auto-run trigger
        _watchService.RunTriggered += OnWatchRunTriggered;
        _watchService.WatcherError += OnWatcherError;
    }

    /// <summary>GUI-115: Named handler for proper unsubscription in CleanupWatchers.</summary>
    private void OnWatcherError(string msg) => AddLog(msg, "WARN");

    // ═══ NAVIGATION (GUI-061 → Phase 1: 5-area shell) ═══════════════════
    private int _selectedNavIndex;
    /// <summary>Active sidebar navigation index (0=MissionControl, 1=Library, 2=Config, 3=Tools, 4=System).</summary>
    public int SelectedNavIndex
    {
        get => _selectedNavIndex;
        set
        {
            if (SetProperty(ref _selectedNavIndex, value))
            {
                OnPropertyChanged(nameof(SelectedNavTag));
                OnPropertyChanged(nameof(CanNavBack));
                OnPropertyChanged(nameof(CanNavForward));
                ApplyDefaultSubTab();
            }
        }
    }

    /// <summary>Navigation tag derived from index for ContentControl switching.</summary>
    public string SelectedNavTag
    {
        get => _selectedNavIndex switch
        {
            0 => "MissionControl",
            1 => "Library",
            2 => "Config",
            3 => "Tools",
            4 => "System",
            _ => "MissionControl"
        };
        set
        {
            int idx = value switch
            {
                // New 5-area tags
                "MissionControl" => 0,
                "Library" => 1,
                "Config" => 2,
                "Tools" => 3,
                "System" => 4,
                // Legacy compatibility aliases
                "Start" => 0,
                "Analyse" => 1,
                "Setup" => 2,
                "Log" => 4,
                _ => 0
            };
            SelectedNavIndex = idx;
        }
    }

    // ═══ SUB-TAB NAVIGATION ═════════════════════════════════════════════
    private string _selectedSubTab = "Dashboard";
    public string SelectedSubTab
    {
        get => _selectedSubTab;
        set => SetProperty(ref _selectedSubTab, value);
    }

    private void ApplyDefaultSubTab()
    {
        SelectedSubTab = _selectedNavIndex switch
        {
            0 => "Dashboard",
            1 => "Results",
            2 => "Workflow",
            3 => "ExternalTools",
            4 => "ActivityLog",
            _ => "Dashboard"
        };
    }

    // ═══ CONTEXT WING (Inspector) ═══════════════════════════════════════
    private bool _showContextWing = true;
    public bool ShowContextWing
    {
        get => _showContextWing;
        set => SetProperty(ref _showContextWing, value);
    }

    // ═══ GUI-063: NAVIGATION HISTORY ════════════════════════════════════
    private readonly Stack<int> _navBack = new();
    private readonly Stack<int> _navForward = new();
    private bool _isNavigatingHistory;

    public bool CanNavBack => _navBack.Count > 0;
    public bool CanNavForward => _navForward.Count > 0;

    /// <summary>Navigate to a specific screen by tag name (with history tracking).</summary>
    public void NavigateTo(string tag)
    {
        int newIndex = tag switch
        {
            "MissionControl" or "Start" => 0,
            "Library" or "Analyse" => 1,
            "Config" or "Setup" => 2,
            "Tools" => 3,
            "System" or "Log" => 4,
            _ => 0
        };

        if (!_isNavigatingHistory && newIndex != _selectedNavIndex)
        {
            _navBack.Push(_selectedNavIndex);
            _navForward.Clear();
        }
        SelectedNavIndex = newIndex;
    }

    public void NavGoBack()
    {
        if (_navBack.Count == 0) return;
        _isNavigatingHistory = true;
        _navForward.Push(_selectedNavIndex);
        SelectedNavIndex = _navBack.Pop();
        _isNavigatingHistory = false;
    }

    public void NavGoForward()
    {
        if (_navForward.Count == 0) return;
        _isNavigatingHistory = true;
        _navBack.Push(_selectedNavIndex);
        SelectedNavIndex = _navForward.Pop();
        _isNavigatingHistory = false;
    }

    // ═══ FIRST-RUN WIZARD (GUI-081) ═════════════════════════════════════
    private bool _showFirstRunWizard;
    public bool ShowFirstRunWizard
    {
        get => _showFirstRunWizard;
        set => SetProperty(ref _showFirstRunWizard, value);
    }

    private int _wizardStep;
    public int WizardStep
    {
        get => _wizardStep;
        set
        {
            if (SetProperty(ref _wizardStep, value))
            {
                OnPropertyChanged(nameof(WizardStepIs0));
                OnPropertyChanged(nameof(WizardStepIs1));
                OnPropertyChanged(nameof(WizardStepIs2));
                OnPropertyChanged(nameof(WizardNextLabel));
                WizardBackCommand?.NotifyCanExecuteChanged();
                WizardNextCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    // Step visibility helpers for XAML DataTrigger
    public bool WizardStepIs0 => WizardStep == 0;
    public bool WizardStepIs1 => WizardStep == 1;
    public bool WizardStepIs2 => WizardStep == 2;

    /// <summary>GUI-082: Returns the recommended region label based on current selection.</summary>
    public string WizardRegionSummary
    {
        get
        {
            var parts = new List<string>();
            if (PreferEU) parts.Add("EU");
            if (PreferUS) parts.Add("US");
            if (PreferJP) parts.Add("JP");
            if (PreferWORLD) parts.Add("World");
            return parts.Count > 0 ? string.Join(", ", parts) : "–";
        }
    }

    /// <summary>Dynamic label for the Next/Finish button.</summary>
    public string WizardNextLabel => WizardStep < 2 ? Loc["Wizard.Next"] : Loc["Wizard.Finish"];

    // ── Wizard Commands ─────────────────────────────────────────────────
    public IRelayCommand WizardNextCommand { get; private set; } = null!;
    public IRelayCommand WizardBackCommand { get; private set; } = null!;
    public IRelayCommand WizardSkipCommand { get; private set; } = null!;

    private void WizardNext()
    {
        if (WizardStep < 2)
        {
            WizardStep++;
        }
        else
        {
            // Finish wizard
            ShowFirstRunWizard = false;
            WizardStep = 0;
        }
    }

    private void WizardBack()
    {
        if (WizardStep > 0) WizardStep--;
    }

    private void WizardSkip()
    {
        ShowFirstRunWizard = false;
        WizardStep = 0;
    }

    /// <summary>GUI-082: Auto-detect region from OS locale on wizard start.</summary>
    public void ApplyLocaleRegionDefaults()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var region = culture.TwoLetterISOLanguageName.ToUpperInvariant() switch
        {
            "DE" or "FR" or "IT" or "ES" or "NL" or "PT" or "SV" or "DA" or "FI" or "NB" or "PL" => "EU",
            "EN" => culture.Name.Contains("GB", StringComparison.OrdinalIgnoreCase) ? "EU" : "US",
            "JA" => "JP",
            _ => "US"
        };
        // Set detected locale region to true; leave others at their defaults (all true).
        if (region == "EU") PreferEU = true;
        else if (region == "US") PreferUS = true;
        else if (region == "JP") PreferJP = true;
    }

    // ═══ GUI-101: SHORTCUT CHEATSHEET OVERLAY ═══════════════════════════
    private bool _showShortcutSheet;
    public bool ShowShortcutSheet
    {
        get => _showShortcutSheet;
        set => SetProperty(ref _showShortcutSheet, value);
    }

    private bool _showMoveInlineConfirm;
    public bool ShowMoveInlineConfirm
    {
        get => _showMoveInlineConfirm;
        set
        {
            if (SetProperty(ref _showMoveInlineConfirm, value))
                DeferCommandRequery();
        }
    }

    public RelayCommand ToggleShortcutSheetCommand { get; private set; } = null!;
    public RelayCommand ToggleCommandPaletteCommand { get; private set; } = null!;
    public RelayCommand ToggleDetailDrawerCommand { get; private set; } = null!;

    // ═══ DETAIL DRAWER (Phase 4.4) ═════════════════════════════════════
    private bool _showDetailDrawer;
    public bool ShowDetailDrawer
    {
        get => _showDetailDrawer;
        set => SetProperty(ref _showDetailDrawer, value);
    }

    // ═══ NOTIFICATIONS (GUI-055) ════════════════════════════════════════
    public ObservableCollection<NotificationItem> Notifications { get; } = [];

    public void ShowNotification(string message, string type = "Success", int autoCloseMs = 5000)
    {
        var item = new NotificationItem { Message = message, Type = type, AutoCloseMs = autoCloseMs };
        Notifications.Add(item);
        if (autoCloseMs > 0)
        {
            _ = Task.Delay(autoCloseMs).ContinueWith(_ =>
            {
                var d = System.Windows.Application.Current?.Dispatcher;
                d?.InvokeAsync(() => Notifications.Remove(item));
            });
        }
    }

    public void DismissNotification(NotificationItem item) => Notifications.Remove(item);

    // ═══ LOCALIZATION (GUI-047) ═════════════════════════════════════════
    /// <summary>XAML-bindable localization: {Binding Loc[Key]}.</summary>
    public ILocalizationService Loc => _loc;

    // ═══ COMMANDS ═══════════════════════════════════════════════════════
    public IRelayCommand RunCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand RollbackCommand { get; }
    public IRelayCommand AddRootCommand { get; }
    public IRelayCommand RemoveRootCommand { get; }
    public IRelayCommand OpenReportCommand { get; }
    public IRelayCommand ClearLogCommand { get; }
    public IRelayCommand ThemeToggleCommand { get; }
    public IRelayCommand GameKeyPreviewCommand { get; }
    public IRelayCommand PresetSafeDryRunCommand { get; }
    public IRelayCommand PresetFullSortCommand { get; }
    public IRelayCommand PresetConvertCommand { get; }
    public IRelayCommand BrowseToolPathCommand { get; }
    public IRelayCommand BrowseFolderPathCommand { get; }
    public IRelayCommand QuickPreviewCommand { get; }
    public IRelayCommand ConvertOnlyCommand { get; }
    public IRelayCommand RequestStartMoveCommand { get; }
    public IRelayCommand CancelStartMoveCommand { get; }
    public IRelayCommand StartMoveCommand { get; }
    public IRelayCommand SaveSettingsCommand { get; }
    public IRelayCommand LoadSettingsCommand { get; }
    public IRelayCommand WatchApplyCommand { get; }

    // GUI-063: Navigation history commands
    public IRelayCommand NavBackCommand { get; }
    public IRelayCommand NavForwardCommand { get; }
    public IRelayCommand GoToSetupCommand { get; }
    public IRelayCommand GoToAnalyseCommand { get; }
    public IRelayCommand GoToConfigCommand { get; }
    public IRelayCommand GoToLibraryCommand { get; }
    public IRelayCommand GoToToolsCommand { get; }
    public IRelayCommand ToggleContextWingCommand { get; }

    // ═══ FEATURE COMMANDS (TASK-111: replaces Click event handlers) ═══════
    public Dictionary<string, ICommand> FeatureCommands { get; } = new();

    private void InitFeatureCommands()
    {
        // All feature commands are registered by FeatureCommandService.RegisterCommands()
    }

    // ═══ COLLECTIONS ════════════════════════════════════════════════════
    public ObservableCollection<string> Roots { get; } = [];
    public ObservableCollection<LogEntry> LogEntries { get; } = [];
    public ObservableCollection<DatMapRow> DatMappings { get; } = [];
    public ObservableCollection<UiError> ErrorSummaryItems { get; } = [];

    /// <summary>Assigns FeatureCommands to matching ToolItems. Call after FeatureCommandService.RegisterCommands().</summary>
    public void WireToolItemCommands()
    {
        // Legacy: wire to old MainViewModel.ToolItems (until fully migrated)
        foreach (var item in ToolItems)
        {
            if (FeatureCommands.TryGetValue(item.Key, out var cmd))
                item.Command = cmd;
        }

        // GUI-021: Also wire to child ToolsViewModel
        foreach (var kvp in FeatureCommands)
            Tools.FeatureCommands[kvp.Key] = kvp.Value;
        Tools.WireToolItemCommands();
    }

    /// <summary>Add a log entry (thread-safe via Dispatcher if needed). Caps at 10,000 entries with batch trimming.</summary>
    public void AddLog(string text, string level = "INFO")
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            // V2-M03: No WPF Application (unit tests or post-shutdown) — write directly
            // only if called from the thread that created the VM; discard otherwise.
            if (_syncContext is null || SynchronizationContext.Current == _syncContext)
                AddLogCore(text, level);
            else
                System.Diagnostics.Debug.WriteLine($"[RomCleanup] Log discarded (no Dispatcher): [{level}] {text}");
            return;
        }
        if (dispatcher.CheckAccess())
        {
            AddLogCore(text, level);
        }
        else
        {
            dispatcher.InvokeAsync(
                () => AddLogCore(text, level));
        }
    }

    private const int MaxLogEntries = 10_000;
    private const int LogTrimBatchSize = 256;

    private void AddLogCore(string text, string level)
    {
        if (LogEntries.Count >= MaxLogEntries)
        {
            var removeCount = Math.Min(LogTrimBatchSize, LogEntries.Count);
            for (var i = 0; i < removeCount; i++)
                LogEntries.RemoveAt(0);

            // Keep _runLogStartIndex consistent after trimming oldest entries
            _runLogStartIndex = Math.Max(0, _runLogStartIndex - removeCount);
        }
        LogEntries.Add(new LogEntry(text, level));
    }

    // ═══ INPC INFRASTRUCTURE ════════════════════════════════════════════
    // Provided by ObservableObject base class (SetProperty, OnPropertyChanged).

    private void OnRootsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshStatus();
        OnPropertyChanged(nameof(HasNoRoots));
        OnPropertyChanged(nameof(HasRootsConfigured));
        MissionControl.UpdateSourceCount(Roots.Count);
        DeferCommandRequery();
    }

    public bool HasRootsConfigured => Roots.Count > 0;

    private bool _requeryScheduled;
    /// <summary>P1-008: Batches command CanExecute re-evaluation to one call per dispatcher cycle.</summary>
    private void DeferCommandRequery()
    {
        if (_requeryScheduled) return;
        _requeryScheduled = true;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            _requeryScheduled = false;
            NotifyAllCommands();
            return;
        }
        dispatcher.InvokeAsync(() =>
        {
            _requeryScheduled = false;
            NotifyAllCommands();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void NotifyAllCommands()
    {
        RunCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RollbackCommand.NotifyCanExecuteChanged();
        AddRootCommand.NotifyCanExecuteChanged();
        RemoveRootCommand.NotifyCanExecuteChanged();
        OpenReportCommand.NotifyCanExecuteChanged();
        GameKeyPreviewCommand.NotifyCanExecuteChanged();
        WatchApplyCommand.NotifyCanExecuteChanged();
        QuickPreviewCommand.NotifyCanExecuteChanged();
        ConvertOnlyCommand.NotifyCanExecuteChanged();
        RequestStartMoveCommand.NotifyCanExecuteChanged();
        CancelStartMoveCommand.NotifyCanExecuteChanged();
        StartMoveCommand.NotifyCanExecuteChanged();
    }

    // GUI-098: Respect prefers-reduced-motion (Windows "Show animations" setting)
    public bool ReduceMotion => !System.Windows.SystemParameters.ClientAreaAnimation;
}