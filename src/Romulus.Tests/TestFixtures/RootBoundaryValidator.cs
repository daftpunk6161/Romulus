using System.Security.Cryptography;

namespace Romulus.Tests.TestFixtures;

/// <summary>
/// Block D2 - filesystem scan validator that snapshots files OUTSIDE allowed
/// roots before a Run and verifies they are byte-identical (and still present)
/// after the Run.
///
/// Use this to assert the safety invariant: "Move/Sort phase never touches a
/// file outside the configured Roots". Replaces ad-hoc snapshot loops.
/// </summary>
internal sealed class RootBoundaryValidator
{
    private readonly Dictionary<string, string> _baselineHashes = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _outsideRoots;

    public RootBoundaryValidator(params string[] outsideRoots)
    {
        ArgumentNullException.ThrowIfNull(outsideRoots);
        _outsideRoots = [.. outsideRoots
            .Where(static r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r))
            .Select(static r => Path.GetFullPath(r))];
    }

    /// <summary>
    /// Capture SHA-256 hashes of every file currently under each "outside" root.
    /// Must be called before the Run starts.
    /// </summary>
    public RootBoundaryValidator Snapshot()
    {
        _baselineHashes.Clear();
        foreach (var root in _outsideRoots)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                _baselineHashes[Path.GetFullPath(file)] = HashFile(file);
            }
        }
        return this;
    }

    /// <summary>
    /// Verify every previously snapshotted file still exists with the
    /// identical bytes, and that no new files appeared in the outside roots.
    /// Returns the list of violations (empty == clean).
    /// </summary>
    public IReadOnlyList<string> Verify()
    {
        var violations = new List<string>();

        var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in _outsideRoots)
        {
            if (!Directory.Exists(root))
            {
                violations.Add($"Outside root '{root}' was deleted during run.");
                continue;
            }
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                current.Add(Path.GetFullPath(file));
            }
        }

        foreach (var (path, baselineHash) in _baselineHashes)
        {
            if (!File.Exists(path))
            {
                violations.Add($"File outside roots was deleted: {path}");
                continue;
            }
            var nowHash = HashFile(path);
            if (!string.Equals(nowHash, baselineHash, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"File outside roots was modified: {path}");
            }
        }

        foreach (var path in current)
        {
            if (!_baselineHashes.ContainsKey(path))
            {
                violations.Add($"File appeared outside roots during run: {path}");
            }
        }

        return violations;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        var bytes = SHA256.HashData(stream);
        return Convert.ToHexString(bytes);
    }
}
