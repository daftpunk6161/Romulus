using RomCleanup.Infrastructure.Tools;
using System.Diagnostics;
using Xunit;

namespace RomCleanup.Tests;

public sealed class ToolRunnerAdapterTests : IDisposable
{
    private readonly string _tempDir;

    public ToolRunnerAdapterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RomCleanup_ToolHash_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void InvokeProcess_MissingHashFile_FailClosed()
    {
        var exe = GetExistingExecutable();
        var missingHashes = Path.Combine(_tempDir, "missing-tool-hashes.json");

        var runner = new ToolRunnerAdapter(missingHashes, allowInsecureHashBypass: false);
        var result = runner.InvokeProcess(exe, new[] { "/?" }, "test");

        Assert.False(result.Success);
        Assert.Contains("hash verification failed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvokeProcess_ToolNotInAllowList_FailClosed()
    {
        var exe = GetExistingExecutable();
        var hashesPath = Path.Combine(_tempDir, "tool-hashes.json");
        File.WriteAllText(hashesPath, """
        {
          "Tools": {
            "other.exe": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
          }
        }
        """);

        var runner = new ToolRunnerAdapter(hashesPath, allowInsecureHashBypass: false);
        var result = runner.InvokeProcess(exe, new[] { "/?" }, "test");

        Assert.False(result.Success);
        Assert.Contains("hash verification failed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvokeProcess_BypassEnabled_AllowsExecutionPath()
    {
        var exe = GetExistingExecutable();
        var missingHashes = Path.Combine(_tempDir, "missing-tool-hashes.json");

        var runner = new ToolRunnerAdapter(missingHashes, allowInsecureHashBypass: true);
        var result = runner.InvokeProcess(exe, new[] { "/?" }, "test");

        Assert.DoesNotContain("hash verification failed", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvokeProcess_CancelledToken_StopsLongRunningProcess()
    {
        var exe = GetExistingExecutable();
        var missingHashes = Path.Combine(_tempDir, "missing-tool-hashes.json");
        var runner = new ToolRunnerAdapter(missingHashes, allowInsecureHashBypass: true, timeoutMinutes: 30);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var sw = Stopwatch.StartNew();

        var result = runner.InvokeProcess(
            exe,
            ["/c", "ping", "127.0.0.1", "-n", "20"],
            "test",
            TimeSpan.FromSeconds(30),
            cts.Token);

        sw.Stop();

        Assert.False(result.Success);
        Assert.Contains("cancel", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(8));
    }

    private static string GetExistingExecutable()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var candidates = new[]
        {
            Path.Combine(winDir, "System32", "cmd.exe"),
            Path.Combine(winDir, "System32", "where.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException("Kein bekanntes Test-Executable gefunden.");
    }
}
