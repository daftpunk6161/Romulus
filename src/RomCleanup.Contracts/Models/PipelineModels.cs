namespace RomCleanup.Contracts.Models;

/// <summary>
/// A single step definition in a pipeline (immutable after construction).
/// </summary>
public sealed class PipelineStep
{
    public string Name { get; set; } = "";
    public Action<PipelineStepContext> Action { get; set; } = _ =>
        throw new InvalidOperationException("PipelineStep.Action not configured");
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Func<PipelineStepContext, bool>? Condition { get; set; }
}

/// <summary>
/// Outcome of executing a single pipeline step.
/// </summary>
public sealed class PipelineStepOutcome
{
    public string StepName { get; init; } = "";
    public string Status { get; init; } = "Pending";
    public string? Error { get; init; }
}

/// <summary>
/// Context passed to each pipeline step.
/// </summary>
public sealed class PipelineStepContext
{
    public string Mode { get; set; } = "DryRun";
    public bool PreviousSuccess { get; set; } = true;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, object> SharedState { get; set; } = new();
}

/// <summary>
/// Pipeline definition with steps and error behavior.
/// </summary>
public sealed class PipelineDefinition
{
    public string Name { get; set; } = "";
    public List<PipelineStep> Steps { get; set; } = new();
    /// <summary>"stop" or "continue"</summary>
    public string OnError { get; set; } = "stop";
}

/// <summary>
/// Result of executing a pipeline.
/// </summary>
public sealed class PipelineResult
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "Completed";
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public int FailedSteps { get; set; }
    public int SkippedSteps { get; set; }
    public List<PipelineStepOutcome> StepOutcomes { get; set; } = new();
}
