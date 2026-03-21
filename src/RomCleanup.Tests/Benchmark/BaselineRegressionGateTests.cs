using RomCleanup.Tests.Benchmark.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace RomCleanup.Tests.Benchmark;

[Collection("BenchmarkEvaluation")]
public sealed class BaselineRegressionGateTests : IClassFixture<BenchmarkFixture>
{
    private readonly BenchmarkFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BaselineRegressionGateTests(BenchmarkFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    [Trait("Category", "BenchmarkRegression")]
    public void BenchmarkResults_NoRegressionVsBaseline()
    {
        var setFiles = new[]
        {
            "golden-core.jsonl",
            "edge-cases.jsonl",
            "negative-controls.jsonl",
            "golden-realworld.jsonl",
            "chaos-mixed.jsonl",
            "dat-coverage.jsonl",
            "repair-safety.jsonl",
        };

        var results = new List<BenchmarkSampleResult>();
        foreach (var set in setFiles)
        {
            results.AddRange(BenchmarkEvaluationRunner.EvaluateSet(_fixture, set));
        }

        var aggregate = MetricsAggregator.CalculateAggregate(results);
        var confusion = MetricsAggregator.BuildConfusionMatrix(results);
        var report = BenchmarkReportWriter.CreateReport(results, "1.0.0", aggregate, confusion);

        BenchmarkReportWriter.Write(report, BenchmarkPaths.CurrentBenchmarkReportPath);

        var regression = BaselineComparator.Compare(report, BenchmarkPaths.LatestBaselinePath);
        if (!regression.HasBaseline)
        {
            _output.WriteLine($"No baseline found at {BenchmarkPaths.LatestBaselinePath}. Regression gate skipped.");
            return;
        }

        _output.WriteLine($"WrongMatchRateDelta={regression.WrongMatchRateDelta:F6}");
        Assert.True(regression.WrongMatchRateDelta <= 0.001,
            $"Wrong match rate regression: {regression.WrongMatchRateDelta:P3} > 0.100% threshold");
        Assert.True(regression.PerSystemRegressions.Count == 0,
            "Per-system regressions: " + string.Join(", ", regression.PerSystemRegressions));
    }
}
