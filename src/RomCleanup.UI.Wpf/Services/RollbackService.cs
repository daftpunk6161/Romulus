using System.IO;
using RomCleanup.Infrastructure.Audit;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>
/// Extracted from MainWindow.xaml.cs — handles audit-based rollback.
/// RF-004 from gui-ux-deep-audit.md.
/// </summary>
public static class RollbackService
{
    /// <summary>
    /// Execute a rollback from the given audit CSV file.
    /// Must be called from a background thread (performs file I/O).
    /// Returns the list of restored file paths.
    /// </summary>
    public static IReadOnlyList<string> Execute(string auditPath, IReadOnlyList<string> roots)
    {
        var audit = new AuditCsvStore();
        var rootArray = roots is string[] arr ? arr : roots.ToArray();
        return audit.Rollback(auditPath, rootArray, rootArray, dryRun: false);
    }
}
