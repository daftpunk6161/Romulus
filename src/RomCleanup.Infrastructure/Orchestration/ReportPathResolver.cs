namespace RomCleanup.Infrastructure.Orchestration;

public static class ReportPathResolver
{
    public static string? Resolve(string? actualReportPath, string? plannedReportPath)
    {
        if (!string.IsNullOrWhiteSpace(actualReportPath) && File.Exists(actualReportPath))
            return Path.GetFullPath(actualReportPath);

        if (!string.IsNullOrWhiteSpace(plannedReportPath) && File.Exists(plannedReportPath))
            return Path.GetFullPath(plannedReportPath);

        var candidateDirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(plannedReportPath))
        {
            var plannedDir = Path.GetDirectoryName(plannedReportPath);
            if (!string.IsNullOrWhiteSpace(plannedDir) && Directory.Exists(plannedDir))
                candidateDirs.Add(plannedDir);
        }

        var fallbackDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RomCleanupRegionDedupe",
            "reports");
        if (Directory.Exists(fallbackDir))
            candidateDirs.Add(fallbackDir);

        foreach (var dir in candidateDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var latest = Directory.GetFiles(dir, "*.html", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(latest) && File.Exists(latest))
                    return Path.GetFullPath(latest);
            }
            catch
            {
                // best-effort lookup only
            }
        }

        return null;
    }
}
