using RomCleanup.Core.Caching;
using RomCleanup.Infrastructure.Events;
using RomCleanup.Infrastructure.Hashing;
using RomCleanup.Infrastructure.State;
using Xunit;

namespace RomCleanup.Tests;

/// <summary>
/// TEST-CONC: Concurrency tests for thread-safe components.
/// Covers LruCache, EventBus, AppStateStore, FileHashService.
/// </summary>
public sealed class ConcurrencyTests : IDisposable
{
    private readonly string _tempDir;

    public ConcurrencyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RomCleanup_Conc_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ── TEST-CONC-01: LruCache concurrent reads/writes ──

    [Fact]
    public void LruCache_ConcurrentSetAndGet_NoCrash()
    {
        var cache = new LruCache<string, int>(100);

        Parallel.For(0, 1000, i =>
        {
            cache.Set($"key-{i % 50}", i);
            cache.TryGet($"key-{i % 50}", out _);
        });

        Assert.True(cache.Count > 0);
        Assert.True(cache.Count <= 100);
    }

    [Fact]
    public void LruCache_ConcurrentEviction_ConsistentState()
    {
        var cache = new LruCache<int, int>(10);

        Parallel.For(0, 500, i =>
        {
            cache.Set(i, i * 2);
        });

        Assert.Equal(10, cache.Count);
        var snapshot = cache.GetSnapshot();
        Assert.Equal(10, snapshot.Count);
    }

    // ── TEST-CONC-02: EventBus concurrent publish/subscribe ──

    [Fact]
    public void EventBus_ConcurrentPublishSubscribe_NoCrash()
    {
        var bus = new EventBus();
        int totalDelivered = 0;

        // Subscribe from multiple threads
        Parallel.For(0, 50, i =>
        {
            bus.Subscribe($"topic-{i % 5}", _ => Interlocked.Increment(ref totalDelivered));
        });

        // Publish from multiple threads
        Parallel.For(0, 100, i =>
        {
            bus.Publish($"topic-{i % 5}");
        });

        Assert.True(totalDelivered > 0);
    }

    [Fact]
    public void EventBus_ConcurrentSubscribeUnsubscribe_NoDeadlock()
    {
        var bus = new EventBus();
        var ids = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Subscribe
        Parallel.For(0, 100, i =>
        {
            var id = bus.Subscribe("test", _ => { });
            ids.Add(id);
        });

        // Unsubscribe half
        int count = 0;
        Parallel.ForEach(ids, id =>
        {
            if (Interlocked.Increment(ref count) % 2 == 0)
                bus.Unsubscribe(id);
        });

        // Should still work
        bus.Publish("test");
    }

    // ── TEST-CONC-03: AppStateStore concurrent SetValue/GetValue ──

    [Fact]
    public void AppState_ConcurrentSetGet_NoCrash()
    {
        var store = new AppStateStore();

        Parallel.For(0, 500, i =>
        {
            store.SetValue($"key-{i % 20}", i);
            store.GetValue<int>($"key-{i % 20}");
        });

        // Should have consistent state
        var state = store.Get();
        Assert.True(state.Count > 0);
    }

    [Fact]
    public void AppState_ConcurrentUndoRedo_NoCrash()
    {
        var store = new AppStateStore();

        // Pre-fill
        for (int i = 0; i < 50; i++)
            store.SetValue("x", i);

        // Concurrent undo/redo
        Parallel.For(0, 100, i =>
        {
            if (i % 2 == 0)
                store.Undo();
            else
                store.Redo();
        });

        // Should not throw
        var val = store.GetValue<int>("x");
        Assert.True(val >= 0);
    }

    [Fact]
    public void AppState_ConcurrentWatchNotify_NoCrash()
    {
        var store = new AppStateStore();
        int notifications = 0;

        var watchers = Enumerable.Range(0, 10)
            .Select(_ => store.Watch(_ => Interlocked.Increment(ref notifications)))
            .ToList();

        Parallel.For(0, 100, i =>
        {
            store.SetValue("x", i);
        });

        // Dispose watchers
        foreach (var w in watchers)
            w.Dispose();

        Assert.True(notifications > 0);
    }

    // ── TEST-CONC-04: FileHashService concurrent hashing ──

    [Fact]
    public void FileHashService_ConcurrentHash_ConsistentResults()
    {
        var svc = new FileHashService(1000);
        var file = Path.Combine(_tempDir, "concurrent.bin");
        File.WriteAllBytes(file, new byte[] { 1, 2, 3, 4, 5 });

        var hashes = new System.Collections.Concurrent.ConcurrentBag<string?>();

        Parallel.For(0, 50, _ =>
        {
            hashes.Add(svc.GetHash(file, "SHA1"));
        });

        var distinct = hashes.Where(h => h != null).Distinct().ToList();
        Assert.Single(distinct); // all threads see the same hash
    }

    // ── TEST-CONC-05: Cancel flag is thread-safe ──

    [Fact]
    public async Task AppState_CancelFlag_ThreadSafe()
    {
        var store = new AppStateStore();

        var tasks = new List<Task>();

        // Writer thread
        tasks.Add(Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                store.RequestCancel();
                store.ResetCancel();
            }
        }));

        // Reader threads
        for (int t = 0; t < 4; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    _ = store.TestCancel();
                }
            }));
        }

        await Task.WhenAll(tasks);
        // No crash = pass
    }
}
