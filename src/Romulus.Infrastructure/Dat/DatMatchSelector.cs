using Romulus.Contracts.Models;

namespace Romulus.Infrastructure.Dat;

/// <summary>
/// Single-match helper bridging the new multi-match <c>LookupByHash</c> surface to
/// legacy single-match call sites. Picks the deterministic first element of the list,
/// matching the historical <see cref="DatIndex.LookupAny(string)"/> contract.
/// </summary>
/// <remarks>
/// In Phase 3 the production <c>MultiDatConflictResolver</c> will replace this helper
/// for ambiguous matches; until then the helper preserves the single-match invariant
/// without changing observable behavior.
/// </remarks>
public static class DatMatchSelector
{
    /// <summary>
    /// Returns the first element of <paramref name="matches"/> in deterministic
    /// console-key order, or <c>null</c> when the list is empty or <c>null</c>.
    /// </summary>
    public static DatMatch? SelectSingle(IReadOnlyList<DatMatch>? matches)
    {
        if (matches is null || matches.Count == 0)
            return null;
        return matches[0];
    }
}
