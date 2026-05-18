using Romulus.Contracts.Models;
using Romulus.Contracts.Ports;
using Romulus.Infrastructure.Analysis;
using Romulus.Infrastructure.Watch;
using System.Reflection;
using Xunit;

namespace Romulus.Tests;

public sealed class AutomationAndTrendServiceTests : IDisposable
{
    private readonly string _tempDir;

    public AutomationAndTrendServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Romulus_Automation_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ScheduleService_QueuesPendingWhileBusy_AndFlushesDeterministically()
    {
        var now = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Local);
        using var scheduleService = new ScheduleService(() => now, TimeSpan.FromMilliseconds(25));
        using var triggered = new ManualResetEventSlim(false);
        var busy = true;
        var triggerCount = 0;

        scheduleService.IsBusyCheck = () => busy;
        scheduleService.Triggered += () =>
        {
            Interlocked.Increment(ref triggerCount);
            triggered.Set();
        };

        Assert.True(scheduleService.Start(intervalMinutes: 1));

        now = now.AddMinutes(1);
        Assert.True(SpinWait.SpinUntil(() => scheduleService.HasPending, TimeSpan.FromSeconds(2)));
        Assert.Equal(0, triggerCount);

        busy = false;
        scheduleService.FlushPendingIfNeeded();

