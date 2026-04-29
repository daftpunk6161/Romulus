using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Wave 2 — T-W2-I18N-ORPHAN-CI-TEST.
/// Permanent CI guard derived from T-W1-I18N-ORPHAN-SWEEP.
/// Read-only. No auto-cleanup.
///
/// Invariants (all enforced as xUnit pin tests):
///   1. Every key in en.json / fr.json must also exist in de.json
///      (de.json is the authoritative master).
///   2. The set of i18n keys that have NO quoted-literal reference under
///      src/**/*.cs or src/**/*.xaml must be a subset of the documented
///      baseline file (data/i18n/orphans-baseline-de.txt). The baseline
///      MAY shrink — never grow. New orphans break the build.
///   3. Baseline entries that are now referenced in source must be
///      removed (sanity guard against stale baseline drift).
///   4. Locale meta block (_meta) is ignored on both sides.
///
/// The Wave-1 sweep removed only the cull-driven slice (Plugin /
/// ScreenScraper / RetroAchievements / Frontend-Export). The remaining
/// legacy WPF tab/string orphans (~480 entries) are tracked in the
/// baseline; a dedicated future sweep is expected to drive that list to
/// zero. This guard prevents regression while that work happens.
/// </summary>
public sealed class Wave2I18nOrphanGuardTests
{
    /// <summary>
    /// Key prefixes whose keys are constructed at runtime (dynamic
    /// composition) and therefore won't appear as quoted literals.
    /// Adding to this list MUST come with a code-side justification.
    /// </summary>
    private static readonly string[] DynamicPrefixes =
    {
        "UnknownReason.",
        "Region.Code.",
        "Console.Display.",
        "ToolMaturity.",
    };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null
               && !File.Exists(Path.Combine(dir.FullName, "src", "Romulus.sln")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static IReadOnlyList<string> ReadKeys(string jsonRelPath)
    {
        var path = Path.Combine(FindRepoRoot(), jsonRelPath.Replace('/', Path.DirectorySeparatorChar));
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject()
            .Where(p => !string.Equals(p.Name, "_meta", StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToList();
    }

    private static string LoadAllSources()
    {
        var root = Path.Combine(FindRepoRoot(), "src");
        var sb = new System.Text.StringBuilder();
        foreach (var f in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            sb.AppendLine(File.ReadAllText(f));
        }
        foreach (var f in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            if (f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            sb.AppendLine(File.ReadAllText(f));
        }
        return sb.ToString();
    }

    [Fact]
    public void EveryEnglishKey_ExistsInGerman()
    {
        var de = new HashSet<string>(ReadKeys("data/i18n/de.json"), StringComparer.Ordinal);
        var en = ReadKeys("data/i18n/en.json");
        var missing = en.Where(k => !de.Contains(k)).ToList();
        Assert.True(missing.Count == 0,
            $"Keys present in en.json but missing in de.json: {string.Join(", ", missing.Take(10))}"
            + (missing.Count > 10 ? $" (+{missing.Count - 10} more)" : ""));
    }

    [Fact]
    public void EveryFrenchKey_ExistsInGerman()
    {
        var de = new HashSet<string>(ReadKeys("data/i18n/de.json"), StringComparer.Ordinal);
        var fr = ReadKeys("data/i18n/fr.json");
        var missing = fr.Where(k => !de.Contains(k)).ToList();
        Assert.True(missing.Count == 0,
            $"Keys present in fr.json but missing in de.json: {string.Join(", ", missing.Take(10))}"
            + (missing.Count > 10 ? $" (+{missing.Count - 10} more)" : ""));
    }

    [Fact]
    public void EveryGermanKey_IsReferencedInSource_OrTrackedInBaseline()
    {
        var keys = ReadKeys("data/i18n/de.json");
        var sources = LoadAllSources();
        var baseline = ReadBaseline();
        var actualOrphans = new List<string>();

        foreach (var key in keys)
        {
            if (DynamicPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)))
                continue;
            if (!sources.Contains(key, StringComparison.Ordinal))
                actualOrphans.Add(key);
        }

        // Regression guard: actual orphans MUST be a subset of the baseline.
        // The baseline may shrink as cleanup happens; growth is forbidden.
        var newOrphans = actualOrphans.Where(o => !baseline.Contains(o)).OrderBy(o => o, StringComparer.Ordinal).ToList();
        Assert.True(newOrphans.Count == 0,
            "New i18n orphans introduced in de.json (no source reference and not in baseline). "
            + "Either reference the key in src/ (XAML or C#) or remove it from de.json. "
            + $"New orphans: {string.Join(", ", newOrphans.Take(15))}"
            + (newOrphans.Count > 15 ? $" (+{newOrphans.Count - 15} more)" : ""));
    }

    [Fact]
    public void Baseline_DoesNotListKeysThatAreActuallyReferencedNow()
    {
        // Sanity check: if a baseline-listed key has been wired up since,
        // it must be removed from the baseline (the baseline is allowed
        // to shrink, but stale entries indicate sloppy tracking).
        var sources = LoadAllSources();
        var baseline = ReadBaseline();
        var stale = baseline.Where(k => sources.Contains(k, StringComparison.Ordinal))
                            .OrderBy(k => k, StringComparer.Ordinal)
                            .ToList();
        Assert.True(stale.Count == 0,
            $"Baseline lists keys that are now referenced in source. "
            + $"Remove these entries from data/i18n/orphans-baseline-de.txt: "
            + string.Join(", ", stale.Take(15))
            + (stale.Count > 15 ? $" (+{stale.Count - 15} more)" : ""));
    }

    private static IReadOnlySet<string> ReadBaseline()
    {
        var path = Path.Combine(FindRepoRoot(), "data", "i18n", "orphans-baseline-de.txt");
        if (!File.Exists(path))
            return new HashSet<string>(StringComparer.Ordinal);
        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
    }
}
