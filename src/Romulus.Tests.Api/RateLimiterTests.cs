using Romulus.Api;
using Romulus.Contracts.Ports;
using System.Reflection;
using Xunit;

namespace Romulus.Tests;

public class RateLimiterTests
{
    [Fact]
    public void TryAcquire_AllowsUpToLimit()
    {
        var limiter = new RateLimiter(3, TimeSpan.FromMinutes(1));

        Assert.True(limiter.TryAcquire("client1"));
        Assert.True(limiter.TryAcquire("client1"));
        Assert.True(limiter.TryAcquire("client1"));
        Assert.False(limiter.TryAcquire("client1"));
    }

    [Fact]
    public void TryAcquire_DifferentClients_IndependentBuckets()
    {
        var limiter = new RateLimiter(2, TimeSpan.FromMinutes(1));

        Assert.True(limiter.TryAcquire("a"));
        Assert.True(limiter.TryAcquire("a"));
        Assert.False(limiter.TryAcquire("a"));

        // Client b has its own bucket
        Assert.True(limiter.TryAcquire("b"));
        Assert.True(limiter.TryAcquire("b"));
        Assert.False(limiter.TryAcquire("b"));
    }

    [Fact]
    public void TryAcquire_Disabled_AlwaysAllows()
    {
        var limiter = new RateLimiter(0, TimeSpan.FromMinutes(1));

        for (int i = 0; i < 1000; i++)
            Assert.True(limiter.TryAcquire("client"));
    }

    [Fact]
    public void TryAcquire_WindowExpires_Resets()
    {
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));
        var limiter = new RateLimiter(1, TimeSpan.FromSeconds(1), clock);

        Assert.True(limiter.TryAcquire("c"));
        Assert.False(limiter.TryAcquire("c"));

        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.True(limiter.TryAcquire("c"));
    }

    [Fact]
    public void TryAcquire_AfterEvictionInterval_RemovesStaleClientBuckets()
    {
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 4, 10, 12, 0, 0, TimeSpan.Zero));
        var limiter = new RateLimiter(1, TimeSpan.FromSeconds(1), clock);

        Assert.True(limiter.TryAcquire("stale-client"));
        Assert.True(ContainsBucket(limiter, "stale-client"));

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(7));

        Assert.True(limiter.TryAcquire("fresh-client"));

        Assert.False(ContainsBucket(limiter, "stale-client"));
        Assert.True(ContainsBucket(limiter, "fresh-client"));
        Assert.Equal(1, BucketCount(limiter));
    }

    private static int BucketCount(RateLimiter limiter)
    {
        var buckets = GetBuckets(limiter);
        return (int)buckets.GetType().GetProperty("Count")!.GetValue(buckets)!;
    }

    private static bool ContainsBucket(RateLimiter limiter, string clientId)
    {
        var buckets = GetBuckets(limiter);
        return (bool)buckets.GetType().GetMethod("ContainsKey")!.Invoke(buckets, new object[] { clientId })!;
    }

    private static object GetBuckets(RateLimiter limiter)
        => typeof(RateLimiter)
            .GetField("_buckets", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(limiter)!;

    private sealed class TestTimeProvider(DateTimeOffset initialUtcNow) : ITimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public DateTimeOffset UtcNow => _utcNow;

        public void Advance(TimeSpan delta)
        {
            _utcNow = _utcNow.Add(delta);
        }
    }
}
