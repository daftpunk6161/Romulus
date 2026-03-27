using RomCleanup.Contracts.Models;

namespace RomCleanup.Core.Classification;

/// <summary>
/// Resolves multiple detection hypotheses into a single deterministic result.
/// Uses weighted confidence scoring, evidence classification, and deterministic tie-breaking.
/// Produces a SortDecision that gates whether automatic sorting is safe.
/// </summary>
public static class HypothesisResolver
{
    /// <summary>Baseline soft-only cap for weak single-source signals.</summary>
    internal const int SoftOnlyCap = 65;

    /// <summary>Minimum confidence for Sort decision.</summary>
    internal const int SortThreshold = 85;

    /// <summary>Minimum confidence for Review decision (below Sort threshold).</summary>
    internal const int ReviewThreshold = 65;

    /// <summary>
    /// Resolve a set of hypotheses into a single console detection result.
    /// Rules:
    /// 1. Group by ConsoleKey, sum confidence per key.
    /// 2. Highest total confidence wins.
    /// 3. On tie: alphabetical ConsoleKey (deterministic).
    /// 4. If multiple distinct ConsoleKeys have hypotheses, mark as conflict.
    /// 5. Soft-only detections are capped at 65 (never auto-sortable).
    /// 6. Single-source detections are capped per source type.
    /// 7. AMBIGUOUS is returned when two strong conflicting consoles are equally plausible.
    /// 8. SortDecision is derived from confidence, conflict, and evidence type.
    /// </summary>
    public static ConsoleDetectionResult Resolve(IReadOnlyList<DetectionHypothesis> hypotheses)
    {
        if (hypotheses.Count == 0)
            return ConsoleDetectionResult.Unknown;

        // Group by ConsoleKey and sum confidence
        var groups = new Dictionary<string, (int TotalConfidence, int MaxSingleConfidence, List<DetectionHypothesis> Items)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var h in hypotheses)
        {
            if (!groups.TryGetValue(h.ConsoleKey, out var group))
            {
                group = (0, 0, new List<DetectionHypothesis>());
                groups[h.ConsoleKey] = group;
            }

            group.Items.Add(h);
            group.TotalConfidence += h.Confidence;
            if (h.Confidence > group.MaxSingleConfidence)
                group.MaxSingleConfidence = h.Confidence;
            groups[h.ConsoleKey] = group;
        }

        // Sort by total confidence descending, then alphabetically for determinism
        var sorted = groups
            .OrderByDescending(g => g.Value.TotalConfidence)
            .ThenByDescending(g => g.Value.MaxSingleConfidence)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var winner = sorted[0];
        bool hasConflict = sorted.Count > 1;
        string? conflictDetail = null;

        if (hasConflict)
        {
            var runner = sorted[1];
            conflictDetail = $"Conflict: {winner.Key}({winner.Value.TotalConfidence}) vs {runner.Key}({runner.Value.TotalConfidence})";
        }

        // Check for AMBIGUOUS: two strong competing consoles both ≥ 60
        // Only trigger when no side has a clear hard-evidence advantage
        if (hasConflict && sorted.Count >= 2)
        {
            var runnerMax = sorted[1].Value.MaxSingleConfidence;
            if (winner.Value.MaxSingleConfidence >= 60 && runnerMax >= 60)
            {
                var winnerHasHard = winner.Value.Items.Any(h => h.Source.IsHardEvidence());
                var runnerHasHard = sorted[1].Value.Items.Any(h => h.Source.IsHardEvidence());

                // Only AMBIGUOUS when evidence quality is comparable:
                // both have hard evidence, or neither has hard evidence
                if (winnerHasHard == runnerHasHard)
                {
                    var ratio = (double)sorted[1].Value.TotalConfidence / winner.Value.TotalConfidence;
                    if (ratio >= 0.7)
                    {
                        return new ConsoleDetectionResult(
                            "AMBIGUOUS", 0, hypotheses, true, conflictDetail,
                            HasHardEvidence: false, IsSoftOnly: true,
                            SortDecision: SortDecision.Blocked);
                    }
                }
            }
        }

        var winnerSources = winner.Value.Items
            .Select(i => i.Source)
            .Distinct()
            .ToList();

        var hasHardEvidence = winnerSources.Any(s => s.IsHardEvidence());
        var runnerHasHardEvidence = hasConflict && sorted[1].Value.Items.Any(h => h.Source.IsHardEvidence());

