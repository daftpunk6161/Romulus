using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Pipeline;
using Xunit;

namespace RomCleanup.Tests;

public class PipelineEngineTests
{
    [Fact]
    public void ExecuteStep_RunsAction()
    {
        bool ran = false;
        var step = new PipelineStep { Name = "test", Action = _ => ran = true };
        var ctx = new PipelineStepContext { Mode = "Move" };
        var outcome = PipelineEngine.ExecuteStep(step, ctx);
        Assert.True(ran);
        Assert.Equal("Completed", outcome.Status);
        Assert.Equal("test", outcome.StepName);
    }

    [Fact]
    public void ExecuteStep_DryRun_DoesNotRunAction()
    {
        bool ran = false;
        var step = new PipelineStep { Name = "test", Action = _ => ran = true };
        var ctx = new PipelineStepContext { Mode = "DryRun" };
        var outcome = PipelineEngine.ExecuteStep(step, ctx);
        Assert.False(ran);
        Assert.Equal("DryRun", outcome.Status);
    }

    [Fact]
    public void ExecuteStep_ConditionFalse_Skips()
    {
        bool ran = false;
        var step = new PipelineStep
        {
            Name = "skip",
            Action = _ => ran = true,
            Condition = ctx => ctx.PreviousSuccess == false
        };
        var ctx = new PipelineStepContext { Mode = "Move", PreviousSuccess = true };
        var outcome = PipelineEngine.ExecuteStep(step, ctx);
        Assert.False(ran);
        Assert.Equal("Skipped", outcome.Status);
    }

    [Fact]
    public void ExecuteStep_ConditionTrue_Runs()
    {
        bool ran = false;
        var step = new PipelineStep
        {
            Name = "go",
            Action = _ => ran = true,
            Condition = ctx => ctx.PreviousSuccess
        };
        var ctx = new PipelineStepContext { Mode = "Move", PreviousSuccess = true };
        var outcome = PipelineEngine.ExecuteStep(step, ctx);
        Assert.True(ran);
        Assert.Equal("Completed", outcome.Status);
    }

    [Fact]
    public void ExecuteStep_ActionThrows_StatusFailed()
    {
        var step = new PipelineStep { Name = "fail", Action = _ => throw new Exception("boom") };
        var ctx = new PipelineStepContext { Mode = "Move" };
        var outcome = PipelineEngine.ExecuteStep(step, ctx);
        Assert.Equal("Failed", outcome.Status);
        Assert.Contains("boom", outcome.Error);
    }

    [Fact]
    public void Execute_AllComplete()
    {
        int sum = 0;
        var pipeline = new PipelineDefinition
        {
            Name = "testPipeline",
            Steps = new List<PipelineStep>
            {
                new() { Name = "step1", Action = _ => sum += 1 },
                new() { Name = "step2", Action = _ => sum += 2 },
                new() { Name = "step3", Action = _ => sum += 4 }
            }
        };
        var ctx = new PipelineStepContext { Mode = "Move" };
        var result = PipelineEngine.Execute(pipeline, ctx);
        Assert.Equal(7, sum);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(3, result.CompletedSteps);
        Assert.Equal(0, result.FailedSteps);
    }

    [Fact]
    public void Execute_OnErrorStop_StopsAtFailure()
    {
        int sum = 0;
        var pipeline = new PipelineDefinition
        {
            Name = "stop",
            OnError = "stop",
            Steps = new List<PipelineStep>
            {
                new() { Name = "ok", Action = _ => sum += 1 },
                new() { Name = "fail", Action = _ => throw new Exception() },
                new() { Name = "never", Action = _ => sum += 100 }
            }
        };
        var ctx = new PipelineStepContext { Mode = "Move" };
        var result = PipelineEngine.Execute(pipeline, ctx);
        Assert.Equal(1, sum); // step3 never ran
        Assert.Equal("Failed", result.Status);
        Assert.Equal(1, result.CompletedSteps);
        Assert.Equal(1, result.FailedSteps);
        Assert.Equal(1, result.SkippedSteps);
    }

    [Fact]
    public void Execute_OnErrorContinue_RunsAll()
    {
        int sum = 0;
        var pipeline = new PipelineDefinition
        {
            Name = "cont",
            OnError = "continue",
            Steps = new List<PipelineStep>
            {
                new() { Name = "ok", Action = _ => sum += 1 },
                new() { Name = "fail", Action = _ => throw new Exception() },
                new() { Name = "after", Action = _ => sum += 10 }
            }
        };
        var ctx = new PipelineStepContext { Mode = "Move" };
        var result = PipelineEngine.Execute(pipeline, ctx);
        Assert.Equal(11, sum);
        Assert.Equal("PartialFailure", result.Status);
        Assert.Equal(2, result.CompletedSteps);
        Assert.Equal(1, result.FailedSteps);
    }

    [Fact]
    public void Execute_DryRun_AllStepsDryRun()
    {
        bool ran = false;
        var pipeline = new PipelineDefinition
        {
            Name = "dry",
            Steps = new List<PipelineStep>
            {
                new() { Name = "s1", Action = _ => ran = true },
                new() { Name = "s2", Action = _ => ran = true }
            }
        };
        var ctx = new PipelineStepContext { Mode = "DryRun" };
        var result = PipelineEngine.Execute(pipeline, ctx);
        Assert.False(ran);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(2, result.CompletedSteps);
    }

    [Fact]
    public void Execute_EmptyPipeline()
    {
        var pipeline = new PipelineDefinition { Name = "empty", Steps = new() };
        var ctx = new PipelineStepContext { Mode = "Move" };
        var result = PipelineEngine.Execute(pipeline, ctx);
        Assert.Equal("Completed", result.Status);
        Assert.Equal(0, result.TotalSteps);
    }

    [Fact]
    public void Execute_PreviousSuccessPropagates()
    {
        bool? seenSuccess = null;
        var pipeline = new PipelineDefinition
        {
            Name = "chain",
            OnError = "continue",
            Steps = new List<PipelineStep>
            {
                new() { Name = "fail", Action = _ => throw new Exception() },
                new()
                {
                    Name = "check",
                    Action = ctx => seenSuccess = ctx.PreviousSuccess
                }
            }
        };
        var ctx = new PipelineStepContext { Mode = "Move" };
        PipelineEngine.Execute(pipeline, ctx);
        Assert.False(seenSuccess);
    }

    [Fact]
    public void Execute_StepOutcomes_MatchStepNames()
    {
        var pipeline = new PipelineDefinition
        {
            Name = "names",
            Steps = new List<PipelineStep>
            {
                new() { Name = "alpha", Action = _ => { } },
                new() { Name = "beta", Action = _ => { } }
            }
        };
        var ctx = new PipelineStepContext { Mode = "Move" };
        var result = PipelineEngine.Execute(pipeline, ctx);
        Assert.Equal(2, result.StepOutcomes.Count);
        Assert.Equal("alpha", result.StepOutcomes[0].StepName);
        Assert.Equal("beta", result.StepOutcomes[1].StepName);
        Assert.All(result.StepOutcomes, o => Assert.Equal("Completed", o.Status));
    }

    [Fact]
    public void ExecuteStep_DoesNotMutatePipelineStep()
    {
        var step = new PipelineStep { Name = "immutable", Action = _ => throw new Exception("err") };
        var ctx = new PipelineStepContext { Mode = "Move" };
        var outcome = PipelineEngine.ExecuteStep(step, ctx);
        Assert.Equal("Failed", outcome.Status);
        // PipelineStep has no Status/Error fields to mutate — the outcome is separate
        Assert.Equal("immutable", step.Name);
    }
}
