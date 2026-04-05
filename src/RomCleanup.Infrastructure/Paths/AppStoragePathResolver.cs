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
        var fullRoot = Path.GetFullPath(root);

        if (segments.Count == 0)
            return fullRoot;

        var sanitized = segments
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        if (sanitized.Length == 0)
            return fullRoot;

        var combined = Path.GetFullPath(Path.Combine([fullRoot, .. sanitized]));
        var normalizedRoot = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCombined = combined.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;

        if (!normalizedCombined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedCombined, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path segments resolved outside application storage root.");
        }

        return combined;
    }
}