        // Aggregate confidence: use the max single confidence from the winner
        // (capped at 100, boosted slightly if multiple sources agree)
        var aggregateConfidence = winner.Value.MaxSingleConfidence;
        if (winner.Value.Items.Count > 1)
        {
            // Multi-source agreement bonus: +5 per additional source, max 100
            aggregateConfidence = Math.Min(100, aggregateConfidence + (winner.Value.Items.Count - 1) * 5);
        }

        // Penalize if there's a strong competing hypothesis.
        // Keep deterministic behavior but avoid over-penalizing hard winner evidence
        // against weaker soft-context runner-up signals.
        if (hasConflict)
        {
            var runnerConfidence = sorted[1].Value.MaxSingleConfidence;
            var winnerConfidence = winner.Value.MaxSingleConfidence;
            var confidenceDelta = winnerConfidence - runnerConfidence;

            var effectivePenalty = 0;
            if (runnerConfidence >= 80)
            {
                effectivePenalty = 20;
            }
            else if (runnerConfidence >= 50)
            {
                effectivePenalty = 10;
            }

            if (hasHardEvidence && !runnerHasHardEvidence)
            {
                // Weak conflict against hard evidence: do not kill confidence aggressively.
                effectivePenalty = runnerConfidence >= 80
                    ? (confidenceDelta >= 10 ? 5 : 8)
                    : (confidenceDelta >= 10 ? 0 : 5);
            }

            aggregateConfidence = Math.Max(30, aggregateConfidence - effectivePenalty);
        }
        var isSoftOnly = !hasHardEvidence;

        // Soft-only cap: contextual-only detection remains bounded and explainable.
        // Source-specific caps remain respected, and multiple agreeing soft sources
        // may raise confidence into Review territory.
        if (isSoftOnly)
        {
            aggregateConfidence = Math.Min(aggregateConfidence, ComputeSoftOnlyCap(winnerSources));
        }

        // Single-source cap: one signal type alone is capped per its reliability
        if (winnerSources.Count == 1)
        {
            aggregateConfidence = Math.Min(aggregateConfidence, winnerSources[0].SingleSourceCap());
        }

        // Derive SortDecision
        var sortDecision = DetermineSortDecision(aggregateConfidence, hasConflict, hasHardEvidence);

        return new ConsoleDetectionResult(
            winner.Key,
            aggregateConfidence,
            hypotheses,
            hasConflict,
            conflictDetail,
            hasHardEvidence,
            isSoftOnly,
            sortDecision);
    }

    /// <summary>
    /// Derives the sort gate decision from confidence, conflict, and evidence type.
    /// </summary>
    internal static SortDecision DetermineSortDecision(int confidence, bool conflict, bool hardEvidence)
    {
        // DAT-verified: always sort
        if (confidence == 100)
            return SortDecision.DatVerified;

        // High confidence + hard evidence → Sort.
        // With conflict, require stronger confidence to avoid unsafe auto-sorting.
        if (confidence >= SortThreshold && hardEvidence && !conflict)
            return SortDecision.Sort;
        if (confidence >= 90 && hardEvidence && conflict)
            return SortDecision.Sort;

        // Review corridor: medium/high confidence should route to review instead of hard block.
        if (confidence >= SortThreshold && !hardEvidence)
            return SortDecision.Review;
        if (confidence >= ReviewThreshold && !conflict && hardEvidence)
            return SortDecision.Review;
        if (confidence >= 70 && conflict && hardEvidence)
            return SortDecision.Review;
        if (confidence >= ReviewThreshold && !conflict)
            return SortDecision.Review;
        if (confidence >= 60 && !conflict)
            return SortDecision.Review;

        // Everything else → Blocked
        return SortDecision.Blocked;
    }

    private static int ComputeSoftOnlyCap(IReadOnlyList<DetectionSource> winnerSources)
    {
        if (winnerSources.Count == 0)
            return SoftOnlyCap;

        if (winnerSources.Count == 1)
            return winnerSources[0].SingleSourceCap();

        var strongestSourceCap = winnerSources.Max(s => s.SingleSourceCap());
        var multiSourceAgreementBonus = Math.Min(15, (winnerSources.Count - 1) * 5);

        // Soft-only detections can become strong enough for review but should not
        // exceed hard-evidence sort confidence without corroboration.
        return Math.Min(85, strongestSourceCap + multiSourceAgreementBonus);
    }
}
