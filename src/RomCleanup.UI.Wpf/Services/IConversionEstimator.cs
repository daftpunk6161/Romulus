using RomCleanup.Contracts.Models;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>GUI-036: Conversion estimation, format priority.</summary>
public interface IConversionEstimator
{
    ConversionEstimateResult GetConversionEstimate(IReadOnlyList<RomCandidate> candidates);
    string? GetTargetFormat(string ext);
    (int passed, int failed, int missing) VerifyConversions(IReadOnlyList<string> targetPaths, long minSize = 1);
    string FormatFormatPriority();
}
