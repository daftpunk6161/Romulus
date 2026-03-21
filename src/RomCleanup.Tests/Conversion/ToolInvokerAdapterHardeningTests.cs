using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Conversion;
using Xunit;

namespace RomCleanup.Tests.Conversion;

public sealed class ToolInvokerAdapterHardeningTests
{
    [Fact]
    public void Invoke_SourceMissing_ReturnsError()
    {
        var runner = new RecordingToolRunner();
        var invoker = new ToolInvokerAdapter(runner);

        var result = invoker.Invoke(
            Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.iso"),
            Path.Combine(Path.GetTempPath(), $"out_{Guid.NewGuid():N}.chd"),
            Capability("chdman", "createcd"));

        Assert.False(result.Success);
        Assert.Equal("source-not-found", result.StdErr);
        Assert.False(runner.WasInvokeCalled);
    }

    [Fact]
    public void Invoke_CommandWithPathSeparator_ReturnsInvalidCommand()
    {
        var source = CreateTempFile(".iso");
        var target = Path.ChangeExtension(source, ".chd");
        var runner = new RecordingToolRunner();
        var invoker = new ToolInvokerAdapter(runner);

        try
        {
            var result = invoker.Invoke(source, target, Capability("chdman", "..\\createcd"));

            Assert.False(result.Success);
            Assert.Equal("invalid-command", result.StdErr);
            Assert.False(runner.WasInvokeCalled);
        }
        finally
        {
            if (File.Exists(source))
                File.Delete(source);
            if (File.Exists(target))
                File.Delete(target);
        }
    }

    [Fact]
    public void Invoke_OnlyUsesFirstCommandToken()
    {
        var source = CreateTempFile(".iso");
        var target = Path.ChangeExtension(source, ".chd");
        var runner = new RecordingToolRunner();
        var invoker = new ToolInvokerAdapter(runner);

        try
        {
            var result = invoker.Invoke(source, target, Capability("chdman", "createcd --bogus"));

            Assert.True(result.Success);
            Assert.NotNull(runner.LastArgs);
            Assert.Equal("createcd", runner.LastArgs![0]);
        }
        finally
        {
            if (File.Exists(source))
                File.Delete(source);
            if (File.Exists(target))
                File.Delete(target);
        }
    }

    private static ConversionCapability Capability(string toolName, string command)
    {
        return new ConversionCapability
        {
            SourceExtension = ".iso",
            TargetExtension = ".chd",
            Tool = new ToolRequirement { ToolName = toolName },
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
        var path = Path.Combine(Path.GetTempPath(), $"tool_invoker_hardening_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        return path;
    }

    private sealed class RecordingToolRunner : IToolRunner
    {
        public bool WasInvokeCalled { get; private set; }
        public string[]? LastArgs { get; private set; }

        public string? FindTool(string toolName) => $"C:\\mock\\{toolName}.exe";

        public ToolResult InvokeProcess(string filePath, string[] arguments, string? errorLabel = null)
        {
            WasInvokeCalled = true;
            LastArgs = arguments;

            var outIndex = Array.IndexOf(arguments, "-o");
            if (outIndex >= 0 && outIndex < arguments.Length - 1)
            {
                var outputPath = arguments[outIndex + 1];
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(outputPath, [1, 2, 3, 4]);
            }

            return new ToolResult(0, "ok", true);
        }

        public ToolResult Invoke7z(string sevenZipPath, string[] arguments)
            => new(0, "ok", true);
    }
}
