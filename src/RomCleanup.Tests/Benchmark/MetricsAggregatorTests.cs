using Xunit;

namespace RomCleanup.Tests.Benchmark;

public sealed class MetricsAggregatorTests
{
    [Fact]
    public void MetricsAggregator_CalculatesCorrectly()
    {
        var samples = new List<BenchmarkSampleResult>
        {
            new("1", BenchmarkVerdict.Correct, "NES", "NES", 90, false, null),
            new("2", BenchmarkVerdict.Wrong, "NES", "SNES", 90, false, null),
            new("3", BenchmarkVerdict.Correct, "SNES", "SNES", 90, false, null),
            new("4", BenchmarkVerdict.Missed, "SNES", "UNKNOWN", 0, false, null),
            new("5", BenchmarkVerdict.FalsePositive, null, "NES", 90, false, null),
        };

        var perSystem = MetricsAggregator.CalculatePerSystem(samples);
        var aggregate = MetricsAggregator.CalculateAggregate(samples);
        var matrix = MetricsAggregator.BuildConfusionMatrix(samples);

        Assert.True(perSystem.ContainsKey("NES"));
        Assert.True(perSystem.ContainsKey("SNES"));

        // NES: TP=1, FP=1, FN=1 => precision=0.5, recall=0.5, f1=0.5
        Assert.Equal(0.5, perSystem["NES"].Precision, 3);
        Assert.Equal(0.5, perSystem["NES"].Recall, 3);
        Assert.Equal(0.5, perSystem["NES"].F1, 3);

        // Wrongs include wrong + false positive => 2/5 = 0.4
        Assert.Equal(0.4, aggregate["wrongMatchRate"], 3);
        Assert.NotEmpty(matrix);
    }
}
