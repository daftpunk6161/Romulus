using RomCleanup.Tests.Benchmark.Models;
using RomCleanup.Tests.Benchmark.Infrastructure;

namespace RomCleanup.Tests.Benchmark;

internal static class BenchmarkEvaluationRunner
{
    public static BenchmarkSampleResult Evaluate(BenchmarkFixture fixture, GroundTruthEntry entry)
    {
        var samplePath = fixture.GetSamplePath(entry);
        if (!File.Exists(samplePath))
        {
            return new BenchmarkSampleResult(
                entry.Id,
                BenchmarkVerdict.Missed,
                entry.Expected.ConsoleKey,
                "UNKNOWN",
                0,
                false,
                $"Sample file missing: {samplePath}");
        }

        var detection = fixture.Detector.DetectWithConfidence(samplePath, fixture.SamplesRoot);
        return GroundTruthComparator.Compare(entry, detection);
    }

    public static List<BenchmarkSampleResult> EvaluateSet(BenchmarkFixture fixture, string setFileName)
    {
        var entries = GroundTruthLoader.LoadSet(setFileName);
        return entries
            .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(entry => Evaluate(fixture, entry))
            .ToList();
    }
}
