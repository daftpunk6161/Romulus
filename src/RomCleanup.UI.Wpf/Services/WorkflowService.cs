using RomCleanup.Infrastructure.Orchestration;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>GUI-040: Delegates to static FeatureService.Workflow methods.</summary>
public sealed class WorkflowService : IWorkflowService
{
    public Dictionary<string, string> GetSortTemplates()
        => FeatureService.GetSortTemplates();

    public bool TestCronMatch(string cronExpression, DateTime dt)
        => FeatureService.TestCronMatch(cronExpression, dt);
}
