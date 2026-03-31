using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Orchestration;

namespace RomCleanup.Infrastructure.Analysis;

/// <summary>
/// DAT analysis operations extracted from FeatureService.Dat.
/// Pure logic + file I/O, no GUI dependency.
/// </summary>
public static class DatAnalysisService
{
    public static DatDiffResult CompareDatFiles(string pathA, string pathB)
    {
        var gamesA = LoadDatGameNames(pathA);
        var gamesB = LoadDatGameNames(pathB);

        var setA = new HashSet<string>(gamesA, StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(gamesB, StringComparer.OrdinalIgnoreCase);

        var added = gamesB.Where(g => !setA.Contains(g)).ToList();
        var removed = gamesA.Where(g => !setB.Contains(g)).ToList();
        var common = gamesA.Where(g => setB.Contains(g)).ToList();

        int modified = 0;
        int unchanged = 0;
        if (File.Exists(pathA) && File.Exists(pathB))
        {
            var docA = SafeLoadXDocument(pathA);
            var docB = SafeLoadXDocument(pathB);
            var gameMapA = BuildGameElementMap(docA);
            var gameMapB = BuildGameElementMap(docB);

            foreach (var name in common)
            {
                var xmlA = gameMapA.GetValueOrDefault(name);
                var xmlB = gameMapB.GetValueOrDefault(name);
                if (xmlA is not null && xmlB is not null &&
                    !string.Equals(xmlA, xmlB, StringComparison.Ordinal))
                    modified++;
                else
                    unchanged++;
            }
        }
        else
        {
            unchanged = common.Count;
        }

        return new DatDiffResult(added, removed, modified, unchanged);
    }

    public static string FormatDatDiffReport(string fileA, string fileB, DatDiffResult diff)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DAT Diff Viewer (Logiqx XML)");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"\n  A: {Path.GetFileName(fileA)}");
        sb.AppendLine($"  B: {Path.GetFileName(fileB)}");
        sb.AppendLine($"\n  Unchanged:  {diff.UnchangedCount}");
        sb.AppendLine($"  Modified:   {diff.ModifiedCount}");
        sb.AppendLine($"  Added:      {diff.Added.Count}");
        sb.AppendLine($"  Removed:    {diff.Removed.Count}");

        if (diff.Added.Count > 0)
        {
            sb.AppendLine($"\n  --- Added (first {Math.Min(30, diff.Added.Count)}) ---");
            foreach (var name in diff.Added.Take(30))
                sb.AppendLine($"    + {name}");
            if (diff.Added.Count > 30)
                sb.AppendLine($"    ... and {diff.Added.Count - 30} more");
        }

        if (diff.Removed.Count > 0)
        {
            sb.AppendLine($"\n  --- Removed (first {Math.Min(30, diff.Removed.Count)}) ---");
            foreach (var name in diff.Removed.Take(30))
                sb.AppendLine($"    - {name}");
            if (diff.Removed.Count > 30)
                sb.AppendLine($"    ... and {diff.Removed.Count - 30} more");
        }

        return sb.ToString();
    }

    public static (string Report, int LocalCount, int OldCount) BuildDatAutoUpdateReport(string datRoot)
    {
        var dataDir = RunEnvironmentBuilder.TryResolveDataDir() ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var catalogPath = Path.Combine(dataDir, "dat-catalog.json");

        var sb = new StringBuilder();
        sb.AppendLine("DAT Auto-Update Status");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"\n  DAT root: {datRoot}");

        var localDats = Directory.GetFiles(datRoot, "*.dat", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(datRoot, "*.xml", SearchOption.AllDirectories))
            .ToList();

        sb.AppendLine($"  Local DAT files: {localDats.Count}\n");

        if (localDats.Count > 0)
        {
            sb.AppendLine("  Local files (sorted by age):");
            foreach (var dat in localDats.OrderBy(d => File.GetLastWriteTime(d)).Take(20))
            {
                var age = DateTime.Now - File.GetLastWriteTime(dat);
                var ageStr = age.TotalDays > 365 ? $"{age.TotalDays / 365:0.0} years"
                    : age.TotalDays > 30 ? $"{age.TotalDays / 30:0.0} months"
                    : $"{age.TotalDays:0} days";
                sb.AppendLine($"    {Path.GetFileName(dat),-40} {ageStr} old");
            }
            if (localDats.Count > 20)
                sb.AppendLine($"    ... and {localDats.Count - 20} more");
        }

