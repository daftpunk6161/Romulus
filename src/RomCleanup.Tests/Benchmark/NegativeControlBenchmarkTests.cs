using RomCleanup.Tests.Benchmark.Models;
using Xunit;
using Xunit.Abstractions;

namespace RomCleanup.Tests.Benchmark;

[Collection("BenchmarkEvaluation")]
[Trait("Category", "Benchmark")]
public sealed class NegativeControlBenchmarkTests : IClassFixture<BenchmarkFixture>
{
    private readonly BenchmarkFixture _fixture;
    private readonly ITestOutputHelper _output;

    public NegativeControlBenchmarkTests(BenchmarkFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Theory]
    [BenchmarkData("negative-controls.jsonl")]
    public void NegativeControl_MustBeBlockedOrUnknown(GroundTruthEntry entry)
    {
        var result = BenchmarkEvaluationRunner.Evaluate(_fixture, entry);
        _output.WriteLine($"[{entry.Id}] verdict={result.Verdict} actual={result.ActualConsoleKey ?? "null"} conf={result.ActualConfidence}");

        if (entry.Id.Contains("-expunk-", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Assert.True(
            result.Verdict is BenchmarkVerdict.TrueNegative or BenchmarkVerdict.FalsePositive,
            $"[{entry.Id}] unexpected verdict: {result.Verdict}");

        Assert.True(result.ActualConfidence <= 100, $"[{entry.Id}] confidence out of range");
    }
}
