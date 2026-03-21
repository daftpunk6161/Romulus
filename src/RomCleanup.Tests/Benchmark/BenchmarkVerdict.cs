namespace RomCleanup.Tests.Benchmark;

/// <summary>
/// Result of comparing a ground-truth entry against actual detection output.
/// </summary>
public enum BenchmarkVerdict
{
    /// <summary>Detection matches the expected console key exactly.</summary>
    Correct,

    /// <summary>Detection matches an acceptable alternative console key.</summary>
    Acceptable,

    /// <summary>Detection produced a wrong console key.</summary>
    Wrong,

    /// <summary>Detection produced no result (UNKNOWN) for a known console.</summary>
    Missed,

    /// <summary>Negative control correctly identified as unknown.</summary>
    TrueNegative,

    /// <summary>Negative control incorrectly assigned a console.</summary>
    FalsePositive
}

/// <summary>
/// Detailed result of evaluating a single ground-truth sample.
/// </summary>
public sealed record BenchmarkSampleResult(
    string Id,
    BenchmarkVerdict Verdict,
    string? ExpectedConsoleKey,
    string? ActualConsoleKey,
    int ActualConfidence,
    bool ActualHasConflict,
    string? Details);
