namespace RomCleanup.UI.Wpf.Services;

/// <summary>GUI-040: Sort templates and scheduling helpers.</summary>
public interface IWorkflowService
{
    Dictionary<string, string> GetSortTemplates();
    bool TestCronMatch(string cronExpression, DateTime dt);
}
