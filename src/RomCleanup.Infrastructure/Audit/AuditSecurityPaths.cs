namespace RomCleanup.Infrastructure.Audit;

using RomCleanup.Infrastructure.Paths;

/// <summary>
/// Centralizes persistent paths for audit integrity artifacts.
/// </summary>
public static class AuditSecurityPaths
{
    public static string GetDefaultSigningKeyPath()
    {
        return AppStoragePathResolver.ResolveRoamingPath("security", "audit-signing.key");
    }

    public static string GetDefaultAuditDirectory()
    {
        return AppStoragePathResolver.ResolveRoamingPath("audit");
    }

    public static string GetDefaultReportDirectory()
    {
        return AppStoragePathResolver.ResolveRoamingPath("reports");
    }
}