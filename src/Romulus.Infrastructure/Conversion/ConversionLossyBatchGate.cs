using Romulus.Contracts.Models;
using Romulus.Core.Conversion;

namespace Romulus.Infrastructure.Conversion;

/// <summary>
/// T-W5-CONVERSION-SAFETY-ADVISOR — pre-flight gate for lossy conversions.
///
/// Single Source of Truth: token enforcement is delegated to
/// <see cref="ConversionLossyTokenPolicy.ValidateAcceptDataLossToken"/>.
/// This helper exists so the orchestration pipeline does not duplicate the
/// projection from <see cref="ConversionResult"/>/<see cref="ConversionPlan"/>
/// to <see cref="ConversionLossyPlanItem"/>.
///
/// Token MUST stay out of logs and MUST never be invented at the call site.
/// Callers receive the canonical token from <c>RunResult.PendingLossyToken</c>
/// (computed from the same projection) and forward it via
/// <c>RunOptions.AcceptDataLossToken</c>.
/// </summary>
internal static class ConversionLossyBatchGate
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when <paramref name="lossyItems"/>
    /// is non-empty and <paramref name="acceptDataLossToken"/> is missing or
    /// does not match the deterministic token. No-op when the list is empty.
    /// </summary>
    public static void Enforce(
        IReadOnlyList<ConversionLossyPlanItem> lossyItems,
        string? acceptDataLossToken)
    {
        ArgumentNullException.ThrowIfNull(lossyItems);
        ConversionLossyTokenPolicy.ValidateAcceptDataLossToken(lossyItems, acceptDataLossToken);
    }

    /// <summary>
    /// Projects already-classified <see cref="ConversionResult"/>s
    /// (post-DryRun or post-execute) to the canonical lossy plan items
    /// expected by <see cref="ConversionLossyTokenPolicy"/>. Used by the
    /// orchestrator to populate <c>RunResult.PendingLossyToken</c> from a
    /// DryRun, so the caller can echo it back when executing.
    /// </summary>
    public static IReadOnlyList<ConversionLossyPlanItem> CollectLossyFromResults(
        IReadOnlyList<ConversionResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (results.Count == 0)
            return Array.Empty<ConversionLossyPlanItem>();

        var list = new List<ConversionLossyPlanItem>(results.Count);
        foreach (var r in results)
        {
            if (r is null) continue;
            if (r.SourceIntegrity != SourceIntegrity.Lossy) continue;

            var srcExt = NormalizeExtension(Path.GetExtension(r.SourcePath));
            var tgtExt = NormalizeExtension(r.Plan?.FinalTargetExtension);

            // Skip degenerate items where we cannot derive a stable target
            // extension; including them would silently change the canonical
            // token and let an unrelated dry-run-token sneak through.
            if (string.IsNullOrEmpty(tgtExt))
                continue;

            list.Add(new ConversionLossyPlanItem(r.SourcePath, srcExt, tgtExt));
        }
        return list;
    }

    /// <summary>
    /// Same projection as <see cref="CollectLossyFromResults"/> but for raw
    /// pre-execute (filePath, plan) pairs from a planner pre-flight pass.
    /// Items where the plan is null, non-executable, or non-lossy are filtered.
    /// </summary>
    public static IReadOnlyList<ConversionLossyPlanItem> CollectLossyFromPlans(
        IEnumerable<(string SourcePath, ConversionPlan? Plan)> planned)
    {
        ArgumentNullException.ThrowIfNull(planned);
        var list = new List<ConversionLossyPlanItem>();
        foreach (var (sourcePath, plan) in planned)
        {
            if (plan is null) continue;
            if (!plan.IsExecutable) continue;
            if (plan.SourceIntegrity != SourceIntegrity.Lossy) continue;

            var srcExt = NormalizeExtension(Path.GetExtension(sourcePath));
            var tgtExt = NormalizeExtension(plan.FinalTargetExtension);
            if (string.IsNullOrEmpty(tgtExt))
                continue;

            list.Add(new ConversionLossyPlanItem(sourcePath, srcExt, tgtExt));
        }
        return list;
    }

    private static string NormalizeExtension(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
            return string.Empty;
        var trimmed = ext.Trim().TrimStart('.');
        return trimmed.ToLowerInvariant();
    }
}
