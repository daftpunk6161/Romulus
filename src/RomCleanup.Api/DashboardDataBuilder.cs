using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Analysis;
using RomCleanup.Infrastructure.Dat;
using RomCleanup.Infrastructure.Index;
using RomCleanup.Infrastructure.Orchestration;
using RomCleanup.Infrastructure.Profiles;
using RomCleanup.Infrastructure.Safety;
using RomCleanup.Infrastructure.Workflow;

namespace RomCleanup.Api;

internal static class DashboardDataBuilder
{
    public static DashboardBootstrapResponse BuildBootstrap(
        HeadlessApiOptions options,
        AllowedRootPathPolicy allowedRootPolicy,
        string version)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(allowedRootPolicy);

        return new DashboardBootstrapResponse
        {
            Version = version,
            DashboardEnabled = options.DashboardEnabled,
            AllowRemoteClients = options.AllowRemoteClients,
            AllowedRootsEnforced = allowedRootPolicy.IsEnforced,
            AllowedRoots = allowedRootPolicy.AllowedRoots.ToArray(),
            PublicBaseUrl = options.PublicBaseUrl
        };
    }

    public static async Task<DashboardSummaryResponse> BuildSummaryAsync(
        RunLifecycleManager lifecycleManager,
        ApiAutomationService automationService,
        ICollectionIndex collectionIndex,
        RunProfileService profileService,
        AllowedRootPathPolicy allowedRootPolicy,
        string version,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lifecycleManager);
        ArgumentNullException.ThrowIfNull(automationService);
        ArgumentNullException.ThrowIfNull(collectionIndex);
        ArgumentNullException.ThrowIfNull(profileService);
        ArgumentNullException.ThrowIfNull(allowedRootPolicy);

        var activeRun = lifecycleManager.GetActive();
        var snapshots = await collectionIndex.ListRunSnapshotsAsync(10, ct).ConfigureAwait(false);
        var trends = await RunHistoryInsightsService.BuildStorageInsightsAsync(collectionIndex, 30, ct).ConfigureAwait(false);
        var profiles = await profileService.ListAsync(ct).ConfigureAwait(false);
        var workflows = WorkflowScenarioCatalog.List();
        var datStatus = await BuildDatStatusAsync(allowedRootPolicy, ct).ConfigureAwait(false);

        return new DashboardSummaryResponse
        {
            Version = version,
            HasActiveRun = activeRun is not null,
            ActiveRun = activeRun?.ToDto(),
            WatchStatus = automationService.GetStatus(),
            DatStatus = datStatus,
            Trends = trends,
            RecentRuns = CollectionRunHistoryPageBuilder.Build(snapshots, snapshots.Count, 0, 10)
                .Runs
                .Select(MapRunHistoryEntry)
                .ToArray(),
            Profiles = profiles.ToArray(),
            Workflows = workflows.ToArray()
        };
    }

    public static Task<DashboardDatStatusResponse> BuildDatStatusAsync(
        AllowedRootPathPolicy allowedRootPolicy,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var dataDir = RunEnvironmentBuilder.TryResolveDataDir()
            ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var settings = RunEnvironmentBuilder.LoadSettings(dataDir);
        var datRoot = settings.Dat?.DatRoot;

        if (string.IsNullOrWhiteSpace(datRoot) || !Directory.Exists(datRoot))
        {
            return Task.FromResult(new DashboardDatStatusResponse
            {
                Configured = false,
                DatRoot = datRoot ?? string.Empty,
                Message = "DatRoot is not configured or does not exist.",
                TotalFiles = 0,
                Consoles = Array.Empty<DashboardDatConsoleStatus>(),
                OldFileCount = 0,
                CatalogEntries = 0,
                WithinAllowedRoots = string.IsNullOrWhiteSpace(datRoot) || allowedRootPolicy.IsPathAllowed(datRoot)
            });
        }

        var datFiles = Directory.GetFiles(datRoot, "*.dat", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(datRoot, "*.xml", SearchOption.AllDirectories))
            .ToArray();

        var consoleStats = datFiles
            .GroupBy(file =>
            {
                var dir = Path.GetDirectoryName(file);
                return dir is not null && !string.Equals(Path.GetFullPath(dir), Path.GetFullPath(datRoot), StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileName(dir)
                    : "root";
            }, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DashboardDatConsoleStatus
            {
                Console = group.Key,
                FileCount = group.Count(),
                NewestFileUtc = group.Max(File.GetLastWriteTimeUtc).ToString("o"),
                OldestFileUtc = group.Min(File.GetLastWriteTimeUtc).ToString("o")
            })
            .OrderBy(static item => item.Console, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var oldFiles = datFiles.Where(file => (DateTime.UtcNow - File.GetLastWriteTimeUtc(file)).TotalDays > 180).ToArray();
        var catalogPath = Path.Combine(dataDir, "dat-catalog.json");
        var catalogEntries = 0;
        if (File.Exists(catalogPath))
        {
            try
            {
                catalogEntries = DatSourceService.LoadCatalog(catalogPath).Count;
            }
            catch
            {
                // status endpoint must stay best-effort
            }
        }

        return Task.FromResult(new DashboardDatStatusResponse
        {
            Configured = true,
            DatRoot = datRoot,
            TotalFiles = datFiles.Length,
            Consoles = consoleStats,
            OldFileCount = oldFiles.Length,
            CatalogEntries = catalogEntries,
            StaleWarning = oldFiles.Length > 0
                ? $"{oldFiles.Length} DAT files are older than 6 months"
                : null,
            WithinAllowedRoots = allowedRootPolicy.IsPathAllowed(datRoot)
        });
    }

    private static ApiRunHistoryEntry MapRunHistoryEntry(CollectionRunHistoryItem item)
        => new()
        {
            RunId = item.RunId,
            StartedUtc = item.StartedUtc,
            CompletedUtc = item.CompletedUtc,
            Mode = item.Mode,
            Status = item.Status,
            RootCount = item.RootCount,
            RootFingerprint = item.RootFingerprint,
            DurationMs = item.DurationMs,
            TotalFiles = item.TotalFiles,
            CollectionSizeBytes = item.CollectionSizeBytes,
            Games = item.Games,
            Dupes = item.Dupes,
            Junk = item.Junk,
            DatMatches = item.DatMatches,
            ConvertedCount = item.ConvertedCount,
            FailCount = item.FailCount,
            SavedBytes = item.SavedBytes,
            ConvertSavedBytes = item.ConvertSavedBytes,
            HealthScore = item.HealthScore
        };
}