        Assert.True(triggered.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, triggerCount);
    }

    [Fact]
    public void ScheduleService_StartStopAndDisposedState_AreDeterministic()
    {
        using var scheduleService = new ScheduleService(() => new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Local));

        Assert.False(scheduleService.Start(intervalMinutes: 0, cronExpression: " "));
        Assert.False(scheduleService.IsActive);
        Assert.Null(scheduleService.IntervalMinutes);
        Assert.Null(scheduleService.CronExpression);

        Assert.True(scheduleService.Start(intervalMinutes: -5, cronExpression: "  */5 * * * *  "));
        Assert.True(scheduleService.IsActive);
        Assert.Null(scheduleService.IntervalMinutes);
        Assert.Equal("*/5 * * * *", scheduleService.CronExpression);

        scheduleService.Stop();

        Assert.False(scheduleService.IsActive);
        Assert.Null(scheduleService.IntervalMinutes);
        Assert.Null(scheduleService.CronExpression);

        scheduleService.Dispose();
        scheduleService.Dispose();
        Assert.Throws<ObjectDisposedException>(() => scheduleService.Start(intervalMinutes: 1));
    }

    [Fact]
    public void ScheduleService_FlushPending_TreatsBusyCheckExceptionAsStillBusy()
    {
        using var scheduleService = new ScheduleService(() => new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Local));
        var triggerCount = 0;
        scheduleService.Triggered += () => Interlocked.Increment(ref triggerCount);

        Assert.True(scheduleService.Start(intervalMinutes: 1));
        scheduleService.MarkPendingWhileBusy();
        scheduleService.IsBusyCheck = () => throw new InvalidOperationException("busy check failed");

        scheduleService.FlushPendingIfNeeded();

        Assert.True(scheduleService.HasPending);
        Assert.Equal(0, triggerCount);
    }

    [Fact]
    public void ScheduleService_ClockProviderFailure_SkipsTickWithoutCorruptingSchedule()
    {
        var now = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Local);
        var throwOnTick = false;
        using var scheduleService = new ScheduleService(() =>
        {
            if (throwOnTick)
                throw new InvalidOperationException("clock unavailable");
            return now;
        }, TimeSpan.FromMilliseconds(25));
        using var triggered = new ManualResetEventSlim(false);
        var triggerCount = 0;
        scheduleService.Triggered += () =>
        {
            Interlocked.Increment(ref triggerCount);
            triggered.Set();
        };

        Assert.True(scheduleService.Start(intervalMinutes: 1));

        now = now.AddMinutes(1);
        throwOnTick = true;
        Thread.Sleep(80);

        Assert.Equal(0, Volatile.Read(ref triggerCount));
        Assert.True(scheduleService.IsActive);

        throwOnTick = false;

        Assert.True(triggered.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, Volatile.Read(ref triggerCount));
    }

    [Fact]
    public void ScheduleService_CronTrigger_FiresOncePerMatchingMinute()
    {
        var now = new DateTime(2026, 4, 1, 10, 5, 0, DateTimeKind.Local);
        using var scheduleService = new ScheduleService(() => now, TimeSpan.FromMilliseconds(25));
        using var firstTriggered = new ManualResetEventSlim(false);
        using var secondTriggered = new ManualResetEventSlim(false);
        var triggerCount = 0;
        scheduleService.Triggered += () =>
        {
            var count = Interlocked.Increment(ref triggerCount);
            if (count == 1)
                firstTriggered.Set();
            if (count == 2)
                secondTriggered.Set();
        };

        Assert.True(scheduleService.Start(cronExpression: "* * * * *"));

        Assert.True(firstTriggered.Wait(TimeSpan.FromSeconds(2)));
        Thread.Sleep(80);
        Assert.Equal(1, Volatile.Read(ref triggerCount));

        now = now.AddMinutes(1);

        Assert.True(secondTriggered.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, Volatile.Read(ref triggerCount));
    }

    [Fact]
    public void WatchFolderService_FileChangeWhileBusy_QueuesPendingAndFlushes()
    {
        var nowUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        using var watchService = new WatchFolderService(() => nowUtc);
        using var triggered = new ManualResetEventSlim(false);
        var busy = true;
        var triggerCount = 0;
        var root = Path.Combine(_tempDir, "watch-root");
        Directory.CreateDirectory(root);

        watchService.IsBusyCheck = () => busy;
        watchService.RunTriggered += () =>
        {
            Interlocked.Increment(ref triggerCount);
            triggered.Set();
        };

        Assert.Equal(1, watchService.Start([root], debounceSeconds: 1, maxWaitSeconds: 1));

        File.WriteAllText(Path.Combine(root, "game.bin"), "data");

        Assert.True(SpinWait.SpinUntil(() => watchService.HasPending, TimeSpan.FromSeconds(4)));
        Assert.Equal(0, triggerCount);

        busy = false;
        nowUtc = nowUtc.AddSeconds(31);
        watchService.FlushPendingIfNeeded();

        Assert.True(triggered.Wait(TimeSpan.FromSeconds(4)));
        Assert.Equal(1, triggerCount);
    }

    [Fact]
    public void WatchFolderService_StartStopInvalidRootsAndDisposedState_AreDeterministic()
    {
        using var watchService = new WatchFolderService(() => new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var errors = new List<string>();
        watchService.WatcherError += errors.Add;
        var root = Path.Combine(_tempDir, "watch-start-stop-root");
        Directory.CreateDirectory(root);

        Assert.Throws<ArgumentNullException>(() => watchService.Start(null!));
        Assert.Equal(1, watchService.Start([" ", root], debounceSeconds: 0, maxWaitSeconds: 0));
        Assert.True(watchService.IsActive);
        Assert.False(watchService.HasPending);

        Assert.Equal(0, watchService.Start([root]));
        Assert.False(watchService.IsActive);

        watchService.MarkPendingWhileBusy();
        watchService.FlushPendingIfNeeded();
        Assert.True(watchService.HasPending);

        watchService.Stop();
        Assert.False(watchService.IsActive);
        Assert.False(watchService.HasPending);

        Assert.Equal(0, watchService.Start([Path.Combine(_tempDir, "missing-root")]));
        Assert.False(watchService.IsActive);
        watchService.Stop();

        Assert.Equal(0, watchService.Start(["bad\0root"]));
        Assert.NotEmpty(errors);

        watchService.Dispose();
        watchService.Dispose();
        Assert.Throws<ObjectDisposedException>(() => watchService.Start([root]));
    }

    [Fact]
    public void WatchFolderService_WatcherErrorRecoversDeletedAndRecreatedRoot()
    {
        using var watchService = new WatchFolderService(() => new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var root = Path.Combine(_tempDir, "watch-recovery-root");
        Directory.CreateDirectory(root);
        var errors = new List<string>();
        watchService.WatcherError += errors.Add;

        Assert.Equal(1, watchService.Start([root]));
        Assert.True(watchService.IsActive);

        Directory.Delete(root, recursive: true);
        InvokePrivate(
            watchService,
            "OnWatcherError",
            watchService,
            new ErrorEventArgs(new IOException("watcher buffer overflow")));

        Assert.False(watchService.IsActive);
        Assert.NotEmpty(errors);

        Directory.CreateDirectory(root);
        InvokePrivate(watchService, "OnRecoveryTimer", (object?)null);

        Assert.True(watchService.IsActive);
    }

    [Fact]
    public void WatchFolderService_WatcherErrorOnHealthyRootReportsErrorAndKeepsWatcherActive()
    {
        using var watchService = new WatchFolderService(() => new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        var root = Path.Combine(_tempDir, "watch-healthy-error-root");
        Directory.CreateDirectory(root);
        var errors = new List<string>();
        watchService.WatcherError += errors.Add;

        Assert.Equal(1, watchService.Start([root]));

        InvokePrivate(
            watchService,
            "OnWatcherError",
            watchService,
            new ErrorEventArgs(new IOException("transient watcher error")));

        Assert.True(watchService.IsActive);
        Assert.Single(errors);
        Assert.False(string.IsNullOrWhiteSpace(errors[0]));
    }

    [Fact]
    public void WatchFolderService_CooldownPending_TriggersFollowupWithoutNewEvent()
    {
        var nowUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        using var watchService = new WatchFolderService(() => nowUtc);
        using var firstTriggered = new ManualResetEventSlim(false);
        using var secondTriggered = new ManualResetEventSlim(false);
        var triggerCount = 0;
        var root = Path.Combine(_tempDir, "watch-cooldown-root");
        Directory.CreateDirectory(root);

        watchService.IsBusyCheck = () => false;
        watchService.RunTriggered += () =>
        {
            var count = Interlocked.Increment(ref triggerCount);
            if (count == 1)
                firstTriggered.Set();
            if (count >= 2)
                secondTriggered.Set();
        };

        Assert.Equal(1, watchService.Start([root], debounceSeconds: 1, maxWaitSeconds: 1));

        File.WriteAllText(Path.Combine(root, "first.bin"), "a");
        Assert.True(firstTriggered.Wait(TimeSpan.FromSeconds(4)));

        // Trigger another change inside cooldown so it must be queued and auto-fired later.
        nowUtc = nowUtc.AddSeconds(29);
        File.WriteAllText(Path.Combine(root, "second.bin"), "b");

        Assert.True(SpinWait.SpinUntil(() => watchService.HasPending, TimeSpan.FromSeconds(4)));
        Assert.Equal(1, Volatile.Read(ref triggerCount));

        // No further file changes: follow-up should fire from cooldown scheduling only.
        nowUtc = nowUtc.AddSeconds(2);
        Assert.True(secondTriggered.Wait(TimeSpan.FromSeconds(4)));
        Assert.True(Volatile.Read(ref triggerCount) >= 2);
    }

    [Fact]
    public async Task RunHistoryTrendService_UsesPersistedCollectionSizeBytes()
    {
        var snapshots = new[]
        {
            new CollectionRunSnapshot
            {
                RunId = "run-1",
                StartedUtc = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                CompletedUtc = new DateTime(2026, 4, 1, 8, 1, 0, DateTimeKind.Utc),
                TotalFiles = 10,
                CollectionSizeBytes = 1024,
                DatMatches = 8,
                Dupes = 2,
                Junk = 1,
                HealthScore = 90,
                SavedBytes = 999999,
                ConvertSavedBytes = 555555
            }
        };

        var history = await RunHistoryTrendService.LoadTrendHistoryAsync(new SnapshotOnlyCollectionIndex(snapshots));

        Assert.Single(history);
        Assert.Equal(1024, history[0].SizeBytes);
    }

    private sealed class SnapshotOnlyCollectionIndex : ICollectionIndex
    {
        private readonly IReadOnlyList<CollectionRunSnapshot> _snapshots;

        public SnapshotOnlyCollectionIndex(IReadOnlyList<CollectionRunSnapshot> snapshots)
        {
            _snapshots = snapshots;
        }

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

        public ValueTask<IReadOnlyList<CollectionIndexEntry>> ListEntriesInScopeAsync(IReadOnlyList<string> roots, IReadOnlyCollection<string> extensions, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<CollectionIndexEntry>>(Array.Empty<CollectionIndexEntry>());

        public ValueTask UpsertEntriesAsync(IReadOnlyList<CollectionIndexEntry> entries, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask RemovePathsAsync(IReadOnlyList<string> paths, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<CollectionHashCacheEntry?> TryGetHashAsync(string path, string algorithm, long sizeBytes, DateTime lastWriteUtc, CancellationToken ct = default)
            => ValueTask.FromResult<CollectionHashCacheEntry?>(null);

        public ValueTask SetHashAsync(CollectionHashCacheEntry entry, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask AppendRunSnapshotAsync(CollectionRunSnapshot snapshot, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<int> CountRunSnapshotsAsync(CancellationToken ct = default)
            => ValueTask.FromResult(_snapshots.Count);

        public ValueTask<IReadOnlyList<CollectionRunSnapshot>> ListRunSnapshotsAsync(int limit = 50, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<CollectionRunSnapshot>>(_snapshots.Take(limit).ToArray());
    }

    private static void InvokePrivate(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(target, args);
    }
}
