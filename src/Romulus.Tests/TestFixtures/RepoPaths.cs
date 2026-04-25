namespace Romulus.Tests.TestFixtures;

/// <summary>
/// Block E4 - centralized repository path resolution for tests.
///
/// Replaces the 8 copies of <c>FindRepoFile(params string[])</c> and the
/// 2 copies of <c>FindSrcRoot()</c> previously scattered across test files.
///
/// Resolution strategies are chosen to match prior local helpers:
/// <list type="bullet">
///   <item><see cref="RepoFile"/> walks up from the resolved data directory
///   (anchored to <c>data/</c>) so behaviour matches the historic
///   <c>RunEnvironmentBuilder.ResolveDataDir</c>-based helpers.</item>
///   <item><see cref="SrcRoot"/> walks up from <see cref="AppContext.BaseDirectory"/>
///   looking for a <c>src/</c> sibling that contains
///   <c>Romulus.Infrastructure</c>, mirroring the previous heuristic.</item>
/// </list>
/// </summary>
internal static class RepoPaths
{
    /// <summary>
    /// Combine repository-relative segments to an absolute file path
    /// rooted at the repository root (the parent of <c>data/</c>).
    /// </summary>
    public static string RepoFile(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var dataDir = Romulus.Infrastructure.Orchestration.RunEnvironmentBuilder.ResolveDataDir();
        var repoRoot = Directory.GetParent(dataDir)?.FullName
            ?? throw new InvalidOperationException(
                $"Repository root could not be resolved from data directory '{dataDir}'.");
        return Path.Combine([repoRoot, .. parts]);
    }

    /// <summary>
    /// Resolve the absolute path to <c>src/</c> by walking up from
    /// <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public static string SrcRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "src");
            if (Directory.Exists(candidate) &&
                Directory.Exists(Path.Combine(candidate, "Romulus.Infrastructure")))
            {
                return candidate;
            }

            // We may already be inside src/.
            if (string.Equals(Path.GetFileName(dir), "src", StringComparison.Ordinal) &&
                Directory.Exists(Path.Combine(dir, "Romulus.Infrastructure")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException(
            $"Could not find src/ root from {AppContext.BaseDirectory}");
    }
}
