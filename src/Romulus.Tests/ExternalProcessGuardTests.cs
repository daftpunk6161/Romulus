using System.Diagnostics;
using System.Reflection;
using Romulus.Infrastructure.Tools;
using Xunit;

namespace Romulus.Tests;

public sealed class ExternalProcessGuardTests
{
    [Fact]
    public void Track_LiveProcess_RegistersAndLeaseDetachDoesNotTerminateProcess()
    {
        var trackedBefore = ExternalProcessGuard.GetTrackedProcessCountForTests();
        using var process = StartLongRunningProcess();
        IDisposable? lease = null;

        try
        {
            lease = ExternalProcessGuard.Track(process, "guard-track-test");

            Assert.True(SpinWait.SpinUntil(
                () => ExternalProcessGuard.GetTrackedProcessCountForTests() > trackedBefore,
                TimeSpan.FromSeconds(2)));

            lease.Dispose();
            lease = null;

            Assert.True(SpinWait.SpinUntil(
                () => ExternalProcessGuard.GetTrackedProcessCountForTests() <= trackedBefore,
                TimeSpan.FromSeconds(2)));
            Assert.False(process.HasExited);
        }
        finally
        {
            lease?.Dispose();
            ExternalProcessGuard.TryTerminate(process, "guard-track-test-cleanup", TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void TryTerminate_LiveTrackedProcess_KillsAndDetaches()
    {
        var trackedBefore = ExternalProcessGuard.GetTrackedProcessCountForTests();
        using var process = StartLongRunningProcess();
        using var lease = ExternalProcessGuard.Track(process, "guard-terminate-test");

        Assert.True(SpinWait.SpinUntil(
            () => ExternalProcessGuard.GetTrackedProcessCountForTests() > trackedBefore,
            TimeSpan.FromSeconds(2)));

        ExternalProcessGuard.TryTerminate(process, "guard-terminate-test", TimeSpan.FromSeconds(5));

        Assert.True(process.HasExited);
        Assert.True(SpinWait.SpinUntil(
            () => ExternalProcessGuard.GetTrackedProcessCountForTests() <= trackedBefore,
            TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void KillAllTrackedProcesses_TerminatesLiveTrackedProcess()
    {
        var trackedBefore = ExternalProcessGuard.GetTrackedProcessCountForTests();
        using var process = StartLongRunningProcess();
        using var lease = ExternalProcessGuard.Track(process, "guard-kill-all-test");

        Assert.True(SpinWait.SpinUntil(
            () => ExternalProcessGuard.GetTrackedProcessCountForTests() > trackedBefore,
            TimeSpan.FromSeconds(2)));

        ExternalProcessGuard.KillAllTrackedProcesses("guard-kill-all-test");

        Assert.True(process.HasExited);
        Assert.True(SpinWait.SpinUntil(
            () => ExternalProcessGuard.GetTrackedProcessCountForTests() <= trackedBefore,
            TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void EmitDiagnostic_WhenLoggerThrows_FallsBackToTraceWithoutThrowing()
    {
        var marker = $"guard-diagnostic-{Guid.NewGuid():N}";
        using var writer = new StringWriter();
        using var listener = new TextWriterTraceListener(writer);
        Trace.Listeners.Add(listener);
        var previousAutoFlush = Trace.AutoFlush;
        Trace.AutoFlush = true;

        try
        {
            InvokeEmitDiagnostic(_ => throw new InvalidOperationException("logger failed"), marker);
            listener.Flush();

            var trace = writer.ToString();
            Assert.Contains(marker, trace, StringComparison.Ordinal);
            Assert.Contains("logger-failed: InvalidOperationException", trace, StringComparison.Ordinal);
        }
        finally
        {
            Trace.AutoFlush = previousAutoFlush;
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public void EmitDiagnostic_WithLogger_UsesProvidedLoggerAndDoesNotWriteTrace()
    {
        var marker = $"guard-logger-{Guid.NewGuid():N}";
        string? captured = null;
        using var writer = new StringWriter();
        using var listener = new TextWriterTraceListener(writer);
        Trace.Listeners.Add(listener);

        try
        {
            InvokeEmitDiagnostic(message => captured = message, marker);
            listener.Flush();

            Assert.Equal(marker, captured);
            Assert.DoesNotContain(marker, writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    private static Process StartLongRunningProcess()
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                Arguments = "/c ping 127.0.0.1 -n 30 > NUL",
                CreateNoWindow = true,
                UseShellExecute = false
            }
            : new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = "-c 'sleep 30'",
                UseShellExecute = false
            };

        return Process.Start(startInfo)
               ?? throw new InvalidOperationException("Failed to start long-running test process.");
    }

    private static void InvokeEmitDiagnostic(Action<string>? log, string message)
    {
        var method = typeof(ExternalProcessGuard).GetMethod(
            "EmitDiagnostic",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        method!.Invoke(null, [log, message]);
    }
}
