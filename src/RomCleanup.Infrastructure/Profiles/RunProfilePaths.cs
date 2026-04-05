using RomCleanup.Contracts;
using RomCleanup.Infrastructure.Paths;

namespace RomCleanup.Infrastructure.Profiles;

public static class RunProfilePaths
{
    public const string BuiltInProfilesFileName = "builtin-profiles.json";

    public static string ResolveUserProfileDirectory(string? overrideDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
            return Path.GetFullPath(overrideDirectory);

        return AppStoragePathResolver.ResolveRoamingPath("profiles");
    }

    public static string ResolveBuiltInProfilesPath(string dataDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDir);
        return Path.Combine(dataDir, BuiltInProfilesFileName);
    }
}
