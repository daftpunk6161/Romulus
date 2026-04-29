using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Romulus.Core.Conversion;

/// <summary>
/// Wave 2 — T-W2-CONVERSION-SAFETY-CONTRACT.
/// Single lossy item in a planned conversion run. Used solely as input to
/// <see cref="ConversionLossyTokenPolicy"/>; consumers may build it from
/// any plan source (CLI, API, GUI). Source paths are <c>Ordinal</c>.
/// </summary>
public sealed record ConversionLossyPlanItem(
    string SourcePath,
    string SourceFormat,
    string TargetFormat);

/// <summary>
/// Wave 2 — T-W2-CONVERSION-SAFETY-CONTRACT.
///
/// <para>
/// Token-based pre-gate that hardens the lossy-conversion path against
/// silent data loss. The orchestrator computes the
/// <see cref="ComputeAcceptDataLossToken"/> token from the planned lossy
/// items in the <em>plan</em> phase and exposes it as
/// <c>RunResult.PendingLossyToken</c>. The execute phase MUST forward the
/// same token via <c>RunOptions.AcceptDataLossToken</c>; otherwise the
/// pre-gate throws <see cref="InvalidOperationException"/> and the run is
/// rejected.
/// </para>
///
/// <para>
/// Determinism: the token is a SHA-256 hash of a canonical, ordered
/// projection of all lossy plan items. Same plan ⇒ same token across
/// processes, machines, and locales. There is no time component.
/// </para>
///
/// <para>
/// Comparison: token comparison is <see cref="StringComparison.Ordinal"/>
/// — case-sensitive, byte-for-byte. Hex digests are emitted in lowercase.
/// </para>
///
/// <para>
/// Pure: this type performs no I/O and lives in <c>Romulus.Core</c>.
/// </para>
/// </summary>
public static class ConversionLossyTokenPolicy
{
    /// <summary>
    /// Returns the deterministic accept-data-loss token for the given lossy
    /// plan items, or <c>null</c> when the list is empty (no lossy step ⇒
    /// no token required).
    /// </summary>
    public static string? ComputeAcceptDataLossToken(IReadOnlyList<ConversionLossyPlanItem> lossyItems)
    {
        ArgumentNullException.ThrowIfNull(lossyItems);
        if (lossyItems.Count == 0)
            return null;

        var ordered = lossyItems
            .OrderBy(i => i.SourcePath, StringComparer.Ordinal)
            .ThenBy(i => i.SourceFormat, StringComparer.Ordinal)
            .ThenBy(i => i.TargetFormat, StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach (var item in ordered)
        {
            // canonical line: <src>|<srcFormat>|<targetFormat>\n
            // chosen pipe separator + LF; every component appears verbatim.
            sb.Append(item.SourcePath).Append('|')
              .Append(item.SourceFormat).Append('|')
              .Append(item.TargetFormat).Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);

        // lowercase hex; ordinal comparison.
        var hex = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) hex.Append(b.ToString("x2"));
        return hex.ToString();
    }

    /// <summary>
    /// Validates the supplied <paramref name="providedToken"/> against the
    /// expected token computed from <paramref name="lossyItems"/>. When the
    /// plan contains no lossy items, the token is irrelevant and no
    /// validation occurs. Otherwise the provided token MUST equal the
    /// expected token under <see cref="StringComparison.Ordinal"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the plan contains lossy items but the provided token is
    /// missing, empty, or does not match the expected token.
    /// </exception>
    public static void ValidateAcceptDataLossToken(
        IReadOnlyList<ConversionLossyPlanItem> lossyItems,
        string? providedToken)
    {
        ArgumentNullException.ThrowIfNull(lossyItems);
        if (lossyItems.Count == 0)
            return; // no lossy work — no token required.

        var expected = ComputeAcceptDataLossToken(lossyItems);
        if (string.IsNullOrEmpty(providedToken))
            throw new InvalidOperationException(
                "Lossy conversion plan rejected: AcceptDataLossToken is required but was not provided.");

        if (!string.Equals(expected, providedToken, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Lossy conversion plan rejected: AcceptDataLossToken does not match the planned lossy items. "
                + "Recompute the plan and forward the new token.");
    }
}
