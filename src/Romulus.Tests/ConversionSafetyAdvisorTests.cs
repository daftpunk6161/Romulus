using System.Reflection;
using Romulus.CLI;
using Romulus.Contracts.Models;
using Romulus.Core.Conversion;
using Romulus.Infrastructure.Conversion;
using Romulus.Infrastructure.Orchestration;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// T-W5-CONVERSION-SAFETY-ADVISOR — pin tests for the lossy-conversion gate.
///
/// Surface plumbing:
///  - CLI exposes <c>--accept-data-loss &lt;token&gt;</c>.
///  - <see cref="RunConfigurationDraft"/> + <see cref="RunConfigurationExplicitness"/>
///    carry the field; reflection drift guard prevents silent removal.
///
/// Production wiring:
///  - <see cref="RunResultBuilder.PendingLossyToken"/> exists and is propagated
///    by <see cref="RunResult.PendingLossyToken"/> after Build().
///  - The orchestrator helper <see cref="ApplyConversionReportAccessor"/> populates
///    <c>PendingLossyToken</c> from lossy <see cref="ConversionResult"/>s.
///  - <see cref="ConversionLossyBatchGate"/> rejects lossy batches without a
///    matching token and is a no-op when no lossy items are present.
///
/// These pins lock the Single Source of Truth: token enforcement uses
/// <see cref="ConversionLossyTokenPolicy"/> exclusively; surfaces only forward
/// the value but never recompute it.
/// </summary>
public sealed class ConversionSafetyAdvisorTests
{
    // ---------------------------------------------------------------- surfaces

    [Fact]
    public void Cli_AcceptDataLossFlag_OnRunCommand_SetsTokenOnCliRunOptions()
    {
        // run command: `--accept-data-loss <token>`
        var result = CliArgsParser.Parse(["--roots", Path.GetTempPath(), "--accept-data-loss", "abc123"]);
        Assert.Equal(CliCommand.Run, result.Command);
        Assert.Equal("abc123", result.Options!.AcceptDataLossToken);
        Assert.True(result.Options!.AcceptDataLossTokenExplicit);
    }

    [Fact]
    public void Cli_AcceptDataLossFlag_OnConvertSubcommand_SetsTokenOnCliRunOptions()
    {
        var inputFile = Path.Combine(Path.GetTempPath(), "Romulus_safety_" + Guid.NewGuid().ToString("N") + ".zip");
        File.WriteAllBytes(inputFile, [0x00]);
        try
        {
            var result = CliArgsParser.Parse(["convert", "--input", inputFile, "--accept-data-loss", "deadbeef"]);
            Assert.Equal(CliCommand.Convert, result.Command);
            Assert.Equal("deadbeef", result.Options!.AcceptDataLossToken);
            Assert.True(result.Options!.AcceptDataLossTokenExplicit);
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
        }
    }

