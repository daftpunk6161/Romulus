using RomCleanup.Contracts.Models;

namespace RomCleanup.Infrastructure.Pipeline;

/// <summary>
/// Conditional multi-step pipeline engine.
/// Mirrors PipelineEngine.ps1.
/// </summary>
public sealed class PipelineEngine
{
    /// <summary>
    /// Executes a single pipeline step with condition checking.
    /// Returns an outcome without mutating the step.
    /// </summary>
    public static PipelineStepOutcome ExecuteStep(PipelineStep step, PipelineStepContext context)
    {
        // Check condition
        if (step.Condition != null && !step.Condition(context))
        {
            return new PipelineStepOutcome { StepName = step.Name, Status = "Skipped" };
        }

        // DryRun mode
        if (context.Mode == "DryRun")
        {
            return new PipelineStepOutcome { StepName = step.Name, Status = "DryRun" };
        }

        try
        {
            step.Action(context);
            return new PipelineStepOutcome { StepName = step.Name, Status = "Completed" };
        }
        catch (Exception ex)
        {
            return new PipelineStepOutcome { StepName = step.Name, Status = "Failed", Error = ex.ToString() };
        }
    }

    /// <summary>
    /// Executes a complete pipeline with OnError mode (stop/continue).
    /// </summary>
    public static PipelineResult Execute(PipelineDefinition pipeline, PipelineStepContext context)
    {
        var result = new PipelineResult
        {
            Name = pipeline.Name,
            TotalSteps = pipeline.Steps.Count,
        };

        bool previousSuccess = true;

        for (int i = 0; i < pipeline.Steps.Count; i++)
        {
            var step = pipeline.Steps[i];
            context.PreviousSuccess = previousSuccess;

            var outcome = ExecuteStep(step, context);
            result.StepOutcomes.Add(outcome);

            switch (outcome.Status)
            {
                case "Completed":
                case "DryRun":
                    result.CompletedSteps++;
                    break;
                case "Skipped":
                    result.SkippedSteps++;
                    break;
                case "Failed":
                    result.FailedSteps++;
                    previousSuccess = false;

                    if (pipeline.OnError == "stop")
                    {
                        // Mark remaining steps as skipped
                        for (int j = i + 1; j < pipeline.Steps.Count; j++)
                        {
                            result.StepOutcomes.Add(new PipelineStepOutcome
                            {
                                StepName = pipeline.Steps[j].Name,
                                Status = "Skipped"
                            });
                            result.SkippedSteps++;
                        }
                        result.Status = "Failed";
                        return result;
                    }
                    break;
            }
        }

        result.Status = result.FailedSteps > 0 ? "PartialFailure" : "Completed";
        return result;
    }
}
