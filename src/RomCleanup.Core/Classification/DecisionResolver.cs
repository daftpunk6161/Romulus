using RomCleanup.Contracts.Models;

namespace RomCleanup.Core.Classification;

/// <summary>
/// Central tier-based decision resolver for recognition results.
/// </summary>
public static class DecisionResolver
{
    /// <summary>
    /// Resolve decision from evidence tier, conflict state, and confidence.
    /// Backward-compatible overload without DAT-gate or family-conflict awareness.
    /// </summary>
    public static DecisionClass Resolve(EvidenceTier tier, bool hasConflict, int confidence)
        => Resolve(tier, hasConflict, confidence, datAvailable: false, conflictType: ConflictType.None);

    /// <summary>
    /// Resolve decision with full DAT-gate and family-conflict awareness.
    /// <para>
    /// Conservative DAT gate: When a DAT index is loaded (<paramref name="datAvailable"/> = true)
    /// but the file did NOT hash-match (tier &gt; Tier0), Tier1 structural evidence
    /// is capped at <see cref="DecisionClass.Review"/>. Rationale: a loaded DAT that
    /// doesn't contain the hash is a negative signal — the file may be undumped,
    /// bad, or misidentified.
    /// </para>
    /// <para>
    /// Family-conflict gate: Cross-family conflicts always produce
    /// <see cref="DecisionClass.Blocked"/>; intra-family conflicts without
    /// structural evidence produce <see cref="DecisionClass.Review"/>.
    /// </para>
    /// </summary>
    public static DecisionClass Resolve(
        EvidenceTier tier,
        bool hasConflict,
        int confidence,
        bool datAvailable,
        ConflictType conflictType)
    {
        // Tier0 = exact DAT hash. Absolute authority.
        if (tier == EvidenceTier.Tier0_ExactDat && !hasConflict)
            return DecisionClass.DatVerified;

        if (tier == EvidenceTier.Tier0_ExactDat && hasConflict)
        {
            // Cross-family DAT conflict → Blocked (fundamentally different systems)
            if (conflictType == ConflictType.CrossFamily)
                return DecisionClass.Blocked;
            return DecisionClass.Review;
        }

        // Cross-family conflict at any non-DAT tier → always Blocked
        if (hasConflict && conflictType == ConflictType.CrossFamily)
            return DecisionClass.Blocked;

        // Conservative DAT gate: DAT loaded but no hash match → cap at Review
        if (tier == EvidenceTier.Tier1_Structural && datAvailable)
            return DecisionClass.Review;

        // Tier1 without DAT loaded: original behavior
        if (tier == EvidenceTier.Tier1_Structural && !hasConflict && confidence >= 85)
            return DecisionClass.Sort;

        if (tier == EvidenceTier.Tier1_Structural)
            return DecisionClass.Review;

        if (tier == EvidenceTier.Tier2_StrongHeuristic)
            return DecisionClass.Review;

        if (tier == EvidenceTier.Tier3_WeakHeuristic)
            return DecisionClass.Blocked;

        return DecisionClass.Unknown;
    }
}
