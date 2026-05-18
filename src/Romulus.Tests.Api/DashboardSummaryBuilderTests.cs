using Romulus.Api;
using Romulus.Contracts;
using Romulus.Contracts.Models;
using Romulus.Contracts.Ports;
using Romulus.Infrastructure.Audit;
using Romulus.Infrastructure.FileSystem;
using Romulus.Infrastructure.Profiles;
using Romulus.Infrastructure.Safety;
using Xunit;

namespace Romulus.Tests;

public sealed class DashboardSummaryBuilderTests
{
    [Fact]
    public async Task BuildSummaryAsync_HidesForeignActiveRunAndSnapshots()
    {
        var root = CreateTempDirectory();
        using var executorStarted = new ManualResetEventSlim(false);
        using var allowCompletion = new ManualResetEventSlim(false);
        using var manager = new RunManager(new FileSystemAdapter(), new AuditCsvStore(), (_, _, _, ct) =>
        {
            executorStarted.Set();
            allowCompletion.Wait(ct);
            return CompletedOutcome();
        });
        using var automation = new ApiAutomationService(manager);

        try
        {
            var active = manager.TryCreateOrReuse(
                new RunRequest { Roots = new[] { root } },
                RunConstants.ModeDryRun,
                ownerClientId: "owner-a").Run!;
            Assert.True(executorStarted.Wait(TimeSpan.FromSeconds(5)));

            var index = new SnapshotOnlyCollectionIndex(
                Snapshot("run-owner-a", "owner-a", startedDay: 1, totalFiles: 10, healthScore: 90),
                Snapshot("run-owner-b", "owner-b", startedDay: 2, totalFiles: 20, healthScore: 80));

            var summary = await DashboardDataBuilder.BuildSummaryAsync(
                manager.Lifecycle,
                automation,
                index,
                CreateProfileService(),
                new AllowedRootPathPolicy(null),
                requesterClientId: "owner-b",
                version: "test-version",
                CancellationToken.None);

            Assert.False(summary.HasActiveRun);
            Assert.Null(summary.ActiveRun);
            Assert.Single(summary.RecentRuns);
            Assert.Equal("run-owner-b", summary.RecentRuns[0].RunId);
            Assert.Equal(20, summary.RecentRuns[0].TotalFiles);
            Assert.Equal(1, summary.Trends.SampleCount);
            Assert.NotEqual(active.RunId, summary.RecentRuns[0].RunId);
        }
        finally
        {
            allowCompletion.Set();
            if (manager.GetActive() is { } activeRun)
                await manager.WaitForCompletion(activeRun.RunId, timeout: TimeSpan.FromSeconds(5));

            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task BuildSummaryAsync_ProjectsVisibleActiveRunProfilesWorkflowsAndHistory()
    {
        var root = CreateTempDirectory();
        using var executorStarted = new ManualResetEventSlim(false);
        using var allowCompletion = new ManualResetEventSlim(false);
        using var manager = new RunManager(new FileSystemAdapter(), new AuditCsvStore(), (_, _, _, ct) =>
        {
            executorStarted.Set();
            allowCompletion.Wait(ct);
            return CompletedOutcome();
        });
        using var automation = new ApiAutomationService(manager);

        try
        {
            var active = manager.TryCreateOrReuse(
                new RunRequest
                {
                    Roots = new[] { root },
                    EnableDat = true,
                    SortConsole = true
                },
                RunConstants.ModeMove,
                ownerClientId: "owner-a").Run!;
            Assert.True(executorStarted.Wait(TimeSpan.FromSeconds(5)));

            var index = new SnapshotOnlyCollectionIndex(
                Snapshot("newer", "owner-a", startedDay: 3, totalFiles: 30, healthScore: 70),
                Snapshot("older", "owner-a", startedDay: 1, totalFiles: 10, healthScore: 95));

            var profileService = CreateProfileService(new RunProfileDocument
            {
                Id = "safe-cleanup",
                Name = "Safe Cleanup",
                Description = "Audit-first cleanup",
                Tags = ["audit"],
                WorkflowScenarioId = "full-audit"
            });

            var summary = await DashboardDataBuilder.BuildSummaryAsync(
                manager.Lifecycle,
                automation,
                index,
                profileService,
                new AllowedRootPathPolicy(null),
                requesterClientId: "owner-a",
                version: "2026.05",
                CancellationToken.None);

            Assert.True(summary.HasActiveRun);
            Assert.Equal(active.RunId, summary.ActiveRun!.RunId);
            Assert.Equal(RunConstants.ModeMove, summary.ActiveRun.Mode);
            Assert.True(summary.ActiveRun.EnableDat);
            Assert.True(summary.ActiveRun.SortConsole);
            Assert.NotNull(summary.WatchStatus);
            Assert.NotNull(summary.DatStatus);
            Assert.Contains(summary.Profiles, profile => profile.Id == "safe-cleanup" && profile.WorkflowScenarioId == "full-audit");
            Assert.NotEmpty(summary.Workflows);
            Assert.Equal(2, summary.RecentRuns.Length);
            Assert.Equal("newer", summary.RecentRuns[0].RunId);
            Assert.Equal(30, summary.RecentRuns[0].TotalFiles);
            Assert.Equal(3000, summary.RecentRuns[0].CollectionSizeBytes);
            Assert.Equal(7, summary.RecentRuns[0].Games);
            Assert.Equal(3, summary.RecentRuns[0].Dupes);
            Assert.Equal(70, summary.RecentRuns[0].HealthScore);
            Assert.Equal(2, summary.Trends.SampleCount);
        }
        finally
        {
            allowCompletion.Set();
            if (manager.GetActive() is { } activeRun)
                await manager.WaitForCompletion(activeRun.RunId, timeout: TimeSpan.FromSeconds(5));

            DeleteDirectory(root);
        }
    }

    private static RunProfileService CreateProfileService(params RunProfileDocument[] profiles)
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "Romulus_DashboardSummary_Profile_" + Guid.NewGuid().ToString("N"));
        return new RunProfileService(new InMemoryRunProfileStore(profiles), dataDir);
    }

