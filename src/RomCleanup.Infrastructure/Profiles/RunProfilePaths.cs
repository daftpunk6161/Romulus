using RomCleanup.Contracts;

namespace RomCleanup.Infrastructure.Profiles;

public static class RunProfilePaths
{
    public const string BuiltInProfilesFileName = "builtin-profiles.json";

    public static string ResolveUserProfileDirectory(string? overrideDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
            return Path.GetFullPath(overrideDirectory);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppIdentity.AppFolderName, "profiles");
    }

    public static string ResolveBuiltInProfilesPath(string dataDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDir);
        return Path.Combine(dataDir, BuiltInProfilesFileName);
    }
}
