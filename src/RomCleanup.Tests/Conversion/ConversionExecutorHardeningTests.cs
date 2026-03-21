using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Conversion;
using Xunit;

namespace RomCleanup.Tests.Conversion;

public sealed class ConversionExecutorHardeningTests
{
    [Fact]
    public void Execute_SourceMissing_ReturnsError()
    {
        var executor = new ConversionExecutor([new PassThroughInvoker()]);
        var plan = BuildPlan(Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.iso"));

        var result = executor.Execute(plan);

        Assert.Equal(ConversionOutcome.Error, result.Outcome);
        Assert.Equal("source-not-found", result.Reason);
    }

    [Fact]
    public void Execute_NonContiguousStepOrder_ReturnsError()
    {
        var source = CreateTempFile(".iso");
        try
        {
            var capability = Capability(".iso", ".chd", "chdman", "createcd");
            var plan = new ConversionPlan
            {
                SourcePath = source,
                ConsoleKey = "PS1",
                Policy = ConversionPolicy.Auto,
                SourceIntegrity = SourceIntegrity.Lossless,
                Safety = ConversionSafety.Safe,
                Steps =
                [
                    new ConversionStep
                    {
                        Order = 1,
                        InputExtension = ".iso",
                        OutputExtension = ".chd",
                        Capability = capability,
                        IsIntermediate = false
                    }
                ]
            };

            var executor = new ConversionExecutor([new PassThroughInvoker()]);
            var result = executor.Execute(plan);

            Assert.Equal(ConversionOutcome.Error, result.Outcome);
            Assert.Equal("invalid-step-order", result.Reason);
        }
        finally
        {
            if (File.Exists(source))
                File.Delete(source);
        }
    }

    [Fact]
    public void Execute_ExceptionInLaterStep_CleansIntermediateArtifacts()
    {
        var source = CreateTempFile(".cso");
        var sourceDir = Path.GetDirectoryName(source)!;
        var baseName = Path.GetFileNameWithoutExtension(source);
        var intermediatePath = Path.Combine(sourceDir, $"{baseName}.tmp.step1.iso");

        try
        {
            var plan = new ConversionPlan
            {
                SourcePath = source,
                ConsoleKey = "PSP",
                Policy = ConversionPolicy.Auto,
                SourceIntegrity = SourceIntegrity.Lossy,
                Safety = ConversionSafety.Acceptable,
                Steps =
                [
                    new ConversionStep
                    {
                        Order = 0,
                        InputExtension = ".cso",
                        OutputExtension = ".iso",
                        Capability = Capability(".cso", ".iso", "ciso", "decompress"),
                        IsIntermediate = true
                    },
                    new ConversionStep
                    {
                        Order = 1,
                        InputExtension = ".iso",
                        OutputExtension = ".chd",
                        Capability = Capability(".iso", ".chd", "chdman", "throw"),
                        IsIntermediate = false
                    }
                ]
            };

            var executor = new ConversionExecutor([new ThrowOnCommandInvoker("throw")]);

            Assert.Throws<InvalidOperationException>(() => executor.Execute(plan));
            Assert.False(File.Exists(intermediatePath));
        }
        finally
        {
            if (File.Exists(source))
                File.Delete(source);

            var finalPath = Path.ChangeExtension(source, ".chd");
            if (File.Exists(finalPath))
                File.Delete(finalPath);
            if (File.Exists(intermediatePath))
                File.Delete(intermediatePath);
        }
    }

    private static ConversionPlan BuildPlan(string sourcePath)
    {
        return new ConversionPlan
        {
            SourcePath = sourcePath,
            ConsoleKey = "PS1",
            Policy = ConversionPolicy.Auto,
            SourceIntegrity = SourceIntegrity.Lossless,
            Safety = ConversionSafety.Safe,
            Steps =
            [
                new ConversionStep
                {
                    Order = 0,
                    InputExtension = ".iso",
                    OutputExtension = ".chd",
                    Capability = Capability(".iso", ".chd", "chdman", "createcd"),
                    IsIntermediate = false
                }
            ]
        };
    }

    private static ConversionCapability Capability(string source, string target, string tool, string command)
    {
        return new ConversionCapability
        {
            SourceExtension = source,
            TargetExtension = target,
            Tool = new ToolRequirement { ToolName = tool },
            Command = command,
            ResultIntegrity = SourceIntegrity.Lossless,
            Lossless = true,
            Cost = 0,
            Verification = VerificationMethod.FileExistenceCheck,
            Condition = ConversionCondition.None
        };
    }

    private static string CreateTempFile(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"conv_exec_hardening_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        return path;
    }

    private sealed class PassThroughInvoker : IToolInvoker
    {
        public bool CanHandle(ConversionCapability capability) => true;

        public ToolInvocationResult Invoke(string sourcePath, string targetPath, ConversionCapability capability, CancellationToken cancellationToken = default)
        {
            File.Copy(sourcePath, targetPath, overwrite: false);
            return new ToolInvocationResult(true, targetPath, 0, "ok", null, 1, VerificationStatus.NotAttempted);
        }

        public VerificationStatus Verify(string targetPath, ConversionCapability capability)
            => File.Exists(targetPath) ? VerificationStatus.Verified : VerificationStatus.VerifyFailed;
    }

    private sealed class ThrowOnCommandInvoker(string commandToThrow) : IToolInvoker
    {
        public bool CanHandle(ConversionCapability capability) => true;

        public ToolInvocationResult Invoke(string sourcePath, string targetPath, ConversionCapability capability, CancellationToken cancellationToken = default)
        {
            if (string.Equals(capability.Command, commandToThrow, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("forced");

            File.Copy(sourcePath, targetPath, overwrite: false);
            return new ToolInvocationResult(true, targetPath, 0, "ok", null, 1, VerificationStatus.NotAttempted);
        }

        public VerificationStatus Verify(string targetPath, ConversionCapability capability)
            => VerificationStatus.Verified;
    }
}
