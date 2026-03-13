using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
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
public sealed partial class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly ThemeService _theme;
    private readonly IDialogService _dialog;
    private readonly SettingsService _settings;
    private readonly SynchronizationContext? _syncContext;
    private readonly WatchService _watchService = new();
    private CancellationTokenSource? _cts;

    public MainViewModel() : this(new ThemeService(), new WpfDialogService()) { }

    public MainViewModel(ThemeService theme, IDialogService dialog, SettingsService? settings = null)
    {
        _theme = theme;
        _dialog = dialog;
        _settings = settings ?? new SettingsService();
        _syncContext = SynchronizationContext.Current;

        // Wire collection changes to status refresh
        Roots.CollectionChanged += OnRootsChanged;

        // ── Commands ────────────────────────────────────────────────────
        RunCommand = new RelayCommand(OnRun, () => !IsBusy && Roots.Count > 0);
        CancelCommand = new RelayCommand(OnCancel, () => IsBusy);
        RollbackCommand = new RelayCommand(OnRollback, () => !IsBusy && CanRollback);
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
        BrowseToolPathCommand = new RelayCommand(OnBrowseToolPath);
        BrowseFolderPathCommand = new RelayCommand(OnBrowseFolderPath);

        // Settings commands
        SaveSettingsCommand = new RelayCommand(OnSaveSettings);
        LoadSettingsCommand = new RelayCommand(OnLoadSettings);
        WatchApplyCommand = new RelayCommand(ToggleWatchMode, () => !IsBusy);

        // Quick workflow commands
        QuickPreviewCommand = new RelayCommand(
            () => { DryRun = true; RunCommand.Execute(null); },
            () => Roots.Count > 0 && !IsBusy);
        StartMoveCommand = new RelayCommand(
            () => { DryRun = false; RunCommand.Execute(null); },
            () => Roots.Count > 0 && !IsBusy);

        // Extension filter collection (UX-004)
        InitExtensionFilters();

        // Console filter collection (Runde 7: replaces 30 x:Name checkboxes)
        InitConsoleFilters();

        // Tool items collection (RD-004: Werkzeuge tab)
        InitToolItems();

        // Feature commands (TASK-111: replaces Click event handlers)
        InitFeatureCommands();

        // Wire watch-mode auto-run trigger
        _watchService.RunTriggered += OnWatchRunTriggered;
    }

    // ═══ COMMANDS ═══════════════════════════════════════════════════════
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RollbackCommand { get; }
    public ICommand AddRootCommand { get; }
    public ICommand RemoveRootCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand ThemeToggleCommand { get; }
    public ICommand GameKeyPreviewCommand { get; }
    public ICommand PresetSafeDryRunCommand { get; }
    public ICommand PresetFullSortCommand { get; }
    public ICommand PresetConvertCommand { get; }
    public ICommand BrowseToolPathCommand { get; }
    public ICommand BrowseFolderPathCommand { get; }
    public ICommand QuickPreviewCommand { get; }
    public ICommand StartMoveCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand LoadSettingsCommand { get; }
    public ICommand WatchApplyCommand { get; }

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
    public ObservableCollection<string> ErrorSummaryItems { get; } = [];

    /// <summary>Assigns FeatureCommands to matching ToolItems. Call after FeatureCommandService.RegisterCommands().</summary>
    public void WireToolItemCommands()
    {
        foreach (var item in ToolItems)
        {
            if (FeatureCommands.TryGetValue(item.Key, out var cmd))
                item.Command = cmd;
        }
    }

    /// <summary>Add a log entry (thread-safe via Dispatcher if needed).</summary>
    public void AddLog(string text, string level = "INFO")
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            LogEntries.Add(new LogEntry(text, level));
        }
        else
        {
            dispatcher.InvokeAsync(
                () => LogEntries.Add(new LogEntry(text, level)));
        }
    }

    // ═══ INPC INFRASTRUCTURE ════════════════════════════════════════════
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void OnRootsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshStatus();
        CommandManager.InvalidateRequerySuggested();
    }
}