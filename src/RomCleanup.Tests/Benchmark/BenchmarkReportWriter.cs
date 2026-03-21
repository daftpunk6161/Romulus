using System.Text.Json;

namespace RomCleanup.Tests.Benchmark;

internal sealed record BenchmarkSystemSummary(int Correct, int Acceptable, int Wrong, int Missed, int TrueNegative, int FalsePositive);

internal sealed record BenchmarkReport(
    DateTimeOffset Timestamp,
    string GroundTruthVersion,
    int TotalSamples,
    int Correct,
    int Acceptable,
    int Wrong,
    int Missed,
    int TrueNegative,
    int FalsePositive,
    double WrongMatchRate,
    IReadOnlyDictionary<string, BenchmarkSystemSummary> PerSystem,
    IReadOnlyDictionary<string, double> AggregateMetrics,
    IReadOnlyList<ConfusionEntry> ConfusionMatrix);

internal static class BenchmarkReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static BenchmarkReport CreateReport(
        IReadOnlyList<BenchmarkSampleResult> results,
        string groundTruthVersion,
        IReadOnlyDictionary<string, double>? aggregateMetrics = null,
        IReadOnlyList<ConfusionEntry>? confusionMatrix = null)
    {
        var bySystem = results
            .GroupBy(r => r.ExpectedConsoleKey ?? "UNKNOWN", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new BenchmarkSystemSummary(
                    Correct: g.Count(x => x.Verdict == BenchmarkVerdict.Correct),
                    Acceptable: g.Count(x => x.Verdict == BenchmarkVerdict.Acceptable),
                    Wrong: g.Count(x => x.Verdict == BenchmarkVerdict.Wrong),
                    Missed: g.Count(x => x.Verdict == BenchmarkVerdict.Missed),
                    TrueNegative: g.Count(x => x.Verdict == BenchmarkVerdict.TrueNegative),
                    FalsePositive: g.Count(x => x.Verdict == BenchmarkVerdict.FalsePositive)),
                StringComparer.OrdinalIgnoreCase);

        int total = results.Count;
        int wrong = results.Count(r => r.Verdict == BenchmarkVerdict.Wrong || r.Verdict == BenchmarkVerdict.FalsePositive);

        return new BenchmarkReport(
            Timestamp: DateTimeOffset.UtcNow,
            GroundTruthVersion: groundTruthVersion,
            TotalSamples: total,
            Correct: results.Count(r => r.Verdict == BenchmarkVerdict.Correct),
            Acceptable: results.Count(r => r.Verdict == BenchmarkVerdict.Acceptable),
            Wrong: results.Count(r => r.Verdict == BenchmarkVerdict.Wrong),
            Missed: results.Count(r => r.Verdict == BenchmarkVerdict.Missed),
            TrueNegative: results.Count(r => r.Verdict == BenchmarkVerdict.TrueNegative),
            FalsePositive: results.Count(r => r.Verdict == BenchmarkVerdict.FalsePositive),
            WrongMatchRate: total == 0 ? 0 : (double)wrong / total,
            PerSystem: bySystem,
            AggregateMetrics: aggregateMetrics ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            ConfusionMatrix: confusionMatrix ?? []);
    }

    public static void Write(BenchmarkReport report, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static BenchmarkReport Read(string path)
    {
        var json = File.ReadAllText(path);
        var report = JsonSerializer.Deserialize<BenchmarkReport>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize benchmark report from '{path}'.");
        return report;
    }
}
