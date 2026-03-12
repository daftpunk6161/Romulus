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
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
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

    // ═══ RUN RESULT STATE (moved from code-behind for MVVM command access) ═══
    private IReadOnlyList<RomCandidate> _lastCandidates = Array.Empty<RomCandidate>();
    public IReadOnlyList<RomCandidate> LastCandidates
    {
        get => _lastCandidates;
        set { _lastCandidates = value; OnPropertyChanged(); }
    }

    private IReadOnlyList<DedupeResult> _lastDedupeGroups = Array.Empty<DedupeResult>();
    public IReadOnlyList<DedupeResult> LastDedupeGroups
    {
        get => _lastDedupeGroups;
        set { _lastDedupeGroups = value; OnPropertyChanged(); }
    }

    private RunResult? _lastRunResult;
    public RunResult? LastRunResult
    {
        get => _lastRunResult;
        set { _lastRunResult = value; OnPropertyChanged(); }
    }

    private string? _lastAuditPath;
    public string? LastAuditPath
    {
        get => _lastAuditPath;
        set { _lastAuditPath = value; OnPropertyChanged(); }
    }

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

    // ═══ EXTENSION FILTERS (UX-004: VM-bound, replaces code-behind x:Name checkboxes) ═══
    public ObservableCollection<ExtensionFilterItem> ExtensionFilters { get; } = [];

    /// <summary>Grouped view for XAML binding with category headers.</summary>
    public ICollectionView ExtensionFiltersView { get; private set; } = null!;

    /// <summary>Returns checked extensions, or empty array if none selected (= scan all).</summary>
    public string[] GetSelectedExtensions() =>
        ExtensionFilters.Where(e => e.IsChecked).Select(e => e.Extension).ToArray();

    private void InitExtensionFilters()
    {
        var items = new (string ext, string cat, string tip)[]
        {
            (".chd", "Disc-Images", "CHD Disk-Image"),
            (".iso", "Disc-Images", "ISO-Abbild"),
            (".cue", "Disc-Images", "CUE Steuerdatei"),
            (".gdi", "Disc-Images", "GDI (Dreamcast)"),
            (".img", "Disc-Images", "IMG Disk-Image"),
            (".bin", "Disc-Images", "BIN (CD-Image)"),
            (".cso", "Disc-Images", "Compressed ISO (PSP)"),
            (".pbp", "Disc-Images", "PBP-Paket (PSP)"),
            (".zip", "Archive", "ZIP-Archiv"),
            (".7z",  "Archive", "7-Zip-Archiv"),
            (".rar", "Archive", "RAR-Archiv"),
            (".nes", "Cartridge / Modern", "NES ROM"),
            (".gba", "Cartridge / Modern", "Game Boy Advance ROM"),
            (".nds", "Cartridge / Modern", "Nintendo DS ROM"),
            (".nsp", "Cartridge / Modern", "NSP (Nintendo Switch)"),
            (".xci", "Cartridge / Modern", "XCI Cartridge-Image"),
            (".wbfs","Cartridge / Modern", "WBFS (Wii Backup)"),
            (".rvz", "Cartridge / Modern", "RVZ (GC/Wii, Dolphin)"),
        };
        foreach (var (ext, cat, tip) in items)
            ExtensionFilters.Add(new ExtensionFilterItem { Extension = ext, Category = cat, ToolTip = tip });

        ExtensionFiltersView = CollectionViewSource.GetDefaultView(ExtensionFilters);
        ExtensionFiltersView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ExtensionFilterItem.Category)));
    }

    // ═══ CONSOLE FILTERS (VM-bound, replaces code-behind x:Name checkboxes) ═══
    public ObservableCollection<ConsoleFilterItem> ConsoleFilters { get; } = [];

    /// <summary>Grouped view for XAML binding with category headers (Sony, Nintendo, Sega, Andere).</summary>
    public ICollectionView ConsoleFiltersView { get; private set; } = null!;

    /// <summary>Returns checked console keys, or empty array if none selected (= all consoles).</summary>
    public string[] GetSelectedConsoles() =>
        ConsoleFilters.Where(c => c.IsChecked).Select(c => c.Key).ToArray();

    private void InitConsoleFilters()
    {
        var items = new (string key, string display, string cat)[]
        {
            ("PS1",    "PlayStation",               "Sony"),
            ("PS2",    "PlayStation 2",             "Sony"),
            ("PS3",    "PlayStation 3",             "Sony"),
            ("PSP",    "PSP",                       "Sony"),
            ("NES",    "NES / Famicom",             "Nintendo"),
            ("SNES",   "SNES / Super Famicom",      "Nintendo"),
            ("N64",    "Nintendo 64",               "Nintendo"),
            ("GC",     "GameCube",                  "Nintendo"),
            ("WII",    "Wii",                       "Nintendo"),
            ("WIIU",   "Wii U",                     "Nintendo"),
            ("SWITCH", "Nintendo Switch",           "Nintendo"),
            ("GB",     "Game Boy",                  "Nintendo"),
            ("GBC",    "Game Boy Color",            "Nintendo"),
            ("GBA",    "Game Boy Advance",          "Nintendo"),
            ("NDS",    "Nintendo DS",               "Nintendo"),
            ("3DS",    "Nintendo 3DS",              "Nintendo"),
            ("MD",     "Mega Drive / Genesis",      "Sega"),
            ("SCD",    "Mega-CD / Sega CD",         "Sega"),
            ("SAT",    "Saturn",                    "Sega"),
            ("DC",     "Dreamcast",                 "Sega"),
            ("SMS",    "Master System",             "Sega"),
            ("GG",     "Game Gear",                 "Sega"),
            ("ARCADE", "Arcade / MAME / FBNeo",     "Andere"),
            ("NEOGEO", "Neo Geo",                   "Andere"),
            ("NEOCD",  "Neo Geo CD",                "Andere"),
            ("PCE",    "PC Engine / TurboGrafx-16", "Andere"),
            ("PCECD",  "PC Engine CD",              "Andere"),
            ("DOS",    "DOS / PC",                  "Andere"),
            ("3DO",    "3DO",                       "Andere"),
            ("JAG",    "Atari Jaguar",              "Andere"),
        };
        foreach (var (key, display, cat) in items)
            ConsoleFilters.Add(new ConsoleFilterItem { Key = key, DisplayName = display, Category = cat });

        ConsoleFiltersView = CollectionViewSource.GetDefaultView(ConsoleFilters);
        ConsoleFiltersView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ConsoleFilterItem.Category)));
    }

    // ═══ TOOL ITEMS (RD-004: Werkzeuge tab with categorized, filterable list) ═══
    public ObservableCollection<ToolItem> ToolItems { get; } = [];

    /// <summary>Grouped view for XAML binding with category headers.</summary>
    public ICollectionView ToolItemsView { get; private set; } = null!;

    private string _toolFilterText = "";
    public string ToolFilterText
    {
        get => _toolFilterText;
        set
        {
            if (SetField(ref _toolFilterText, value))
                ToolItemsView?.Refresh();
        }
    }

    private void InitToolItems()
    {
        var items = new (string key, string display, string cat, string desc, string icon, bool needsResult)[]
        {
            // Analyse & Berichte
            ("QuickPreview",       "Quick-Preview",              "Analyse & Berichte",        "Schnelle ROM-Vorschau (DryRun)",               "\xE8A7", false),
            ("HealthScore",        "Health-Score",               "Analyse & Berichte",        "Sammlungsqualität prüfen",                     "\xE8CB", true),
            ("CollectionDiff",     "Collection-Diff",            "Analyse & Berichte",        "Sammlungen vergleichen",                       "\xE8F1", false),
            ("DuplicateInspector", "Duplikat-Inspektor",         "Analyse & Berichte",        "Duplikate untersuchen",                        "\xE71D", true),
            ("ConversionEstimate", "Konvertierungs-Schätzung",   "Analyse & Berichte",        "Speicherersparnis berechnen",                  "\xE8EF", true),
            ("JunkReport",         "Junk-Bericht",               "Analyse & Berichte",        "Detaillierter Junk-Bericht",                   "\xE74D", true),
            ("RomFilter",          "ROM-Filter",                 "Analyse & Berichte",        "ROM-Sammlung durchsuchen",                     "\xE721", true),
            ("DuplicateHeatmap",   "Duplikat-Heatmap",           "Analyse & Berichte",        "Duplikatverteilung visualisieren",             "\xEB05", true),
            ("MissingRom",         "Fehlende ROMs",              "Analyse & Berichte",        "Fehlende ROMs ermitteln",                      "\xE783", true),
            ("CrossRootDupe",      "Cross-Root-Duplikate",       "Analyse & Berichte",        "Duplikate über mehrere Roots finden",          "\xE8B9", true),
            ("HeaderAnalysis",     "Header-Analyse",             "Analyse & Berichte",        "ROM-Header analysieren",                       "\xE9D9", true),
            ("Completeness",       "Vollständigkeit",            "Analyse & Berichte",        "Vollständigkeitsbericht",                      "\xE73E", true),
            ("DryRunCompare",      "DryRun-Vergleich",           "Analyse & Berichte",        "Zwei DryRun-Ergebnisse vergleichen",           "\xE8F1", false),
            ("TrendAnalysis",      "Trend-Analyse",              "Analyse & Berichte",        "Historische Trends",                           "\xE9D2", false),
            ("EmulatorCompat",     "Emulator-Kompatibilität",    "Analyse & Berichte",        "Kompatibilitätsmatrix",                        "\xE7FC", false),

            // Konvertierung & Hashing
            ("ConversionPipeline", "Konvertierungs-Pipeline",    "Konvertierung & Hashing",   "Konvertierungspipeline starten",               "\xE8AB", false),
            ("NKitConvert",        "NKit-Konvertierung",         "Konvertierung & Hashing",   "NKit-Images konvertieren",                     "\xE8AB", false),
            ("ConvertQueue",       "Konvert-Warteschlange",      "Konvertierung & Hashing",   "Warteschlange anzeigen",                       "\xE8CB", false),
            ("ConversionVerify",   "Konvertierung verifizieren", "Konvertierung & Hashing",   "Konvertierte Dateien prüfen",                  "\xE73E", false),
            ("FormatPriority",     "Format-Priorität",           "Konvertierung & Hashing",   "Format-Prioritätsliste anzeigen",              "\xE8CB", false),
            ("ParallelHashing",    "Parallel-Hashing",           "Konvertierung & Hashing",   "Hash-Threading konfigurieren",                 "\xE8CB", false),
            ("GpuHashing",         "GPU-Hashing",                "Konvertierung & Hashing",   "GPU-beschleunigtes Hashing",                   "\xE8CB", false),

            // DAT & Verifizierung
            ("DatAutoUpdate",      "DAT Auto-Update",            "DAT & Verifizierung",       "Lokale DAT-Dateien prüfen",                    "\xE895", false),
            ("DatDiffViewer",      "DAT-Diff-Viewer",            "DAT & Verifizierung",       "DAT-Versionen vergleichen",                    "\xE8F1", false),
            ("TosecDat",           "TOSEC-DAT",                  "DAT & Verifizierung",       "TOSEC-DAT importieren",                        "\xE8B5", false),
            ("CustomDatEditor",    "Custom-DAT-Editor",          "DAT & Verifizierung",       "Eigene DAT-Einträge erstellen",                "\xE70F", false),
            ("HashDatabaseExport", "Hash-Datenbank",             "DAT & Verifizierung",       "Hash-Datenbank exportieren",                   "\xE792", true),

            // Sammlungsverwaltung
            ("CollectionManager",  "Smart Collection",           "Sammlungsverwaltung",       "Sammlung intelligent verwalten",               "\xE8F1", true),
            ("CloneListViewer",    "Clone-Liste",                "Sammlungsverwaltung",       "Clone-/Parent-Beziehungen",                    "\xE8B9", true),
            ("CoverScraper",       "Cover-Scraper",              "Sammlungsverwaltung",       "Cover-Bilder zuordnen",                        "\xE8B9", true),
            ("GenreClassification","Genre-Klassifikation",       "Sammlungsverwaltung",       "ROMs nach Genre einordnen",                    "\xE8CB", true),
            ("PlaytimeTracker",    "Spielzeit-Tracker",          "Sammlungsverwaltung",       "RetroArch-Spielzeiten auslesen",               "\xE916", false),
            ("CollectionSharing",  "Sammlung teilen",            "Sammlungsverwaltung",       "Sammlungsliste exportieren",                   "\xE72D", true),
            ("VirtualFolderPreview","Virtuelle Ordner",          "Sammlungsverwaltung",       "Virtuelle Ordnerstruktur planen",              "\xE8B7", true),

            // Sicherheit & Integrität
            ("IntegrityMonitor",   "Integritäts-Monitor",        "Sicherheit & Integrität",   "Baseline erstellen/prüfen",                    "\xE72E", true),
            ("BackupManager",      "Backup-Manager",             "Sicherheit & Integrität",   "Winner-Dateien sichern",                       "\xE8F1", true),
            ("Quarantine",         "Quarantäne",                 "Sicherheit & Integrität",   "Verdächtige Dateien isolieren",                "\xE7BA", true),
            ("RuleEngine",         "Regel-Engine",               "Sicherheit & Integrität",   "Aktive Regeln anzeigen",                       "\xE713", false),
            ("PatchEngine",        "Patch-Engine",               "Sicherheit & Integrität",   "ROM-Patches anwenden",                         "\xE70F", false),
            ("HeaderRepair",       "Header-Reparatur",           "Sicherheit & Integrität",   "ROM-Header reparieren",                        "\xE90F", false),
            ("RollbackQuick",      "Schnell-Rollback",           "Sicherheit & Integrität",   "Letzten Lauf rückgängig machen",               "\xE777", false),
            ("RollbackUndo",       "Rollback Undo",              "Sicherheit & Integrität",   "Rollback rückgängig machen",                   "\xE7A7", false),
            ("RollbackRedo",       "Rollback Redo",              "Sicherheit & Integrität",   "Rollback wiederherstellen",                    "\xE7A6", false),

            // Workflow & Automatisierung
            ("CommandPalette",     "Command-Palette",            "Workflow & Automatisierung", "Befehle suchen und ausführen",                 "\xE721", false),
            ("SplitPanelPreview",  "Split-Panel",                "Workflow & Automatisierung", "Winner/Loser-Vergleich",                       "\xE8A0", true),
            ("FilterBuilder",      "Filter-Builder",             "Workflow & Automatisierung", "Erweiterte Filter erstellen",                  "\xE71C", true),
            ("SortTemplates",      "Sort-Templates",             "Workflow & Automatisierung", "Sortierungs-Vorlagen",                         "\xE8CB", false),
            ("PipelineEngine",     "Pipeline-Engine",            "Workflow & Automatisierung", "Pipeline-Status anzeigen",                     "\xE8CB", false),
            ("SystemTray",         "System-Tray",                "Workflow & Automatisierung", "System-Tray ein-/ausschalten",                 "\xE8CB", false),
            ("SchedulerAdvanced",  "Cron-Tester",                "Workflow & Automatisierung", "Cron-Expressions testen",                     "\xE787", false),
            ("RulePackSharing",    "Regel-Pakete",               "Workflow & Automatisierung", "Regeln importieren/exportieren",               "\xE72D", false),
            ("ArcadeMergeSplit",   "Arcade Merge/Split",         "Workflow & Automatisierung", "Arcade-Sets analysieren",                     "\xE8CB", false),
            ("AutoProfile",        "Auto-Profil",                "Workflow & Automatisierung", "Profil automatisch erkennen",                  "\xE713", false),

            // Export & Integration
            ("PdfReport",          "PDF-Report",                 "Export & Integration",       "HTML-Report für PDF-Druck",                    "\xE8A5", true),
            ("LauncherIntegration","Launcher-Integration",       "Export & Integration",       "RetroArch-Playlist exportieren",               "\xE768", true),
            ("ToolImport",         "Tool-Import",                "Export & Integration",       "DAT-Dateien importieren",                      "\xE8B5", false),
            ("DuplicateExport",    "Duplikate exportieren",      "Export & Integration",       "Duplikatliste als CSV speichern",              "\xE792", true),
            ("ExportCsv",          "CSV Export",                 "Export & Integration",       "Sammlung als CSV exportieren",                 "\xE792", true),
            ("ExportExcel",        "Excel Export",               "Export & Integration",       "Sammlung als Excel-XML exportieren",           "\xE792", true),

            // Infrastruktur & Deployment
            ("StorageTiering",     "Storage-Tiering",            "Infrastruktur",              "Speicher-Analyse",                             "\xE8CB", true),
            ("NasOptimization",    "NAS-Optimierung",            "Infrastruktur",              "NAS-Pfad-Infos anzeigen",                     "\xE8CB", false),
            ("FtpSource",          "FTP-Quelle",                 "Infrastruktur",              "FTP/SFTP-Quelle konfigurieren",               "\xE774", false),
            ("CloudSync",          "Cloud-Sync",                 "Infrastruktur",              "Cloud-Status prüfen",                          "\xE753", false),
            ("PluginMarketplaceFeature","Plugin-Marktplatz",     "Infrastruktur",              "Plugin-System (geplant)",                      "\xE71B", false),
            ("PluginManager",      "Plugin-Manager",             "Infrastruktur",              "Installierte Plugins verwalten",               "\xE71B", false),
            ("PortableMode",       "Portable Modus",             "Infrastruktur",              "Portable-Modus Status",                        "\xE8CB", false),
            ("DockerContainer",    "Docker",                     "Infrastruktur",              "Docker-Dateien generieren",                    "\xE8CB", false),
            ("MobileWebUI",        "Mobile Web UI",              "Infrastruktur",              "REST API starten",                             "\xE774", false),
            ("WindowsContextMenu", "Kontextmenü",                "Infrastruktur",              "Windows-Kontextmenü registrieren",             "\xE8CB", false),
            ("HardlinkMode",       "Hardlink-Modus",             "Infrastruktur",              "Hardlink-Schätzung berechnen",                 "\xE8CB", true),
            ("MultiInstanceSync",  "Multi-Instanz",              "Infrastruktur",              "Lock-Dateien verwalten",                       "\xE8CB", false),

            // UI & Erscheinungsbild
            ("Accessibility",      "Barrierefreiheit",           "UI & Erscheinungsbild",      "Schriftgröße/Kontrast anpassen",               "\xE7F8", false),
            ("ThemeEngine",        "Theme-Engine",               "UI & Erscheinungsbild",      "Theme-Optionen",                               "\xE771", false),
        };
        foreach (var (key, display, cat, desc, icon, needsResult) in items)
            ToolItems.Add(new ToolItem { Key = key, DisplayName = display, Category = cat, Description = desc, Icon = icon, RequiresRunResult = needsResult });

        ToolItemsView = CollectionViewSource.GetDefaultView(ToolItems);
        ToolItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ToolItem.Category)));
        ToolItemsView.Filter = ToolItemFilter;
    }

    private bool ToolItemFilter(object obj)
    {
        if (obj is not ToolItem item) return false;
        if (string.IsNullOrWhiteSpace(_toolFilterText)) return true;
        return item.DisplayName.Contains(_toolFilterText, StringComparison.OrdinalIgnoreCase)
            || item.Category.Contains(_toolFilterText, StringComparison.OrdinalIgnoreCase)
            || item.Description.Contains(_toolFilterText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Assigns FeatureCommands to matching ToolItems. Call after FeatureCommandService.RegisterCommands().</summary>
    public void WireToolItemCommands()
    {
        foreach (var item in ToolItems)
        {
            if (FeatureCommands.TryGetValue(item.Key, out var cmd))
                item.Command = cmd;
        }
    }

    // ═══ PATH PROPERTIES (persisted) ════════════════════════════════════
    private string _trashRoot = "";
    public string TrashRoot { get => _trashRoot; set => SetField(ref _trashRoot, value); }

    private string _datRoot = "";
    public string DatRoot { get => _datRoot; set { if (SetField(ref _datRoot, value)) RefreshStatus(); } }

    private string _auditRoot = "";
    public string AuditRoot { get => _auditRoot; set => SetField(ref _auditRoot, value); }

    private string _ps3DupesRoot = "";
    public string Ps3DupesRoot { get => _ps3DupesRoot; set => SetField(ref _ps3DupesRoot, value); }

    // ═══ TOOL PATHS (persisted) ═════════════════════════════════════════
    private string _toolChdman = "";
    public string ToolChdman { get => _toolChdman; set { if (SetField(ref _toolChdman, value)) RefreshStatus(); } }

    private string _toolDolphin = "";
    public string ToolDolphin { get => _toolDolphin; set { if (SetField(ref _toolDolphin, value)) RefreshStatus(); } }

    private string _tool7z = "";
    public string Tool7z { get => _tool7z; set { if (SetField(ref _tool7z, value)) RefreshStatus(); } }

    private string _toolPsxtract = "";
    public string ToolPsxtract { get => _toolPsxtract; set => SetField(ref _toolPsxtract, value); }

    private string _toolCiso = "";
    public string ToolCiso { get => _toolCiso; set => SetField(ref _toolCiso, value); }

    // ═══ BOOLEAN FLAGS (persisted) ══════════════════════════════════════
    private bool _sortConsole = true;
    public bool SortConsole { get => _sortConsole; set => SetField(ref _sortConsole, value); }

    private bool _aliasKeying;
    public bool AliasKeying { get => _aliasKeying; set => SetField(ref _aliasKeying, value); }

    private bool _useDat;
    public bool UseDat { get => _useDat; set { if (SetField(ref _useDat, value)) RefreshStatus(); } }

    private bool _datFallback;
    public bool DatFallback { get => _datFallback; set => SetField(ref _datFallback, value); }

    private bool _dryRun = true;
    public bool DryRun { get => _dryRun; set => SetField(ref _dryRun, value); }

    private bool _convertEnabled;
    public bool ConvertEnabled { get => _convertEnabled; set { if (SetField(ref _convertEnabled, value)) RefreshStatus(); } }

    private bool _confirmMove = true;
    public bool ConfirmMove { get => _confirmMove; set => SetField(ref _confirmMove, value); }

    private bool _aggressiveJunk;
    public bool AggressiveJunk { get => _aggressiveJunk; set => SetField(ref _aggressiveJunk, value); }

    private bool _crcVerifyScan;
    public bool CrcVerifyScan { get => _crcVerifyScan; set => SetField(ref _crcVerifyScan, value); }

    private bool _crcVerifyDat;
    public bool CrcVerifyDat { get => _crcVerifyDat; set => SetField(ref _crcVerifyDat, value); }

    private bool _safetyStrict;
    public bool SafetyStrict { get => _safetyStrict; set => SetField(ref _safetyStrict, value); }

    private bool _safetyPrompts;
    public bool SafetyPrompts { get => _safetyPrompts; set => SetField(ref _safetyPrompts, value); }

    private bool _jpOnlySelected;
    public bool JpOnlySelected { get => _jpOnlySelected; set => SetField(ref _jpOnlySelected, value); }

    // ═══ STRING CONFIG (persisted) ══════════════════════════════════════
    private string _protectedPaths = "";
    public string ProtectedPaths { get => _protectedPaths; set => SetField(ref _protectedPaths, value); }

    private string _safetySandbox = "";
    public string SafetySandbox { get => _safetySandbox; set => SetField(ref _safetySandbox, value); }

    private string _jpKeepConsoles = "";
    public string JpKeepConsoles { get => _jpKeepConsoles; set => SetField(ref _jpKeepConsoles, value); }

    private string _logLevel = "Info";
    public string LogLevel { get => _logLevel; set => SetField(ref _logLevel, value); }

    private string _locale = "de";
    public string Locale { get => _locale; set => SetField(ref _locale, value); }

    private bool _isWatchModeActive;
    public bool IsWatchModeActive { get => _isWatchModeActive; set => SetField(ref _isWatchModeActive, value); }

    private string _datHashType = "SHA1";
    public string DatHashType { get => _datHashType; set => SetField(ref _datHashType, value); }

    // ═══ REGION PREFERENCES (persisted) ═════════════════════════════════
    private bool _preferEU = true;
    public bool PreferEU { get => _preferEU; set => SetField(ref _preferEU, value); }

    private bool _preferUS = true;
    public bool PreferUS { get => _preferUS; set => SetField(ref _preferUS, value); }

    private bool _preferJP = true;
    public bool PreferJP { get => _preferJP; set => SetField(ref _preferJP, value); }

    private bool _preferWORLD = true;
    public bool PreferWORLD { get => _preferWORLD; set => SetField(ref _preferWORLD, value); }

    private bool _preferDE;
    public bool PreferDE { get => _preferDE; set => SetField(ref _preferDE, value); }

    private bool _preferFR;
    public bool PreferFR { get => _preferFR; set => SetField(ref _preferFR, value); }

    private bool _preferIT;
    public bool PreferIT { get => _preferIT; set => SetField(ref _preferIT, value); }

    private bool _preferES;
    public bool PreferES { get => _preferES; set => SetField(ref _preferES, value); }

    private bool _preferAU;
    public bool PreferAU { get => _preferAU; set => SetField(ref _preferAU, value); }

    private bool _preferASIA;
    public bool PreferASIA { get => _preferASIA; set => SetField(ref _preferASIA, value); }

    private bool _preferKR;
    public bool PreferKR { get => _preferKR; set => SetField(ref _preferKR, value); }

    private bool _preferCN;
    public bool PreferCN { get => _preferCN; set => SetField(ref _preferCN, value); }

    private bool _preferBR;
    public bool PreferBR { get => _preferBR; set => SetField(ref _preferBR, value); }

    private bool _preferNL;
    public bool PreferNL { get => _preferNL; set => SetField(ref _preferNL, value); }

    private bool _preferSE;
    public bool PreferSE { get => _preferSE; set => SetField(ref _preferSE, value); }

    private bool _preferSCAN;
    public bool PreferSCAN { get => _preferSCAN; set => SetField(ref _preferSCAN, value); }

    // ═══ UI MODE ════════════════════════════════════════════════════════
    private bool _isSimpleMode = true;
    public bool IsSimpleMode
    {
        get => _isSimpleMode;
        set { if (SetField(ref _isSimpleMode, value)) OnPropertyChanged(nameof(IsExpertMode)); }
    }
    public bool IsExpertMode => !_isSimpleMode;

    // Simple-mode options (not persisted — derived from main options at run time)
    private int _simpleRegionIndex;
    public int SimpleRegionIndex { get => _simpleRegionIndex; set => SetField(ref _simpleRegionIndex, value); }

    private bool _simpleDupes = true;
    public bool SimpleDupes { get => _simpleDupes; set => SetField(ref _simpleDupes, value); }

    private bool _simpleJunk = true;
    public bool SimpleJunk { get => _simpleJunk; set => SetField(ref _simpleJunk, value); }

    private bool _simpleSort = true;
    public bool SimpleSort { get => _simpleSort; set => SetField(ref _simpleSort, value); }

    // ═══ RUN STATE (UX-002: explicit state machine) ════════════════════
    private RunState _runState = RunState.Idle;
    public RunState CurrentRunState
    {
        get => _runState;
        set
        {
            if (SetField(ref _runState, value))
            {
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(ShowStartMoveButton));
                OnPropertyChanged(nameof(HasRunResult));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Derived from RunState — true while any run phase is active.</summary>
    public bool IsBusy => _runState is RunState.Preflight or RunState.Scanning
        or RunState.Deduplicating or RunState.Moving or RunState.Converting;
    public bool IsIdle => !IsBusy;

    /// <summary>Show the "Start as Move" button after a successful DryRun (UX-008).</summary>
    public bool ShowStartMoveButton => _runState == RunState.CompletedDryRun && !IsBusy;

    /// <summary>True when a run has completed (DryRun or Move) and results are available (UX-003/TEST-009).</summary>
    public bool HasRunResult => _runState is RunState.Completed or RunState.CompletedDryRun;

    // ═══ CONFLICT POLICY (UX-007: was YesNoCancel hack, now VM property) ═
    private ConflictPolicy _conflictPolicy = ConflictPolicy.Rename;
    public ConflictPolicy ConflictPolicy
    {
        get => _conflictPolicy;
        set => SetField(ref _conflictPolicy, value);
    }

    /// <summary>Index for ComboBox binding (0=Rename, 1=Skip, 2=Overwrite).</summary>
    public int ConflictPolicyIndex
    {
        get => (int)_conflictPolicy;
        set => ConflictPolicy = (ConflictPolicy)value;
    }

    // ═══ ROLLBACK HISTORY (UX-010: moved from code-behind) ══════════════
    private readonly Stack<string> _rollbackUndoStack = new();
    private readonly Stack<string> _rollbackRedoStack = new();

    public bool HasRollbackUndo => _rollbackUndoStack.Count > 0;
    public bool HasRollbackRedo => _rollbackRedoStack.Count > 0;

    public void PushRollbackUndo(string auditPath)
    {
        _rollbackUndoStack.Push(auditPath);
        _rollbackRedoStack.Clear();
        OnPropertyChanged(nameof(HasRollbackUndo));
        OnPropertyChanged(nameof(HasRollbackRedo));
    }

    public string? PopRollbackUndo()
    {
        if (_rollbackUndoStack.Count == 0) return null;
        var path = _rollbackUndoStack.Pop();
        _rollbackRedoStack.Push(path);
        OnPropertyChanged(nameof(HasRollbackUndo));
        OnPropertyChanged(nameof(HasRollbackRedo));
        return path;
    }

    public string? PopRollbackRedo()
    {
        if (_rollbackRedoStack.Count == 0) return null;
        var path = _rollbackRedoStack.Pop();
        _rollbackUndoStack.Push(path);
        OnPropertyChanged(nameof(HasRollbackUndo));
        OnPropertyChanged(nameof(HasRollbackRedo));
        return path;
    }

    private double _progress;
    public double Progress { get => _progress; set => SetField(ref _progress, value); }

    private string _progressText = "";
    public string ProgressText { get => _progressText; set => SetField(ref _progressText, value); }

    // ═══ PERFORMANCE DETAILS ════════════════════════════════════════════
    private string _perfPhase = "Phase: –";
    public string PerfPhase { get => _perfPhase; set => SetField(ref _perfPhase, value); }

    private string _perfFile = "Datei: –";
    public string PerfFile { get => _perfFile; set => SetField(ref _perfFile, value); }

    private string _busyHint = "";
    public string BusyHint { get => _busyHint; set => SetField(ref _busyHint, value); }

    private string? _selectedRoot;
    public string? SelectedRoot
    {
        get => _selectedRoot;
        set { SetField(ref _selectedRoot, value); CommandManager.InvalidateRequerySuggested(); }
    }

    private bool _canRollback;
    public bool CanRollback
    {
        get => _canRollback;
        set { SetField(ref _canRollback, value); CommandManager.InvalidateRequerySuggested(); }
    }

    private string _lastReportPath = "";
    public string LastReportPath
    {
        get => _lastReportPath;
        set { SetField(ref _lastReportPath, value); CommandManager.InvalidateRequerySuggested(); }
    }

    private bool _showDryRunBanner = true;
    public bool ShowDryRunBanner { get => _showDryRunBanner; set => SetField(ref _showDryRunBanner, value); }

    private bool _showMoveCompleteBanner;
    public bool ShowMoveCompleteBanner { get => _showMoveCompleteBanner; set => SetField(ref _showMoveCompleteBanner, value); }

    // GameKey preview
    private string _gameKeyPreviewInput = "";
    public string GameKeyPreviewInput { get => _gameKeyPreviewInput; set => SetField(ref _gameKeyPreviewInput, value); }

    private string _gameKeyPreviewOutput = "–";
    public string GameKeyPreviewOutput { get => _gameKeyPreviewOutput; set => SetField(ref _gameKeyPreviewOutput, value); }

    // Theme
    public string ThemeToggleText => _theme.Current switch
    {
        AppTheme.Dark => "☀ Hell",
        AppTheme.Light => "◐ Kontrast",
        AppTheme.HighContrast => "☾ Dunkel",
        _ => "☾ Dunkel",
    };

    // ═══ STATUS INDICATORS ══════════════════════════════════════════════
    private string _statusRoots = "Roots: –";
    public string StatusRoots { get => _statusRoots; set => SetField(ref _statusRoots, value); }

    private string _statusTools = "Tools: –";
    public string StatusTools { get => _statusTools; set => SetField(ref _statusTools, value); }

    private string _statusDat = "DAT: –";
    public string StatusDat { get => _statusDat; set => SetField(ref _statusDat, value); }

    private string _statusReady = "Status: –";
    public string StatusReady { get => _statusReady; set => SetField(ref _statusReady, value); }

    private string _statusRuntime = "Laufzeit: –";
    public string StatusRuntime { get => _statusRuntime; set => SetField(ref _statusRuntime, value); }

    private StatusLevel _rootsStatusLevel = StatusLevel.Missing;
    public StatusLevel RootsStatusLevel { get => _rootsStatusLevel; set => SetField(ref _rootsStatusLevel, value); }

    private StatusLevel _toolsStatusLevel = StatusLevel.Missing;
    public StatusLevel ToolsStatusLevel { get => _toolsStatusLevel; set => SetField(ref _toolsStatusLevel, value); }

    private StatusLevel _datStatusLevel = StatusLevel.Missing;
    public StatusLevel DatStatusLevel { get => _datStatusLevel; set => SetField(ref _datStatusLevel, value); }

    private StatusLevel _readyStatusLevel = StatusLevel.Missing;
    public StatusLevel ReadyStatusLevel { get => _readyStatusLevel; set => SetField(ref _readyStatusLevel, value); }

    // ═══ DASHBOARD COUNTERS ═════════════════════════════════════════════
    private string _dashMode = "–";
    public string DashMode { get => _dashMode; set => SetField(ref _dashMode, value); }

    private string _dashWinners = "0";
    public string DashWinners { get => _dashWinners; set => SetField(ref _dashWinners, value); }

    private string _dashDupes = "0";
    public string DashDupes { get => _dashDupes; set => SetField(ref _dashDupes, value); }

    private string _dashJunk = "0";
    public string DashJunk { get => _dashJunk; set => SetField(ref _dashJunk, value); }

    private string _dashDuration = "00:00";
    public string DashDuration { get => _dashDuration; set => SetField(ref _dashDuration, value); }

    private string _healthScore = "–";
    public string HealthScore { get => _healthScore; set => SetField(ref _healthScore, value); }

    // ═══ STEP INDICATOR ═════════════════════════════════════════════════
    private int _currentStep;
    public int CurrentStep { get => _currentStep; set => SetField(ref _currentStep, value); }

    private string _stepLabel1 = "Keine Ordner";
    public string StepLabel1 { get => _stepLabel1; set => SetField(ref _stepLabel1, value); }

    private string _stepLabel2 = "Bereit";
    public string StepLabel2 { get => _stepLabel2; set => SetField(ref _stepLabel2, value); }

    private string _stepLabel3 = "F5 drücken";
    public string StepLabel3 { get => _stepLabel3; set => SetField(ref _stepLabel3, value); }

    // ═══ PUBLIC METHODS ═════════════════════════════════════════════════

    /// <summary>Confirm before destructive Move operations (uses injected IDialogService).</summary>
    public bool ConfirmMoveDialog()
    {
        return _dialog.Confirm(
            $"Modus 'Move' verschiebt Dateien in den Papierkorb.\n"
            + $"Roots: {string.Join(", ", Roots)}\n\nFortfahren?",
            "Move bestätigen");
    }

    /// <summary>Build the preferred regions array from all boolean flags.</summary>
    public string[] GetPreferredRegions()
    {
        // TASK-032: In simple mode, translate SimpleRegionIndex to region preferences
        if (IsSimpleMode)
        {
            return SimpleRegionIndex switch
            {
                0 => ["EU", "DE", "WORLD", "US", "JP"],    // Europa
                1 => ["US", "WORLD", "EU", "JP"],           // Nordamerika
                2 => ["JP", "ASIA", "WORLD", "US", "EU"],   // Japan
                3 => ["WORLD", "EU", "US", "JP"],            // Weltweit
                _ => ["EU", "US", "WORLD", "JP"]
            };
        }

        var regions = new List<string>(16);
        if (PreferEU) regions.Add("EU");
        if (PreferUS) regions.Add("US");
        if (PreferWORLD) regions.Add("WORLD");
        if (PreferJP) regions.Add("JP");
        if (PreferDE) regions.Add("DE");
        if (PreferFR) regions.Add("FR");
        if (PreferIT) regions.Add("IT");
        if (PreferES) regions.Add("ES");
        if (PreferAU) regions.Add("AU");
        if (PreferASIA) regions.Add("ASIA");
        if (PreferKR) regions.Add("KR");
        if (PreferCN) regions.Add("CN");
        if (PreferBR) regions.Add("BR");
        if (PreferNL) regions.Add("NL");
        if (PreferSE) regions.Add("SE");
        if (PreferSCAN) regions.Add("SCAN");
        return [.. regions];
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

    /// <summary>Recompute all status dot indicators.</summary>
    public void RefreshStatus()
    {
        // Roots
        bool hasRoots = Roots.Count > 0;
        RootsStatusLevel = hasRoots ? StatusLevel.Ok : StatusLevel.Missing;
        StatusRoots = hasRoots ? $"{Roots.Count} Ordner konfiguriert" : "Keine Ordner";
        StepLabel1 = hasRoots ? $"{Roots.Count} Ordner" : "Keine Ordner";

        // Tools — check that specified paths actually exist
        bool hasChdman = !string.IsNullOrWhiteSpace(ToolChdman) && File.Exists(ToolChdman);
        bool has7z = !string.IsNullOrWhiteSpace(Tool7z) && File.Exists(Tool7z);
        bool anyToolSpecified = !string.IsNullOrWhiteSpace(ToolChdman) || !string.IsNullOrWhiteSpace(Tool7z);
        int toolCount = (hasChdman ? 1 : 0) + (has7z ? 1 : 0);
        ToolsStatusLevel = (hasChdman || has7z) ? StatusLevel.Ok
            : (anyToolSpecified || ConvertEnabled) ? StatusLevel.Warning
            : StatusLevel.Missing;
        StatusTools = ToolsStatusLevel == StatusLevel.Ok ? $"{toolCount} Tools gefunden"
            : ToolsStatusLevel == StatusLevel.Warning ? "Tools nicht gefunden" : "Keine Tools";

        // DAT — validate directory exists when specified
        bool datRootValid = !string.IsNullOrWhiteSpace(DatRoot) && Directory.Exists(DatRoot);
        DatStatusLevel = !UseDat ? StatusLevel.Missing
            : datRootValid ? StatusLevel.Ok
            : !string.IsNullOrWhiteSpace(DatRoot) ? StatusLevel.Warning
            : StatusLevel.Warning;
        StatusDat = DatStatusLevel == StatusLevel.Ok ? "DAT aktiv"
            : DatStatusLevel == StatusLevel.Warning ? "DAT-Pfad ungültig" : "DAT deaktiviert";

        // Overall readiness
        ReadyStatusLevel = !hasRoots ? StatusLevel.Blocked
            : ToolsStatusLevel == StatusLevel.Warning ? StatusLevel.Warning
            : StatusLevel.Ok;
        StatusReady = ReadyStatusLevel switch
        {
            StatusLevel.Ok => "Startbereit ✓",
            StatusLevel.Warning => "Startbereit (Warnung) ⚠",
            StatusLevel.Blocked => "Nicht bereit ✗",
            _ => "Status: –"
        };

        // Step indicator with RunState-awareness (UX-009)
        if (IsBusy)
        {
            CurrentStep = 2;
            StepLabel3 = _runState switch
            {
                RunState.Preflight => "Prüfe…",
                RunState.Scanning => "Scanne…",
                RunState.Deduplicating => "Dedupliziere…",
                RunState.Moving => "Verschiebe…",
                RunState.Converting => "Konvertiere…",
                _ => "Läuft…"
            };
        }
        else if (_runState is RunState.Completed or RunState.CompletedDryRun)
        {
            CurrentStep = 3;
            StepLabel3 = _runState == RunState.CompletedDryRun ? "Vorschau fertig" : "Abgeschlossen";
        }
        else
        {
            CurrentStep = hasRoots ? 1 : 0;
            StepLabel3 = "F5 drücken";
        }
    }

    // ═══ COMMAND HANDLERS ═══════════════════════════════════════════════

    private void OnRun()
    {
        CurrentRunState = RunState.Preflight;
        BusyHint = DryRun ? "DryRun läuft…" : "Move läuft…";
        DashMode = DryRun ? "DryRun" : "Move";
        Progress = 0;
        ProgressText = "0%";
        PerfPhase = "Phase: –";
        PerfFile = "Datei: –";
        ShowMoveCompleteBanner = false;

        // Actual run orchestration is wired in MainWindow.xaml.cs
        // via the RunRequested event
        RunRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancel()
    {
        var cts = Volatile.Read(ref _cts);
        try { cts?.Cancel(); } catch (ObjectDisposedException) { }
        CurrentRunState = RunState.Cancelled;
        BusyHint = "Abbruch angefordert…";
    }

    private async void OnRollback()
    {
        if (!_dialog.Confirm("Letzten Lauf rückgängig machen?", "Rollback bestätigen"))
            return;

        if (string.IsNullOrEmpty(LastAuditPath) || !File.Exists(LastAuditPath))
        {
            AddLog("Keine Audit-Datei gefunden — Rollback nicht möglich.", "WARN");
            return;
        }

        try
        {
            var auditPathCopy = LastAuditPath;
            var roots = Roots.ToList();
            var restored = await Task.Run(() => RollbackService.Execute(auditPathCopy, roots));
            AddLog($"Rollback: {restored.Count} Dateien wiederhergestellt.", "INFO");
            CanRollback = false;
            ShowMoveCompleteBanner = false;
        }
        catch (Exception ex)
        {
            AddLog($"Rollback-Fehler: {ex.Message}", "ERROR");
        }
    }

    private void OnAddRoot()
    {
        var folder = _dialog.BrowseFolder("ROM-Ordner auswählen");
        if (folder is not null && !Roots.Contains(folder))
        {
            Roots.Add(folder);
        }
    }

    private void OnRemoveRoot()
    {
        if (SelectedRoot is not null)
            Roots.Remove(SelectedRoot);
    }

    private void OnOpenReport()
    {
        if (!string.IsNullOrEmpty(LastReportPath) && System.IO.File.Exists(LastReportPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = LastReportPath,
                UseShellExecute = true
            });
        }
    }

    private void OnThemeToggle()
    {
        _theme.Toggle();
        OnPropertyChanged(nameof(ThemeToggleText));
    }

    private void OnGameKeyPreview()
    {
        try
        {
            GameKeyPreviewOutput = Core.GameKeys.GameKeyNormalizer.Normalize(GameKeyPreviewInput);
        }
        catch (Exception ex)
        {
            GameKeyPreviewOutput = $"Fehler: {ex.Message}";
        }
    }

    private void OnBrowseToolPath(object? parameter)
    {
        var path = _dialog.BrowseFile("Executable auswählen", "Executables (*.exe)|*.exe|Alle (*.*)|*.*");
        if (path is null) return;
        switch (parameter as string)
        {
            case "Chdman": ToolChdman = path; break;
            case "Dolphin": ToolDolphin = path; break;
            case "7z": Tool7z = path; break;
            case "Psxtract": ToolPsxtract = path; break;
            case "Ciso": ToolCiso = path; break;
        }
    }

    private void OnBrowseFolderPath(object? parameter)
    {
        var path = _dialog.BrowseFolder("Ordner auswählen");
        if (path is null) return;
        switch (parameter as string)
        {
            case "Dat": DatRoot = path; break;
            case "Trash": TrashRoot = path; break;
            case "Audit": AuditRoot = path; break;
            case "Ps3": Ps3DupesRoot = path; break;
        }
    }

    private void OnPresetSafeDryRun()
    {
        DryRun = true;
        ConvertEnabled = false;
        AggressiveJunk = false;
        PreferEU = true; PreferUS = true; PreferJP = true; PreferWORLD = true;
        RefreshStatus();
    }

    private void OnPresetFullSort()
    {
        SortConsole = true;
        PreferEU = true; PreferUS = true; PreferJP = true; PreferWORLD = true;
        RefreshStatus();
    }

    private void OnPresetConvert()
    {
        DryRun = true;
        ConvertEnabled = true;
        RefreshStatus();
    }

    /// <summary>Complete a run (call from UI thread when orchestration finishes).</summary>
    public void CompleteRun(bool success, string? reportPath = null)
    {
        BusyHint = "";
        if (reportPath is not null)
            LastReportPath = reportPath;
        if (success && DryRun)
        {
            CurrentRunState = RunState.CompletedDryRun;
        }
        else if (success && !DryRun)
        {
            CurrentRunState = RunState.Completed;
            CanRollback = true;
            ShowMoveCompleteBanner = true;
        }
        else
        {
            CurrentRunState = RunState.Failed;
        }
        RefreshStatus();
    }

    /// <summary>Set up a new CancellationTokenSource for a run.</summary>
    public CancellationToken CreateRunCancellation()
    {
        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _cts, newCts);
        try { oldCts?.Dispose(); } catch (ObjectDisposedException) { }
        return newCts.Token;
    }

    /// <summary>Transition to a new run phase (call from code-behind during orchestration).</summary>
    public void TransitionTo(RunState newState)
    {
        CurrentRunState = newState;
    }

    private void OnSaveSettings()
    {
        if (_settings.SaveFrom(this, LastAuditPath))
            AddLog("Einstellungen gespeichert.", "INFO");
        else
            AddLog("Einstellungen konnten nicht gespeichert werden.", "ERROR");
    }

    private void OnLoadSettings()
    {
        _settings.LoadInto(this);
        RefreshStatus();
        AddLog("Einstellungen geladen.", "INFO");
    }

    /// <summary>Load settings into VM on startup (called from code-behind OnLoaded).</summary>
    public void LoadInitialSettings()
    {
        _settings.LoadInto(this);
        LastAuditPath = _settings.LastAuditPath;
        RefreshStatus();
    }

    /// <summary>Save settings (called from code-behind on close / timer).</summary>
    public void SaveSettings() => _settings.SaveFrom(this, LastAuditPath);

    // ═══ RUN PIPELINE EXECUTION ═════════════════════════════════════════

    /// <summary>Execute the full run pipeline (scan, dedupe, sort, convert, move).
    /// Called from code-behind after RunRequested event fires.</summary>
    public async Task ExecuteRunAsync()
    {
        if (!DryRun && ConfirmMove && !ConfirmMoveDialog())
        {
            CurrentRunState = RunState.Idle;
            return;
        }

        var ct = CreateRunCancellation();
        try
        {
            AddLog("Initialisierung…", "INFO");

            var (orchestrator, runOptions, auditPath, reportPath) = await Task.Run(() =>
            {
                DateTime lastProgressUpdate = DateTime.MinValue;
                return RunService.BuildOrchestrator(this, msg =>
                {
                    var now = DateTime.UtcNow;
                    if ((now - lastProgressUpdate).TotalMilliseconds < 100) return;
                    lastProgressUpdate = now;
                    _syncContext?.Post(_ =>
                    {
                        ProgressText = msg;
                        if (msg.StartsWith("[") && msg.Contains(']'))
                        {
                            var phase = msg[..(msg.IndexOf(']') + 1)];
                            PerfPhase = $"Phase: {phase}";
                            var rest = msg[(msg.IndexOf(']') + 1)..].Trim();
                            if (rest.Length > 0) PerfFile = $"Datei: {rest}";
                        }
                        AddLog(msg, "INFO");
                    }, null);
                });
            }, ct);

            var svcResult = await Task.Run(
                () => RunService.ExecuteRun(orchestrator, runOptions, auditPath, reportPath, ct), ct);

            ApplyRunResult(svcResult.Result);
            LastAuditPath = auditPath;

            if (!DryRun && auditPath is not null && File.Exists(auditPath))
                PushRollbackUndo(auditPath);

            if (svcResult.ReportPath is not null)
                AddLog($"Report: {svcResult.ReportPath}", "INFO");

            if (!ct.IsCancellationRequested)
            {
                AddLog("Lauf abgeschlossen.", "INFO");
                CompleteRun(true, reportPath);
                PopulateErrorSummary();
            }
            else
            {
                AddLog("Lauf abgebrochen.", "WARN");
                CompleteRun(false);
            }
        }
        catch (OperationCanceledException)
        {
            AddLog("Lauf abgebrochen.", "WARN");
            CompleteRun(false);
        }
        catch (Exception ex)
        {
            AddLog($"Fehler: {ex.Message}", "ERROR");
            CompleteRun(false);
        }
        finally
        {
            _watchService.FlushPendingIfNeeded();
        }
    }

    // ═══ WATCH-MODE ════════════════════════════════════════════════════

    private void ToggleWatchMode()
    {
        if (Roots.Count == 0)
        { AddLog("Keine Root-Ordner für Watch-Mode.", "WARN"); return; }

        _watchService.IsBusyCheck = () => IsBusy;

        var count = _watchService.Start(Roots);
        if (count == 0)
        {
            AddLog("Watch-Mode deaktiviert.", "INFO");
            _dialog.Info("Watch-Mode wurde deaktiviert.\n\nDateiüberwachung gestoppt.", "Watch-Mode");
        }
        else
        {
            AddLog($"Watch-Mode aktiviert für {count} Ordner. Änderungen werden überwacht.", "INFO");
            _dialog.Info($"Watch-Mode ist aktiv!\n\nÜberwachte Ordner:\n{string.Join("\n", Roots)}\n\nBei Dateiänderungen wird automatisch ein DryRun gestartet.\n\nErneut klicken zum Deaktivieren.",
                "Watch-Mode");
        }
    }

    private void OnWatchRunTriggered()
    {
        if (Roots.Count > 0)
        {
            AddLog("Watch-Mode: Änderungen erkannt, starte DryRun…", "INFO");
            DryRun = true;
            RunCommand.Execute(null);
        }
    }

    /// <summary>Dispose watch-mode resources. Called from code-behind on window close.</summary>
    public void CleanupWatchers()
    {
        _watchService.RunTriggered -= OnWatchRunTriggered;
        _watchService.Dispose();
    }

    // ═══ EVENTS (for code-behind orchestration wiring) ══════════════════
    public event EventHandler? RunRequested;

    /// <summary>
    /// Build the error summary items for the protocol tab.
    /// Extracted from MainWindow.xaml.cs PopulateErrorSummary.
    /// </summary>
    public void PopulateErrorSummary()
    {
        ErrorSummaryItems.Clear();

        var issues = LogEntries
            .Where(e => e.Level is "WARN" or "ERROR")
            .Select(e => $"[{e.Level}] {e.Text}")
            .ToList();

        if (LastRunResult is not null)
        {
            if (LastRunResult.Status == "blocked")
                issues.Insert(0, $"[BLOCKED] Preflight: {LastRunResult.Preflight?.Reason}");

            if (LastRunResult.MoveResult is { FailCount: > 0 } mv)
                issues.Insert(0, $"[ERROR] {mv.FailCount} Dateien konnten nicht verschoben werden");

            var junk = LastCandidates.Count(c => c.Category == "JUNK");
            if (junk > 0)
                issues.Insert(0, $"[WARN] {junk} Junk-Dateien erkannt");

            var unverified = LastCandidates.Count(c => !c.DatMatch);
            if (unverified > 0 && LastCandidates.Count > 0)
                issues.Insert(0, $"[INFO] {unverified}/{LastCandidates.Count} Dateien ohne DAT-Verifizierung");
        }

        if (issues.Count == 0)
        {
            ErrorSummaryItems.Add("✓ Keine Fehler oder Warnungen.");
            if (LastRunResult is not null)
                ErrorSummaryItems.Add($"Report geladen: {LastRunResult.WinnerCount} Winner, {LastRunResult.LoserCount} Dupes");
            return;
        }

        foreach (var issue in issues.Take(50))
            ErrorSummaryItems.Add(issue);
        if (issues.Count > 50)
            ErrorSummaryItems.Add($"… und {issues.Count - 50} weitere");
    }

    /// <summary>Apply run results from orchestrator to all dashboard/state properties.</summary>
    public void ApplyRunResult(RunResult result)
    {
        LastRunResult = result;
        LastCandidates = result.AllCandidates;
        LastDedupeGroups = result.DedupeGroups;

        Progress = 100;
        DashWinners = result.WinnerCount.ToString();
        DashDupes = result.LoserCount.ToString();
        var junkCount = result.AllCandidates.Count(c => c.Category == "JUNK");
        DashJunk = junkCount.ToString();
        DashDuration = $"{result.DurationMs / 1000.0:F1}s";
        var total = result.AllCandidates.Count;
        HealthScore = total > 0 ? $"{100.0 * result.WinnerCount / total:F0}%" : "–";

        if (result.Status == "blocked")
        {
            AddLog($"Preflight blockiert: {result.Preflight?.Reason}", "ERROR");
        }
        else
        {
            AddLog($"Scan: {result.TotalFilesScanned} Dateien", "INFO");
            AddLog($"Dedupe: Keep={result.WinnerCount}, Move={result.LoserCount}, Junk={junkCount}", "INFO");
            if (result.MoveResult is { } mv)
                AddLog($"Verschoben: {mv.MoveCount}, Fehler: {mv.FailCount}", mv.FailCount > 0 ? "WARN" : "INFO");
            if (result.ConvertedCount > 0)
                AddLog($"Konvertiert: {result.ConvertedCount}", "INFO");
        }
    }

    /// <summary>Build a flat key-value config map from current VM state (for diff/export).</summary>
    public Dictionary<string, string> GetCurrentConfigMap()
    {
        return new Dictionary<string, string>
        {
            ["sortConsole"] = SortConsole.ToString(),
            ["aliasKeying"] = AliasKeying.ToString(),
            ["aggressiveJunk"] = AggressiveJunk.ToString(),
            ["dryRun"] = DryRun.ToString(),
            ["useDat"] = UseDat.ToString(),
            ["datRoot"] = DatRoot ?? "",
            ["datHashType"] = DatHashType ?? "SHA1",
            ["convertEnabled"] = ConvertEnabled.ToString(),
            ["trashRoot"] = TrashRoot ?? "",
            ["auditRoot"] = AuditRoot ?? "",
            ["toolChdman"] = ToolChdman ?? "",
            ["toolDolphin"] = ToolDolphin ?? "",
            ["tool7z"] = Tool7z ?? "",
            ["locale"] = Locale ?? "de",
            ["logLevel"] = LogLevel ?? "Info"
        };
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
