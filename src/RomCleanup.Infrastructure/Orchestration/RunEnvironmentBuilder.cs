using System.Text.Json;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Core.Classification;
using RomCleanup.Core.Conversion;
using RomCleanup.Infrastructure.Audit;
using RomCleanup.Infrastructure.Configuration;
using RomCleanup.Infrastructure.Conversion;
using RomCleanup.Infrastructure.Conversion.ToolInvokers;
using RomCleanup.Infrastructure.Dat;
using RomCleanup.Infrastructure.FileSystem;
using RomCleanup.Infrastructure.Hashing;
using RomCleanup.Infrastructure.Logging;
using RomCleanup.Infrastructure.Tools;

namespace RomCleanup.Infrastructure.Orchestration;

/// <summary>
/// Builds RunEnvironment from RunOptions + settings.
/// Shared setup for CLI, API, WPF.
/// ADR-008 §C-04.
/// </summary>
public sealed class RunEnvironmentBuilder
{
    /// <summary>
    /// Try to resolve the data/ directory without throwing.
    /// Returns null when no candidate directory exists.
    /// </summary>
    public static string? TryResolveDataDir()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"),
            Path.Combine(Directory.GetCurrentDirectory(), "data")
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
                return full;
        }

        return null;
    }

    /// <summary>
    /// Resolve the data/ directory by searching multiple candidate locations.
    /// Priority: next to executable → workspace root → current working directory.
    /// </summary>
    public static string ResolveDataDir()
    {
        var resolved = TryResolveDataDir();
        if (resolved is not null)
            return resolved;

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"),
            Path.Combine(Directory.GetCurrentDirectory(), "data")
        };

        throw new DirectoryNotFoundException(
            "Could not locate required data directory. Checked: " +
            string.Join(", ", candidates.Select(Path.GetFullPath)));
    }

    /// <summary>Load settings from defaults.json → user settings.</summary>
    public static RomCleanupSettings LoadSettings(string dataDir)
    {
        var defaultsPath = Path.Combine(dataDir, "defaults.json");
        return SettingsLoader.Load(File.Exists(defaultsPath) ? defaultsPath : null);
    }

    /// <summary>
    /// Build complete environment for a run.
    /// </summary>
    public static RunEnvironment Build(RunOptions runOptions, RomCleanupSettings settings,
        string dataDir, Action<string>? onWarning = null)
    {
        var fs = new FileSystemAdapter();
        var audit = new AuditCsvStore(fs, onWarning ?? (_ => { }),
            AuditSecurityPaths.GetDefaultSigningKeyPath());

        // ToolRunner
        var toolHashesPath = Path.Combine(dataDir, "tool-hashes.json");
        var toolRunner = new ToolRunnerAdapter(File.Exists(toolHashesPath) ? toolHashesPath : null);
        var consolesJsonPath = Path.Combine(dataDir, "consoles.json");

        // FormatConverter
        FormatConverterAdapter? converter = null;
        if (runOptions.ConvertFormat != null)
        {
            IConversionRegistry? conversionRegistry = null;
            IConversionPlanner? conversionPlanner = null;
            IConversionExecutor? conversionExecutor = null;

            try
            {
                var conversionRegistryPath = Path.Combine(dataDir, "conversion-registry.json");
                if (File.Exists(conversionRegistryPath) && File.Exists(consolesJsonPath))
                {
                    conversionRegistry = new ConversionRegistryLoader(conversionRegistryPath, consolesJsonPath);
                    var invokers = new IToolInvoker[]
                    {
                        new ChdmanInvoker(toolRunner),
                        new DolphinToolInvoker(toolRunner),
                        new SevenZipInvoker(toolRunner),
                        new PsxtractInvoker(toolRunner)
                    };
                    conversionExecutor = new ConversionExecutor(invokers);

                    conversionPlanner = new ConversionPlanner(
                        conversionRegistry,
                        toolRunner.FindTool,
                        path => new FileInfo(path).Length);
                }
            }
            catch (Exception ex)
            {
                onWarning?.Invoke($"[Warning] Conversion registry loading failed, fallback to legacy mapping: {ex.Message}");
            }

            converter = new FormatConverterAdapter(toolRunner, null, conversionRegistry, conversionPlanner, conversionExecutor);
        }

        // ArchiveHashService: enables DAT matching and console detection for ROMs inside ZIP/7z archives
        // Created early so ConsoleDetector can use it for 7z inner-extension detection.
        var archiveHashService = new ArchiveHashService(toolRunner);

        // ConsoleDetector
        ConsoleDetector? consoleDetector = null;
        var discHeaderDetector = new DiscHeaderDetector();
        var cartridgeHeaderDetector = new CartridgeHeaderDetector();
        if (File.Exists(consolesJsonPath))
        {
            var consolesJson = File.ReadAllText(consolesJsonPath);
            consoleDetector = ConsoleDetector.LoadFromJson(
                consolesJson,
                discHeaderDetector,
                archiveEntryProvider: archiveHashService.GetArchiveEntryNames,
                cartridgeHeaderDetector: cartridgeHeaderDetector);
        }
        else if (runOptions.SortConsole || runOptions.EnableDat)
        {
            onWarning?.Invoke("[Warning] consoles.json not found, --SortConsole/--EnableDat require it");
        }

        // DAT
        DatIndex? datIndex = null;
        FileHashService? hashService = null;
        var effectiveDatRoot = !string.IsNullOrWhiteSpace(runOptions.DatRoot)
            ? runOptions.DatRoot
            : settings.Dat.DatRoot;

        if (runOptions.EnableDat && !string.IsNullOrWhiteSpace(effectiveDatRoot) && Directory.Exists(effectiveDatRoot))
        {
            var datRepo = new DatRepositoryAdapter();
            hashService = new FileHashService();
            var consoleMap = BuildConsoleMap(dataDir, effectiveDatRoot);
            if (consoleMap.Count > 0)
            {
                datIndex = datRepo.GetDatIndex(effectiveDatRoot, consoleMap,
                    runOptions.HashType ?? settings.Dat.HashType);
                onWarning?.Invoke($"[DAT] Loaded {datIndex.TotalEntries} hashes for {datIndex.ConsoleCount} consoles");
            }
            else
            {
                onWarning?.Invoke("[Warning] No DAT files mapped — check dat-catalog.json and DatRoot");
            }
        }
        else if (runOptions.EnableDat)
        {
            onWarning?.Invoke("[Warning] DAT enabled but DatRoot not set or not found");
        }

        return new RunEnvironment(fs, audit, consoleDetector, hashService, converter, datIndex, archiveHashService);
    }

    /// <summary>
    /// Build a console→DAT-filename map from dat-catalog.json.
    /// Falls back to scanning datRoot for .dat files with console key as stem.
    /// </summary>
    public static Dictionary<string, string> BuildConsoleMap(string dataDir, string datRoot)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var catalogPath = Path.Combine(dataDir, "dat-catalog.json");
        if (File.Exists(catalogPath))
        {
            try
            {
                var json = File.ReadAllText(catalogPath);
                var entries = JsonSerializer.Deserialize<List<DatCatalogEntry>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.ConsoleKey))
                            continue;

                        var candidates = new[]
                        {
                            Path.Combine(datRoot, entry.Id + ".dat"),
                            Path.Combine(datRoot, entry.System + ".dat"),
                            Path.Combine(datRoot, entry.ConsoleKey + ".dat")
                        };

                        foreach (var candidate in candidates)
                        {
                            if (File.Exists(candidate))
                            {
                                map[entry.ConsoleKey] = candidate;
                                break;
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed catalog — fall through to directory scan
            }
        }

        if (Directory.Exists(datRoot))
        {
            foreach (var datFile in Directory.GetFiles(datRoot, "*.dat"))
            {
                var stem = Path.GetFileNameWithoutExtension(datFile).ToUpperInvariant();
                if (!map.ContainsKey(stem))
                    map[stem] = datFile;
            }
        }

        return map;
    }

    public sealed class DatCatalogEntry
    {
        public string Group { get; set; } = "";
        public string System { get; set; } = "";
        public string Id { get; set; } = "";
        public string ConsoleKey { get; set; } = "";
    }
}

/// <summary>
/// All runtime dependencies needed to execute a run.
/// </summary>
public sealed class RunEnvironment
    : IRunEnvironment
{
    public IFileSystem FileSystem { get; }
    public IAuditStore AuditStore { get; }
    public AuditCsvStore Audit => (AuditCsvStore)AuditStore;
    public ConsoleDetector? ConsoleDetector { get; }
    public FileHashService? HashService { get; }
    public ArchiveHashService? ArchiveHashService { get; }
    public IFormatConverter? Converter { get; }
    public DatIndex? DatIndex { get; }

    public RunEnvironment(FileSystemAdapter fileSystem, AuditCsvStore audit,
        ConsoleDetector? consoleDetector, FileHashService? hashService,
        FormatConverterAdapter? converter, DatIndex? datIndex,
        ArchiveHashService? archiveHashService = null)
    {
        FileSystem = fileSystem;
        AuditStore = audit;
        ConsoleDetector = consoleDetector;
        HashService = hashService;
        ArchiveHashService = archiveHashService;
        Converter = converter;
        DatIndex = datIndex;
    }
}
