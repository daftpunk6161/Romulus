using RomCleanup.Contracts;

namespace RomCleanup.Infrastructure.Paths;

/// <summary>
/// Resolves persistent storage roots for standard and portable mode.
/// Portable mode is enabled when a <c>.portable</c> marker exists next to the executable.
/// </summary>
public static class AppStoragePathResolver
{
    private const string PortableMarkerFileName = ".portable";
    private const string PortableDirectoryName = ".romcleanup";

    public static bool IsPortableMode()
    {
        var markerPath = Path.Combine(AppContext.BaseDirectory, PortableMarkerFileName);
        return File.Exists(markerPath);
    }

    public static string ResolvePortableRootDirectory()
        => Path.Combine(AppContext.BaseDirectory, PortableDirectoryName);

    public static string ResolveRoamingAppDirectory()
    {
        if (IsPortableMode())
            return ResolvePortableRootDirectory();

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppIdentity.AppFolderName);
    }

    public static string ResolveLocalAppDirectory()
    {
        if (IsPortableMode())
            return ResolvePortableRootDirectory();

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppIdentity.AppFolderName);
    }

    public static string ResolveRoamingPath(params string[] segments)
        => CombinePath(ResolveRoamingAppDirectory(), segments);

    public static string ResolveLocalPath(params string[] segments)
        => CombinePath(ResolveLocalAppDirectory(), segments);

    private static string CombinePath(string root, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
            return root;

        var sanitized = segments
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        if (sanitized.Length == 0)
            return root;

        return Path.Combine([root, .. sanitized]);
    }
}