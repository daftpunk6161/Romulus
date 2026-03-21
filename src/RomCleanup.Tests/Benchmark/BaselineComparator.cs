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
        foreach (var currentPair in currentPerSystem)
        {
            if (!baselinePerSystem.TryGetValue(currentPair.Key, out var previous))
            {
                continue;
            }

            if (currentPair.Value.Wrong + currentPair.Value.FalsePositive > previous.Wrong + previous.FalsePositive)
            {
                regressions.Add(currentPair.Key);
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
