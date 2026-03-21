namespace RomCleanup.Tests.Benchmark;

internal sealed record RegressionReport(
    bool HasBaseline,
    double WrongMatchRateDelta,
    IReadOnlyList<string> PerSystemRegressions,
    BenchmarkReport? BaselineReport,
    BenchmarkReport CurrentReport);

internal static class BaselineComparator
{
    public static RegressionReport Compare(BenchmarkReport currentReport, string baselinePath)
    {
        if (!File.Exists(baselinePath))
        {
            return new RegressionReport(
                HasBaseline: false,
                WrongMatchRateDelta: 0,
                PerSystemRegressions: [],
                BaselineReport: null,
                CurrentReport: currentReport);
        }

        var baseline = BenchmarkReportWriter.Read(baselinePath);

        var currentPerSystem = currentReport.PerSystem ?? new Dictionary<string, BenchmarkSystemSummary>(StringComparer.OrdinalIgnoreCase);
        var baselinePerSystem = baseline.PerSystem ?? new Dictionary<string, BenchmarkSystemSummary>(StringComparer.OrdinalIgnoreCase);

        var regressions = new List<string>();
        var allSystemKeys = new HashSet<string>(baselinePerSystem.Keys, StringComparer.OrdinalIgnoreCase);
        allSystemKeys.UnionWith(currentPerSystem.Keys);

        foreach (var systemKey in allSystemKeys)
        {
            baselinePerSystem.TryGetValue(systemKey, out var previous);
            currentPerSystem.TryGetValue(systemKey, out var current);

            int baselineWrong = (previous?.Wrong ?? 0) + (previous?.FalsePositive ?? 0);
            int currentWrong = (current?.Wrong ?? 0) + (current?.FalsePositive ?? 0);

            if (currentWrong > baselineWrong)
            {
                regressions.Add(systemKey);
            }
        }

        return new RegressionReport(
            HasBaseline: true,
            WrongMatchRateDelta: currentReport.WrongMatchRate - baseline.WrongMatchRate,
            PerSystemRegressions: regressions,
            BaselineReport: baseline,
            CurrentReport: currentReport);
    }
}
