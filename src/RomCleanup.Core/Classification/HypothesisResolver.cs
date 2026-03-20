namespace RomCleanup.Core.Classification;

/// <summary>
/// Resolves multiple detection hypotheses into a single deterministic result.
/// Uses weighted confidence scoring and deterministic tie-breaking.
/// </summary>
public static class HypothesisResolver
{
    /// <summary>
    /// Resolve a set of hypotheses into a single console detection result.
    /// Rules:
    /// 1. Group by ConsoleKey, sum confidence per key.
    /// 2. Highest total confidence wins.
    /// 3. On tie: alphabetical ConsoleKey (deterministic).
    /// 4. If multiple distinct ConsoleKeys have hypotheses, mark as conflict.
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

        // Aggregate confidence: use the max single confidence from the winner
        // (capped at 100, boosted slightly if multiple sources agree)
        var aggregateConfidence = winner.Value.MaxSingleConfidence;
        if (winner.Value.Items.Count > 1)
        {
            // Multi-source agreement bonus: +5 per additional source, max 100
            aggregateConfidence = Math.Min(100, aggregateConfidence + (winner.Value.Items.Count - 1) * 5);
        }

        // Penalize if there's a strong competing hypothesis
        if (hasConflict)
        {
            var runnerConfidence = sorted[1].Value.MaxSingleConfidence;
            if (runnerConfidence >= 80)
            {
                // Strong conflict — reduce confidence
                aggregateConfidence = Math.Max(30, aggregateConfidence - 20);
            }
            else if (runnerConfidence >= 50)
            {
                aggregateConfidence = Math.Max(40, aggregateConfidence - 10);
            }
        }

        return new ConsoleDetectionResult(
            winner.Key,
            aggregateConfidence,
            hypotheses,
            hasConflict,
            conflictDetail);
    }
}
