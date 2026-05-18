using System.Reflection;
using Romulus.Api;
using Romulus.Contracts;
using Romulus.Infrastructure.Audit;
using Romulus.Infrastructure.FileSystem;
using Xunit;

namespace Romulus.Tests;

public sealed class ApiAutomationServiceTests
{
    [Fact]
    public async Task TriggerRunAsync_WhenAutomationStopped_DoesNotCreateRun()
    {
        var executorCalls = 0;
        using var manager = new RunManager(new FileSystemAdapter(), new AuditCsvStore(), (_, _, _, _) =>
        {
            Interlocked.Increment(ref executorCalls);
            return CompletedOutcome();
        });
        using var service = new ApiAutomationService(manager);

        await InvokeTriggerAsync(service, "watch");

        Assert.Equal(0, Volatile.Read(ref executorCalls));
        Assert.Null(manager.GetActive());
        var status = service.GetStatus();
        Assert.False(status.Active);
        Assert.Null(status.LastTriggerUtc);
        Assert.Null(status.LastRunId);
    }

    [Fact]
    public async Task TriggerRunAsync_UsesClonedStartRequestAndOwnerBinding()
    {
        var root = CreateTempDirectory();
        var mutatedRoot = CreateTempDirectory();
        var capturedRun = new TaskCompletionSource<RunRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var manager = new RunManager(new FileSystemAdapter(), new AuditCsvStore(), (run, _, _, _) =>
        {
            capturedRun.TrySetResult(run);
            return CompletedOutcome();
        });
        using var service = new ApiAutomationService(manager);

        try
        {
            var request = new RunRequest
            {
                Roots = new[] { root },
                PreferRegions = new[] { "EU" },
                Extensions = new[] { "zip" },
                EnableDat = true,
                EnableDatAudit = true,
                ConflictPolicy = "Rename"
            };

            var startStatus = service.Start(
                request,
                RunConstants.ModeMove,
                ownerClientId: "automation-owner",
                debounceSeconds: 0,
                intervalMinutes: null,
                cronExpression: null);
            Assert.True(startStatus.Active);

            request.Roots[0] = mutatedRoot;
            request.PreferRegions![0] = "JP";
            request.Extensions![0] = "iso";
            request.EnableDat = false;
            request.EnableDatAudit = false;

            await InvokeTriggerAsync(service, "watch");

            var completed = await WaitForAsync(capturedRun.Task);
            Assert.Equal(new[] { root }, completed.Roots);
            Assert.Equal(new[] { "EU" }, completed.PreferRegions);
            Assert.Equal(new[] { ".zip" }, completed.Extensions);
            Assert.True(completed.EnableDat);
            Assert.True(completed.EnableDatAudit);
            Assert.Equal(RunConstants.ModeMove, completed.Mode);
            Assert.Equal("automation-owner", completed.OwnerClientId);

            var status = service.GetStatus();
            Assert.Equal("watch", status.LastTriggerSource);
            Assert.Equal(completed.RunId, status.LastRunId);
            Assert.NotNull(status.LastTriggerUtc);
            Assert.Null(status.LastError);
            Assert.False(status.WatchPending);
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(mutatedRoot);
        }
    }

    [Theory]
    [InlineData("watch")]
    [InlineData("schedule")]
    public async Task TriggerRunAsync_WhenRunManagerBusy_DoesNotStartSecondRunAndMarksPending(string source)
    {
        var root = CreateTempDirectory();
        using var executorStarted = new ManualResetEventSlim(false);
        using var allowCompletion = new ManualResetEventSlim(false);
        var executorCalls = 0;
        using var manager = new RunManager(new FileSystemAdapter(), new AuditCsvStore(), (_, _, _, ct) =>
        {
            Interlocked.Increment(ref executorCalls);
            executorStarted.Set();
            allowCompletion.Wait(ct);
            return CompletedOutcome();
        });
        using var service = new ApiAutomationService(manager);

        try
        {
            var active = manager.TryCreateOrReuse(
                new RunRequest { Roots = new[] { root } },
                RunConstants.ModeDryRun,
                idempotencyKey: "active-run").Run!;
            Assert.True(executorStarted.Wait(TimeSpan.FromSeconds(5)));

            var startStatus = service.Start(
                new RunRequest { Roots = new[] { root } },
                RunConstants.ModeDryRun,
                ownerClientId: "automation-owner",
                debounceSeconds: 1,
                intervalMinutes: 15,
                cronExpression: null);
            Assert.True(startStatus.Active);

            await InvokeTriggerAsync(service, source);

            Assert.Equal(1, Volatile.Read(ref executorCalls));
            var status = service.GetStatus();
            Assert.Equal(active.RunId, status.LastRunId);
            Assert.Null(status.LastError);
            if (source == "watch")
            {
                Assert.True(status.WatchPending);
                Assert.False(status.SchedulePending);
            }
            else
            {
                Assert.False(status.WatchPending);
                Assert.True(status.SchedulePending);
            }
        }
        finally
        {
            allowCompletion.Set();
            if (manager.GetActive() is { } active)
                await manager.WaitForCompletion(active.RunId, timeout: TimeSpan.FromSeconds(5));

            DeleteDirectory(root);
        }
    }

    private static async Task InvokeTriggerAsync(ApiAutomationService service, string source)
    {
        var method = typeof(ApiAutomationService).GetMethod(
            "TriggerRunAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(service, new object[] { source }));
        await task;
    }

    private static RunExecutionOutcome CompletedOutcome()
        => new(RunConstants.StatusCompleted, new ApiRunResult
        {
            OrchestratorStatus = "ok",
            ExitCode = 0
        });

    private static async Task<T> WaitForAsync<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, completed);
        return await task;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "Romulus_ApiAutomation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best effort cleanup for test-owned temporary directories.
        }
    }
}
