using System.Diagnostics;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;

namespace RomCleanup.Infrastructure.Conversion;

/// <summary>
/// Executes planned conversion steps and emits enriched conversion results.
/// </summary>
public sealed class ConversionExecutor(IEnumerable<IToolInvoker> invokers) : IConversionExecutor
{
    private readonly IReadOnlyList<IToolInvoker> _invokers = (invokers ?? throw new ArgumentNullException(nameof(invokers))).ToArray();

    public ConversionResult Execute(
        ConversionPlan plan,
        Action<ConversionStep, ConversionStepResult>? onStepComplete = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var totalWatch = Stopwatch.StartNew();

        if (plan.Safety == ConversionSafety.Blocked)
        {
            return BuildResult(
                plan,
                null,
                ConversionOutcome.Blocked,
                plan.SkipReason ?? "plan-blocked",
                0,
                VerificationStatus.NotAttempted,
                totalWatch.ElapsedMilliseconds);
        }

        if (!plan.IsExecutable)
        {
            return BuildResult(
                plan,
                null,
                ConversionOutcome.Skipped,
                plan.SkipReason ?? "plan-not-executable",
                0,
                VerificationStatus.NotAttempted,
                totalWatch.ElapsedMilliseconds);
        }

        var sourceDirectory = Path.GetDirectoryName(plan.SourcePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(plan.SourcePath);
        var currentInputPath = plan.SourcePath;
        var intermediateArtifacts = new List<string>();
        var exitCode = 0;
        var finalVerification = VerificationStatus.NotAttempted;

        foreach (var step in plan.Steps.OrderBy(s => s.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outputPath = BuildOutputPath(sourceDirectory, baseName, step);
            if (File.Exists(outputPath))
            {
                CleanupArtifacts(intermediateArtifacts);
                return BuildResult(
                    plan,
                    null,
                    ConversionOutcome.Skipped,
                    "target-exists",
                    0,
                    VerificationStatus.NotAttempted,
                    totalWatch.ElapsedMilliseconds);
            }

            var invoker = _invokers.FirstOrDefault(i => i.CanHandle(step.Capability));
            if (invoker is null)
            {
                CleanupArtifacts(intermediateArtifacts);
                return BuildResult(
                    plan,
                    null,
                    ConversionOutcome.Error,
                    $"invoker-not-found:{step.Capability.Tool.ToolName}",
                    -1,
                    VerificationStatus.VerifyNotAvailable,
                    totalWatch.ElapsedMilliseconds);
            }

            var invokeResult = invoker.Invoke(currentInputPath, outputPath, step.Capability, cancellationToken);
            exitCode = invokeResult.ExitCode;

            var verifyStatus = invokeResult.Verification == VerificationStatus.NotAttempted
                ? invoker.Verify(outputPath, step.Capability)
                : invokeResult.Verification;

            var stepResult = new ConversionStepResult(
                step.Order,
                outputPath,
                invokeResult.Success,
                verifyStatus,
                invokeResult.Success ? null : invokeResult.StdErr,
                invokeResult.DurationMs);
            onStepComplete?.Invoke(step, stepResult);

            if (!invokeResult.Success)
            {
                CleanupArtifacts(intermediateArtifacts);
                CleanupPath(outputPath);
                return BuildResult(
                    plan,
                    null,
                    ConversionOutcome.Error,
                    "conversion-step-failed",
                    exitCode,
                    verifyStatus,
                    totalWatch.ElapsedMilliseconds);
            }

            if (verifyStatus == VerificationStatus.VerifyFailed)
            {
                CleanupArtifacts(intermediateArtifacts);
                CleanupPath(outputPath);
                return BuildResult(
                    plan,
                    null,
                    ConversionOutcome.Error,
                    "verification-failed",
                    exitCode,
                    verifyStatus,
                    totalWatch.ElapsedMilliseconds);
            }

            finalVerification = verifyStatus;
            currentInputPath = outputPath;
            if (step.IsIntermediate)
                intermediateArtifacts.Add(outputPath);
        }

        CleanupArtifacts(intermediateArtifacts);
        totalWatch.Stop();

        return BuildResult(
            plan,
            currentInputPath,
            ConversionOutcome.Success,
            null,
            exitCode,
            finalVerification,
            totalWatch.ElapsedMilliseconds);
    }

    private static string BuildOutputPath(string sourceDirectory, string baseName, ConversionStep step)
    {
        if (!step.IsIntermediate)
            return Path.Combine(sourceDirectory, baseName + step.OutputExtension);

        return Path.Combine(sourceDirectory, $"{baseName}.tmp.step{step.Order + 1}{step.OutputExtension}");
    }

    private static void CleanupArtifacts(IEnumerable<string> artifacts)
    {
        foreach (var artifact in artifacts)
            CleanupPath(artifact);
    }

    private static void CleanupPath(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static ConversionResult BuildResult(
        ConversionPlan plan,
        string? targetPath,
        ConversionOutcome outcome,
        string? reason,
        int exitCode,
        VerificationStatus verification,
        long durationMs)
    {
        return new ConversionResult(plan.SourcePath, targetPath, outcome, reason, exitCode)
        {
            Plan = plan,
            SourceIntegrity = plan.SourceIntegrity,
            Safety = plan.Safety,
            VerificationResult = verification,
            DurationMs = durationMs
        };
    }
}
