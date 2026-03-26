using RomCleanup.Contracts.Models;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>GUI-036: Delegates to static FeatureService.Conversion methods.</summary>
public sealed class ConversionEstimator : IConversionEstimator
{
    public ConversionEstimateResult GetConversionEstimate(IReadOnlyList<RomCandidate> candidates)
        => FeatureService.GetConversionEstimate(candidates);

    public string? GetTargetFormat(string ext)
        => FeatureService.GetTargetFormat(ext);

    public (int passed, int failed, int missing) VerifyConversions(IReadOnlyList<string> targetPaths, long minSize = 1)
        => FeatureService.VerifyConversions(targetPaths, minSize);

    public string FormatFormatPriority()
        => FeatureService.FormatFormatPriority();
}