        if (File.Exists(catalogPath))
        {
            try
            {
                var catalogJson = File.ReadAllText(catalogPath);
                using var doc = JsonDocument.Parse(catalogJson);
                var entries = doc.RootElement.EnumerateArray().ToList();
                var withUrl = entries.Count(e => e.TryGetProperty("Url", out var u) && u.GetString()?.Length > 0);
                var groups = entries.GroupBy(e => e.TryGetProperty("Group", out var g) ? g.GetString() : "?");

                sb.AppendLine($"\n  Catalog: {entries.Count} entries ({withUrl} with download URL)");
                foreach (var g in groups)
                    sb.AppendLine($"    {g.Key}: {g.Count()} systems");
            }
            catch (Exception ex)
            { sb.AppendLine($"\n  Catalog error: {ex.Message}"); }
        }
        else
            sb.AppendLine($"\n  Catalog not found: {catalogPath}");

        var oldDats = localDats.Where(d => (DateTime.Now - File.GetLastWriteTime(d)).TotalDays > 180).ToList();
        if (oldDats.Count > 0)
            sb.AppendLine($"\n  WARNING: {oldDats.Count} DATs are older than 6 months!");

        return (sb.ToString(), localDats.Count, oldDats.Count);
    }

    public static string BuildArcadeMergeSplitReport(string datPath)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(datPath, settings);
        var doc = XDocument.Load(reader);

        var games = doc.Descendants("game").ToList();
        if (games.Count == 0)
            games = doc.Descendants("machine").ToList();

        var parents = games.Where(g => g.Attribute("cloneof") == null).ToList();
        var clones = games.Where(g => g.Attribute("cloneof") != null).ToList();

        var cloneMap = clones.GroupBy(g => g.Attribute("cloneof")?.Value ?? "")
            .ToDictionary(g => g.Key, g => g.ToList());

        var totalRoms = games.Sum(g => g.Descendants("rom").Count());
        var largestParent = parents.OrderByDescending(p =>
        {
            var name = p.Attribute("name")?.Value ?? "";
            return cloneMap.TryGetValue(name, out var c) ? c.Count : 0;
        }).FirstOrDefault();
        var largestName = largestParent?.Attribute("name")?.Value ?? "?";
        var largestCloneCount = cloneMap.TryGetValue(largestName, out var lc) ? lc.Count : 0;

        var sb = new StringBuilder();
        sb.AppendLine("Arcade Merge/Split Analysis");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine($"\n  DAT: {Path.GetFileName(datPath)}");
        sb.AppendLine($"  Total entries: {games.Count}");
        sb.AppendLine($"  Parents:       {parents.Count}");
        sb.AppendLine($"  Clones:        {clones.Count}");
        sb.AppendLine($"  Total ROMs:    {totalRoms}");
        sb.AppendLine($"\n  Largest family: {largestName} ({largestCloneCount} clones)");
        sb.AppendLine($"\n  Set type recommendation:");
        sb.AppendLine($"    Non-Merged: {parents.Count + clones.Count} sets (portable, large)");
        sb.AppendLine($"    Split:      {parents.Count + clones.Count} sets (clones diff only)");
        sb.AppendLine($"    Merged:     {parents.Count} sets (parents contain everything)");

        var top10 = parents
            .Select(p => new { Name = p.Attribute("name")?.Value ?? "?",
                Clones = cloneMap.TryGetValue(p.Attribute("name")?.Value ?? "", out var cc) ? cc.Count : 0 })
            .OrderByDescending(x => x.Clones)
            .Take(10);
        sb.AppendLine($"\n  Top 10 Parents (most clones):");
        foreach (var p in top10)
            sb.AppendLine($"    {p.Name,-30} {p.Clones} clones");

        return sb.ToString();
    }

    public static string ImportDatFileToRoot(string sourcePath, string datRoot)
    {
        var safeName = Path.GetFileName(sourcePath);
        var targetPath = Path.GetFullPath(Path.Combine(datRoot, safeName));
        if (!targetPath.StartsWith(Path.GetFullPath(datRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path outside of DatRoot.");
        File.Copy(sourcePath, targetPath, overwrite: true);
        return targetPath;
    }

    // --- Internal helpers ---

    internal static List<string> LoadDatGameNames(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return [];
        try
        {
            var doc = SafeLoadXDocument(path);
            return doc.Descendants("game")
                .Select(e => e.Attribute("name")?.Value ?? "")
                .Where(n => n.Length > 0)
                .ToList();
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    internal static Dictionary<string, string> BuildGameElementMap(XDocument doc)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in doc.Descendants("game"))
        {
            var name = game.Attribute("name")?.Value;
            if (name is not null)
                map.TryAdd(name, game.ToString());
        }
        return map;
    }

    internal static XDocument SafeLoadXDocument(string path)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader);
    }
}