    [Fact]
    public void RunConfigurationDraft_HasAcceptDataLossTokenInitProperty()
    {
        var prop = typeof(RunConfigurationDraft).GetProperty(
            "AcceptDataLossToken",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
        Assert.True(prop.CanWrite, "AcceptDataLossToken must be init-settable on the draft.");
    }

    [Fact]
    public void RunConfigurationExplicitness_HasAcceptDataLossTokenProperty()
    {
        var prop = typeof(RunConfigurationExplicitness).GetProperty(
            "AcceptDataLossToken",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.Equal(typeof(bool), prop!.PropertyType);
    }

    // ---------------------------------------------------------------- gate

    [Fact]
    public void ConversionLossyBatchGate_NoLossyItems_DoesNotThrow_EvenWithoutToken()
    {
        // Empty list -> token is irrelevant.
        ConversionLossyBatchGate.Enforce(Array.Empty<ConversionLossyPlanItem>(), acceptDataLossToken: null);
    }

    [Fact]
    public void ConversionLossyBatchGate_LossyItems_WithoutToken_Throws()
    {
        var lossy = new[]
        {
            new ConversionLossyPlanItem("C:/r/a.iso", "iso", "cso"),
            new ConversionLossyPlanItem("C:/r/b.iso", "iso", "cso")
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => ConversionLossyBatchGate.Enforce(lossy, acceptDataLossToken: null));
        Assert.Contains("AcceptDataLossToken", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConversionLossyBatchGate_LossyItems_WithMatchingToken_DoesNotThrow()
    {
        var lossy = new[]
        {
            new ConversionLossyPlanItem("C:/r/a.iso", "iso", "cso"),
            new ConversionLossyPlanItem("C:/r/b.iso", "iso", "cso")
        };
        var token = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(lossy);
        Assert.NotNull(token);

        ConversionLossyBatchGate.Enforce(lossy, acceptDataLossToken: token);
    }

    [Fact]
    public void ConversionLossyBatchGate_CollectLossyFromResults_FiltersNonLossy()
    {
        var lossless = MakeResult("C:/r/a.iso", lossy: false, targetExt: ".chd");
        var lossy = MakeResult("C:/r/b.iso", lossy: true, targetExt: ".cso");

        var collected = ConversionLossyBatchGate.CollectLossyFromResults([lossless, lossy]);

        Assert.Single(collected);
        Assert.Equal("C:/r/b.iso", collected[0].SourcePath);
        Assert.Equal("iso", collected[0].SourceFormat);
        Assert.Equal("cso", collected[0].TargetFormat);
    }

    // ---------------------------------------------------------------- builder propagation

    [Fact]
    public void RunResultBuilder_PendingLossyToken_PropagatesIntoBuiltRunResult()
    {
        var builder = new RunResultBuilder
        {
            PendingLossyToken = "token-xyz"
        };

        var result = builder.Build();
        Assert.Equal("token-xyz", result.PendingLossyToken);
    }

    [Fact]
    public void ApplyConversionReport_PopulatesPendingLossyToken_WhenLossyResultsPresent()
    {
        var lossless = MakeResult("C:/r/a.iso", lossy: false, targetExt: ".chd");
        var lossy = MakeResult("C:/r/b.iso", lossy: true, targetExt: ".cso");
        var builder = new RunResultBuilder();

        ApplyConversionReportAccessor.Invoke([lossless, lossy], builder);

        var expected = ConversionLossyTokenPolicy.ComputeAcceptDataLossToken(
            ConversionLossyBatchGate.CollectLossyFromResults([lossless, lossy]));
        Assert.NotNull(expected);
        Assert.Equal(expected, builder.PendingLossyToken);
    }

    [Fact]
    public void ApplyConversionReport_PendingLossyTokenStaysNull_WhenNoLossyResults()
    {
        var lossless = MakeResult("C:/r/a.iso", lossy: false, targetExt: ".chd");
        var builder = new RunResultBuilder();

        ApplyConversionReportAccessor.Invoke([lossless], builder);

        Assert.Null(builder.PendingLossyToken);
    }

    // ---------------------------------------------------------------- helpers

    private static ConversionResult MakeResult(string sourcePath, bool lossy, string targetExt)
    {
        var capability = new ConversionCapability
        {
            SourceExtension = ".iso",
            TargetExtension = targetExt,
            Tool = new ToolRequirement { ToolName = "test" },
            Command = "convert",
            Verification = VerificationMethod.None,
            ResultIntegrity = lossy ? SourceIntegrity.Lossy : SourceIntegrity.Lossless,
            Lossless = !lossy,
            Cost = 1
        };
        var step = new ConversionStep
        {
            Order = 0,
            InputExtension = ".iso",
            OutputExtension = targetExt,
            Capability = capability,
            IsIntermediate = false
        };
        var plan = new ConversionPlan
        {
            SourcePath = sourcePath,
            ConsoleKey = "PSP",
            Policy = ConversionPolicy.Auto,
            SourceIntegrity = lossy ? SourceIntegrity.Lossy : SourceIntegrity.Lossless,
            Safety = lossy ? ConversionSafety.Acceptable : ConversionSafety.Safe,
            Steps = [step]
        };
        return new ConversionResult(sourcePath, sourcePath + targetExt, ConversionOutcome.Success)
        {
            Plan = plan,
            SourceIntegrity = plan.SourceIntegrity,
            Safety = plan.Safety,
            VerificationResult = VerificationStatus.NotAttempted
        };
    }
}

/// <summary>
/// Reflection bridge to the internal RunOrchestrator helper that builds the
/// ConversionReport. Keeps the test independent of the partial-class file layout.
/// </summary>
internal static class ApplyConversionReportAccessor
{
    public static void Invoke(IReadOnlyList<ConversionResult> results, RunResultBuilder builder)
    {
        // The helper lives as `internal static` on the partial RunOrchestrator type.
        var orchestratorType = typeof(RunResultBuilder).Assembly
            .GetType("Romulus.Infrastructure.Orchestration.RunOrchestrator", throwOnError: true)!;
        var method = orchestratorType.GetMethod(
            "ApplyConversionReport",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
            throw new MissingMethodException("RunOrchestrator", "ApplyConversionReport");
        method.Invoke(null, [results, builder]);
    }
}