    private static CollectionRunSnapshot Snapshot(
        string runId,
        string ownerClientId,
        int startedDay,
        int totalFiles,
        int healthScore)
        => new()
        {
            RunId = runId,
            OwnerClientId = ownerClientId,
            StartedUtc = new DateTime(2026, 5, startedDay, 10, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 5, startedDay, 10, 1, 0, DateTimeKind.Utc),
            Mode = RunConstants.ModeDryRun,
            Status = RunConstants.StatusCompleted,
            Roots = [@"C:\Roms"],
            RootFingerprint = $"{ownerClientId}-{runId}",
            DurationMs = 60_000,
            TotalFiles = totalFiles,
            CollectionSizeBytes = totalFiles * 100L,
            Games = 7,
            Dupes = 3,
            Junk = 2,
            DatMatches = 5,
            ConvertedCount = 1,
            FailCount = 0,
            SavedBytes = 123,
            ConvertSavedBytes = 45,
            HealthScore = healthScore
        };

    private static RunExecutionOutcome CompletedOutcome()
        => new(RunConstants.StatusCompleted, new ApiRunResult
        {
            OrchestratorStatus = "ok",
            ExitCode = 0
        });

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Romulus_DashboardSummary_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort cleanup for test-owned temporary directories.
        }
    }

    private sealed class SnapshotOnlyCollectionIndex(params CollectionRunSnapshot[] snapshots) : ICollectionIndex
    {
        public ValueTask<CollectionIndexMetadata> GetMetadataAsync(CancellationToken ct = default)
            => ValueTask.FromResult(new CollectionIndexMetadata());

        public ValueTask<int> CountEntriesAsync(CancellationToken ct = default)
            => ValueTask.FromResult(0);

        public ValueTask<CollectionIndexEntry?> TryGetByPathAsync(string path, CancellationToken ct = default)
            => ValueTask.FromResult<CollectionIndexEntry?>(null);

        public ValueTask<IReadOnlyList<CollectionIndexEntry>> GetByPathsAsync(IReadOnlyList<string> paths, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<CollectionIndexEntry>>(Array.Empty<CollectionIndexEntry>());

        public ValueTask<IReadOnlyList<CollectionIndexEntry>> ListByConsoleAsync(string consoleKey, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<CollectionIndexEntry>>(Array.Empty<CollectionIndexEntry>());

        public ValueTask<IReadOnlyList<CollectionIndexEntry>> ListEntriesInScopeAsync(
            IReadOnlyList<string> roots,
            IReadOnlyCollection<string> extensions,
            CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<CollectionIndexEntry>>(Array.Empty<CollectionIndexEntry>());

        public ValueTask UpsertEntriesAsync(IReadOnlyList<CollectionIndexEntry> entries, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask RemovePathsAsync(IReadOnlyList<string> paths, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<CollectionHashCacheEntry?> TryGetHashAsync(
            string path,
            string algorithm,
            long sizeBytes,
            DateTime lastWriteUtc,
            CancellationToken ct = default)
            => ValueTask.FromResult<CollectionHashCacheEntry?>(null);

        public ValueTask SetHashAsync(CollectionHashCacheEntry entry, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask AppendRunSnapshotAsync(CollectionRunSnapshot snapshot, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<int> CountRunSnapshotsAsync(CancellationToken ct = default)
            => ValueTask.FromResult(snapshots.Length);

        public ValueTask<IReadOnlyList<CollectionRunSnapshot>> ListRunSnapshotsAsync(int limit = 50, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<CollectionRunSnapshot>>(
                snapshots
                    .OrderByDescending(snapshot => snapshot.CompletedUtc)
                    .ThenBy(snapshot => snapshot.RunId, StringComparer.Ordinal)
                    .Take(limit)
                    .ToArray());
    }

    private sealed class InMemoryRunProfileStore(IReadOnlyList<RunProfileDocument> profiles) : IRunProfileStore
    {
        public ValueTask<IReadOnlyList<RunProfileDocument>> ListAsync(CancellationToken ct = default)
            => ValueTask.FromResult(profiles);

        public ValueTask<RunProfileDocument?> TryGetAsync(string id, CancellationToken ct = default)
            => ValueTask.FromResult(profiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase)));

        public ValueTask UpsertAsync(RunProfileDocument profile, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<bool> DeleteAsync(string id, CancellationToken ct = default)
            => ValueTask.FromResult(false);
    }
}
