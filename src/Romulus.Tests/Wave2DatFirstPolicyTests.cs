using System.IO;
using System.Linq;
using Romulus.Contracts.Models;
using Romulus.Core.Classification;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Wave 2 — T-W2-DAT-FIRST-ADR pin tests.
/// Acceptance gates from docs/plan/strategic-reduction-2026/plan.yaml + ADR-0023:
///   * docs/adrs/0023-dat-first-policy.md existiert mit Status: Accepted und
///     deklariert DAT-first als Default + Opt-In-Flag fuer Heuristik.
///   * RunOptions.AllowHeuristicFallback existiert und ist standardmaessig false.
///   * ConsoleDetectionResult.IsBestEffort ist true gdw. HasHardEvidence == false
///     (Single source of truth fuer Best-Effort-Status; GUI/CLI/API/Reports duerfen
///     den Status nicht parallel berechnen).
///   * DatRepositoryAdapter dokumentiert die DAT-first-Policy als Vertrag.
/// </summary>
public sealed class Wave2DatFirstPolicyTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null
               && !File.Exists(Path.Combine(dir.FullName, "src", "Romulus.sln")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!;
    }

    [Fact]
    public void Adr0023_ExistsWithAcceptedStatusAndDeclaresPolicy()
    {
        var path = Path.Combine(RepoRoot().FullName, "docs", "adrs", "0023-dat-first-policy.md");
        Assert.True(File.Exists(path), $"ADR-0023 fehlt: {path}");
        var src = File.ReadAllText(path);

        Assert.Contains("ADR-0023", src, System.StringComparison.Ordinal);
        Assert.Contains("## Status", src, System.StringComparison.Ordinal);
        Assert.Contains("Accepted", src, System.StringComparison.Ordinal);
        // Default-Verhalten muss explizit benannt sein
        Assert.Contains("DAT-first", src, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Default", src, System.StringComparison.OrdinalIgnoreCase);
        // Opt-In-Flag muss namentlich genannt sein
        Assert.Contains("AllowHeuristicFallback", src, System.StringComparison.Ordinal);
        // Best-Effort-Sichtbarkeit
        Assert.Contains("IsBestEffort", src, System.StringComparison.Ordinal);
        // Reaktivierungs-Bedingung
        Assert.Contains("Reaktivierungs-Bedingung", src, System.StringComparison.Ordinal);
    }

    [Fact]
    public void RunOptions_AllowHeuristicFallback_DefaultsToFalse()
    {
        var options = new RunOptions();
        Assert.False(options.AllowHeuristicFallback,
            "ADR-0023: AllowHeuristicFallback muss standardmaessig false sein (DAT-first Default).");
    }

    [Fact]
    public void RunOptions_AllowHeuristicFallback_CanBeExplicitlyOptedIn()
    {
        var options = new RunOptions { AllowHeuristicFallback = true };
        Assert.True(options.AllowHeuristicFallback);
    }

    [Fact]
    public void ConsoleDetectionResult_IsBestEffort_TrueWhenNoHardEvidence()
    {
        var result = new ConsoleDetectionResult(
            ConsoleKey: "PS1",
            Confidence: 60,
            Hypotheses: new[]
            {
                new DetectionHypothesis("PS1", 60, DetectionSource.FolderName, "folder=PS1"),
            },
            HasConflict: false,
            ConflictDetail: null,
            HasHardEvidence: false,
            IsSoftOnly: true);

        Assert.True(result.IsBestEffort,
            "ADR-0023: ohne HasHardEvidence muss IsBestEffort true sein.");
    }

    [Fact]
    public void ConsoleDetectionResult_IsBestEffort_FalseWhenHardEvidencePresent()
    {
        var result = new ConsoleDetectionResult(
            ConsoleKey: "PS1",
            Confidence: 100,
            Hypotheses: new[]
            {
                new DetectionHypothesis("PS1", 100, DetectionSource.DatHash, "hash=abc"),
            },
            HasConflict: false,
            ConflictDetail: null,
            HasHardEvidence: true,
            IsSoftOnly: false);

        Assert.False(result.IsBestEffort,
            "ADR-0023: mit HasHardEvidence darf IsBestEffort nicht true sein.");
    }

    [Fact]
    public void ConsoleDetectionResult_Unknown_IsBestEffortByDefinition()
    {
        // Unknown hat keine harte Evidenz → Best-Effort = true.
        Assert.True(ConsoleDetectionResult.Unknown.IsBestEffort);
    }

    [Fact]
    public void DatRepositoryAdapter_SourceDocumentsDatFirstPolicy()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            "src", "Romulus.Infrastructure", "Dat", "DatRepositoryAdapter.cs");
        Assert.True(File.Exists(path));
        var src = File.ReadAllText(path);

        Assert.Contains("ADR-0023", src, System.StringComparison.Ordinal);
        Assert.Contains("AllowHeuristicFallback", src, System.StringComparison.Ordinal);
        Assert.Contains("DAT-First", src, System.StringComparison.OrdinalIgnoreCase);
    }
}
