using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Conversion;
using RomCleanup.Contracts.Ports;
using Xunit;

namespace RomCleanup.Tests;

public sealed class ConversionPipelineTests
{
    // =========================================================================
    //  DiskSpaceCheck Tests
    // =========================================================================

    [Fact]
    public void CheckDiskSpace_NonExistentFile_ReturnsFalse()
    {
        var result = ConversionPipeline.CheckDiskSpace(
            @"C:\nonexistent\file.iso", @"C:\temp");
        Assert.False(result.Ok);
        Assert.Contains("not found", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckDiskSpace_ExistingFile_ReturnsOk()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[1024]);
            var result = ConversionPipeline.CheckDiskSpace(tempFile, Path.GetTempPath());
            Assert.True(result.Ok);
            Assert.True(result.AvailableBytes > 0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // =========================================================================
    //  Pipeline Building Tests
    // =========================================================================

    [Fact]
    public void BuildCsoToChdPipeline_HasTwoSteps()
    {
        var pipeline = ConversionPipeline.BuildCsoToChdPipeline(
            @"D:\roms\game.cso", @"D:\output");

        Assert.Equal(2, pipeline.Steps.Count);
        Assert.Equal("ciso", pipeline.Steps[0].Tool);
        Assert.Equal("decompress", pipeline.Steps[0].Action);
        Assert.True(pipeline.Steps[0].IsTemp); // intermediate ISO is temp
        Assert.Equal("chdman", pipeline.Steps[1].Tool);
        Assert.Equal("createcd", pipeline.Steps[1].Action);
        Assert.False(pipeline.Steps[1].IsTemp); // final CHD is NOT temp
    }

    [Fact]
    public void BuildCsoToChdPipeline_CorrectPaths()
    {
        var pipeline = ConversionPipeline.BuildCsoToChdPipeline(
            @"D:\roms\game.cso", @"D:\output");

        Assert.EndsWith(".iso", pipeline.Steps[0].Output);
        Assert.EndsWith(".chd", pipeline.Steps[1].Output);
        Assert.Equal(pipeline.Steps[0].Output, pipeline.Steps[1].Input);
    }

    // =========================================================================
    //  Pipeline Execution Tests (DryRun)
    // =========================================================================

    [Fact]
    public void Execute_DryRun_AllStepsSkipped()
    {
        var tools = new FakeToolRunner();
        var fs = new FakeFs();
        var cvPipeline = new ConversionPipeline(tools, fs);

        var def = ConversionPipeline.BuildCsoToChdPipeline(@"D:\game.cso", @"D:\out");
        var results = cvPipeline.Execute(def, mode: "DryRun");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Skipped));
        Assert.All(results, r => Assert.Equal("dryrun", r.Status));
    }

    [Fact]
    public void Execute_Cancelled_CleansUpTempFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cvt_cancel_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create a source file so disk space check passes
            var sourceFile = Path.Combine(tempDir, "game.cso");
            File.WriteAllBytes(sourceFile, new byte[64]);

            var tempIso = Path.Combine(tempDir, "game.iso");
            var finalChd = Path.Combine(tempDir, "game.chd");

            var cts = new CancellationTokenSource();
            int callCount = 0;

            var tools = new CallbackToolRunner((path, args, label) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First step succeeds — create the temp file on disk
                    File.WriteAllText(tempIso, "temp iso data");
                    // Cancel so the next iteration throws
                    cts.Cancel();
                    return new ToolResult(0, "ok", true);
                }
                return new ToolResult(0, "ok", true);
            });

            var fs = new FakeFs();
            var pipeline = new ConversionPipeline(tools, fs);

            var def = new ConversionPipelineDef
            {
                SourcePath = sourceFile,
                Steps =
                [
                    new ConversionPipelineStep { Tool = "ciso", Action = "decompress", Input = sourceFile, Output = tempIso, IsTemp = true },
                    new ConversionPipelineStep { Tool = "chdman", Action = "createcd", Input = tempIso, Output = finalChd, IsTemp = false }
                ],
                CleanupTemps = true
            };

            Assert.NotEmpty(pipeline.Execute(def, mode: "Move", ct: cts.Token));

            // The temp file should have been cleaned up by the finally block
            Assert.False(File.Exists(tempIso), "Temp file should be cleaned up even on cancellation");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    // Fakes
    private sealed class FakeToolRunner : IToolRunner
    {
        public string? FindTool(string toolName) => $@"C:\tools\{toolName}.exe";
        public ToolResult InvokeProcess(string filePath, string[] arguments, string? errorLabel = null)
            => new(0, "success", true);
        public ToolResult Invoke7z(string sevenZipPath, string[] arguments)
            => new(0, "success", true);
    }

    private sealed class CallbackToolRunner : IToolRunner
    {
        private readonly Func<string, string[], string?, ToolResult> _callback;
        public CallbackToolRunner(Func<string, string[], string?, ToolResult> callback) => _callback = callback;
        public string? FindTool(string toolName) => $@"C:\tools\{toolName}.exe";
        public ToolResult InvokeProcess(string filePath, string[] arguments, string? errorLabel = null)
            => _callback(filePath, arguments, errorLabel);
        public ToolResult Invoke7z(string sevenZipPath, string[] arguments)
            => new(0, "success", true);
    }

    private sealed class FakeFs : IFileSystem
    {
        public bool TestPath(string literalPath, string pathType = "Any") => true;
        public string EnsureDirectory(string path) => path;
        public IReadOnlyList<string> GetFilesSafe(string root, IEnumerable<string>? extensions = null) => [];
        public bool MoveItemSafely(string src, string dest) => true;
        public string? ResolveChildPathWithinRoot(string rootPath, string relativePath)
            => Path.Combine(rootPath, relativePath);
        public bool IsReparsePoint(string path) => false;
        public void DeleteFile(string path) { }
        public void CopyFile(string sourcePath, string destinationPath, bool overwrite = false) { }
    }
}
