using RomCleanup.Core.Classification;
using RomCleanup.Tests.Benchmark.Models;

namespace RomCleanup.Tests.Benchmark;

/// <summary>
/// Compares actual ConsoleDetectionResult against ground-truth expectations.
/// Produces a BenchmarkSampleResult with verdict and details.
/// </summary>
internal static class GroundTruthComparator
{
    /// <summary>
    /// Compare actual detection result against expected ground truth.
    /// </summary>
    public static BenchmarkSampleResult Compare(GroundTruthEntry entry, ConsoleDetectionResult actual)
    {
        var expected = entry.Expected;
        var actualKey = actual.ConsoleKey;
        var isUnknown = actualKey is "UNKNOWN" or "" or null;

        // Case 1: Negative control — expected.consoleKey is null or category is non-ROM
        if (expected.ConsoleKey is null || expected.Category is "NonRom" or "Junk" or "Unknown")
        {
            return isUnknown
                ? new BenchmarkSampleResult(
                    entry.Id, BenchmarkVerdict.TrueNegative,
                    expected.ConsoleKey, actualKey, actual.Confidence, actual.HasConflict,
                    "Correctly identified as unknown/non-ROM")
                : new BenchmarkSampleResult(
                    entry.Id, BenchmarkVerdict.FalsePositive,
                    expected.ConsoleKey, actualKey, actual.Confidence, actual.HasConflict,
                    $"False positive: expected unknown but got '{actualKey}'");
        }

        // Case 2: Expected a specific console → check exact match
        if (string.Equals(expected.ConsoleKey, actualKey, StringComparison.OrdinalIgnoreCase))
        {
            return new BenchmarkSampleResult(
                entry.Id, BenchmarkVerdict.Correct,
                expected.ConsoleKey, actualKey, actual.Confidence, actual.HasConflict,
                null);
        }

        // Case 3: Check acceptable alternatives
        var alternatives = entry.DetectionExpectations?.AcceptableConsoleKeys;
        if (alternatives is not null && alternatives.Any(
                alt => string.Equals(alt, actualKey, StringComparison.OrdinalIgnoreCase)))
        {
            return new BenchmarkSampleResult(
                entry.Id, BenchmarkVerdict.Acceptable,
                expected.ConsoleKey, actualKey, actual.Confidence, actual.HasConflict,
                $"Matched acceptable alternative '{actualKey}'");
        }

        // Case 4: Detection missed (returned UNKNOWN for a known console)
        if (isUnknown)
        {
            return new BenchmarkSampleResult(
                entry.Id, BenchmarkVerdict.Missed,
                expected.ConsoleKey, actualKey, actual.Confidence, actual.HasConflict,
                $"Expected '{expected.ConsoleKey}' but got UNKNOWN");
        }

        // Case 5: Wrong detection
        return new BenchmarkSampleResult(
            entry.Id, BenchmarkVerdict.Wrong,
            expected.ConsoleKey, actualKey, actual.Confidence, actual.HasConflict,
            $"Expected '{expected.ConsoleKey}' but got '{actualKey}'");
    }
}
