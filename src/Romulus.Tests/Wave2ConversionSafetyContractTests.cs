using System;
using System.Collections.Generic;
using Romulus.Contracts.Models;
using Romulus.Core.Conversion;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Wave 2 — T-W2-CONVERSION-SAFETY-CONTRACT pin tests.
/// Acceptance gates from plan.yaml:
///   * RunOptions.AcceptDataLossToken existiert (default null).
///   * RunResult.PendingLossyToken existiert (default null).
///   * Lossy-Plan ohne Token wirft InvalidOperationException.
///   * Token deterministisch (gleicher Plan ⇒ gleicher Hash; kein Zeitstempel).
///   * Token unterscheidet sich, sobald sich der Plan unterscheidet.
///   * Token-Vergleich Ordinal (case-sensitive).
///   * Empty plan ⇒ Token irrelevant, keine Exception.
/// </summary>
public sealed class Wave2ConversionSafetyContractTests
{
    private static IReadOnlyList<ConversionLossyPlanItem> Sample()
    {
        return new[]
        {
            new ConversionLossyPlanItem("C:/r/a.iso", "iso", "cso"),
            new ConversionLossyPlanItem("C:/r/b.iso", "iso", "cso"),
        };
    }

    [Fact]
    public void RunOptions_AcceptDataLossToken_DefaultsToNull()
    {
        var opts = new RunOptions();
        Assert.Null(opts.AcceptDataLossToken);
    }

    [Fact]
    public void RunResult_PendingLossyToken_DefaultsToNull()
    {
        var r = new RunResult();
        Assert.Null(r.PendingLossyToken);
    }

    [Fact]
    public void ComputeToken_EmptyPlan_ReturnsNull()
    {
        var token = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(Array.Empty<ConversionLossyPlanItem>());
        Assert.Null(token);
    }

    [Fact]
    public void ComputeToken_NonEmptyPlan_ReturnsLowercaseHexSha256()
    {
        var token = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(Sample())!;
        Assert.NotNull(token);
        Assert.Equal(64, token.Length); // SHA-256 hex
        foreach (var c in token)
        {
            Assert.True(c is (>= '0' and <= '9') or (>= 'a' and <= 'f'),
                $"Token must be lowercase hex; got '{c}'.");
        }
    }

    [Fact]
    public void ComputeToken_IsDeterministic()
    {
        var t1 = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(Sample());
        var t2 = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(Sample());
        Assert.Equal(t1, t2);
    }

    [Fact]
    public void ComputeToken_OrderingDoesNotChangeResult()
    {
        var ordered = new[]
        {
            new ConversionLossyPlanItem("C:/r/a.iso", "iso", "cso"),
            new ConversionLossyPlanItem("C:/r/b.iso", "iso", "cso"),
        };
        var reversed = new[]
        {
            new ConversionLossyPlanItem("C:/r/b.iso", "iso", "cso"),
            new ConversionLossyPlanItem("C:/r/a.iso", "iso", "cso"),
        };
        Assert.Equal(
            ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(ordered),
            ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(reversed));
    }

    [Fact]
    public void ComputeToken_DiffersBetweenDifferentPlans()
    {
        var a = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(Sample());
        var b = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(new[]
        {
            new ConversionLossyPlanItem("C:/r/a.iso", "iso", "cso"),
            new ConversionLossyPlanItem("C:/r/c.iso", "iso", "cso"),
        });
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Validate_LossyPlanWithoutToken_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ConversionLossyTokenPolicy.ValidateAcceptDataLossToken(Sample(), providedToken: null));
        Assert.Contains("AcceptDataLossToken", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_LossyPlanWithEmptyToken_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ConversionLossyTokenPolicy.ValidateAcceptDataLossToken(Sample(), providedToken: ""));
    }

    [Fact]
    public void Validate_LossyPlanWithMismatchedToken_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ConversionLossyTokenPolicy.ValidateAcceptDataLossToken(Sample(), providedToken: new string('a', 64)));
    }

    [Fact]
    public void Validate_LossyPlanWithCorrectToken_DoesNotThrow()
    {
        var token = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(Sample());
        // expect no exception
        ConversionLossyTokenPolicy.ValidateAcceptDataLossToken(Sample(), providedToken: token);
    }

    [Fact]
    public void Validate_TokenComparisonIsOrdinal_CaseSensitive()
    {
        var token = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(Sample())!;
        var upper = token.ToUpperInvariant();
        Assert.NotEqual(token, upper); // sanity: tokens are lowercase hex
        Assert.Throws<InvalidOperationException>(() =>
            ConversionLossyTokenPolicy.ValidateAcceptDataLossToken(Sample(), providedToken: upper));
    }

    [Fact]
    public void Validate_EmptyPlan_DoesNotRequireToken()
    {
        // no exception expected; token irrelevant when no lossy items.
        ConversionLossyTokenPolicy.ValidateAcceptDataLossToken(
            Array.Empty<ConversionLossyPlanItem>(),
            providedToken: null);
    }
}
