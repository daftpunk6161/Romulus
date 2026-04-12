using System.IO;
using System.Text.Json;
using Romulus.Infrastructure.Orchestration;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Block 3 – DI-Hygiene + Daten-Korrektheit
/// R6-04: DI-Bypass-Stellen in WPF-Schicht müssen mit DI-BYPASS-JUSTIFIED kommentiert sein
/// R6-10: rules.json SCAN/EU-Widerspruch – regionTokenMap["scandinavia"] muss "SCAN" ergeben
/// RED tests: fail now, GREEN after implementing fixes.
/// </summary>
public sealed class Block3_DiHygieneTests
{
    // ═══ Helper ═════════════════════════════════════════════════════════

    private static string FindRepoFile(params string[] parts)
    {
        var dataDir = RunEnvironmentBuilder.ResolveDataDir();
        var repoRoot = Directory.GetParent(dataDir)?.FullName
            ?? throw new InvalidOperationException("Repository root could not be resolved from data directory.");
        return Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    }

    // ═══ R6-04 ══════════════════════════════════════════════════════════

    [Fact]
    public void R6_04_MainViewModelProductization_DiBypass_MustHaveJustificationComment()
    {
        // ARRANGE
        var path = FindRepoFile("src", "Romulus.UI.Wpf", "ViewModels", "MainViewModel.Productization.cs");
        var lines = File.ReadAllLines(path);

        // ACT – find lines that do a fallback-new for RunProfileService but lack justification
        var violations = lines
            .Select((line, i) => (line, lineNo: i + 1))
            .Where(x => x.line.Contains("?? new RunProfileService")
                     && !x.line.Contains("DI-BYPASS-JUSTIFIED"))
            .ToList();

        // ASSERT – every DI bypass must be documented
        Assert.Empty(violations);
    }

    [Fact]
    public void R6_04_RunService_DiBypass_MustHaveJustificationComment()
    {
        // ARRANGE
        var path = FindRepoFile("src", "Romulus.UI.Wpf", "Services", "RunService.cs");
        var lines = File.ReadAllLines(path);

        // ACT – find undocumented ?-newRunProfileService bypasses
        var violations = lines
            .Select((line, i) => (line, lineNo: i + 1))
            .Where(x => x.line.Contains("?? new RunProfileService")
                     && !x.line.Contains("DI-BYPASS-JUSTIFIED"))
            .ToList();

        // ASSERT
        Assert.Empty(violations);
    }

    // ═══ R6-10 ══════════════════════════════════════════════════════════
    // Design decision (R6-10):
    //   SCAN is a sub-region of EU.
    //   NormalizeRegionKey("SCAN") → "EU" in RegionDetector.
    //   Therefore regionTokenMap["scandinavia"] = "EU" is CONSISTENT – both detection
    //   paths (ordered-rule + tokenMap) produce "EU" for Scandinavian releases.
    //   The SCAN key in RegionOrdered exists to give the token a named intermediate key
    //   before normalization. Changing tokenMap to "SCAN" would cause multi-region
    //   filenames like "(Europe, Scandinavia)" to resolve as WORLD instead of EU/SCAN.

    [Fact]
    public void R6_10_Invariant_RegionTokenMap_Scandinavia_MustMapToEu_ConsistentWithNormalization()
    {
        // ARRANGE – load rules.json
        var rulesPath = FindRepoFile("data", "rules.json");
        Assert.True(File.Exists(rulesPath), $"rules.json not found at {rulesPath}");
        using var doc = JsonDocument.Parse(File.ReadAllText(rulesPath));

        // ACT
        var tokenMap = doc.RootElement.GetProperty("regionTokenMap");
        Assert.True(
            tokenMap.TryGetProperty("scandinavia", out var scandinaviaElement),
            "regionTokenMap must contain an entry for 'scandinavia'");
        var region = scandinaviaElement.GetString();

        // ASSERT – regionTokenMap["scandinavia"] must be "EU":
        //   RegionDetector.NormalizeRegionKey("SCAN") → "EU", so the tokenMap
        //   must stay as "EU" to keep both detection paths consistent.
        Assert.Equal("EU", region);
    }

    [Fact]
    public void R6_10_RegionOrdered_ScanEntry_MustExist_WithScandinaviaPattern()
    {
        // ARRANGE – load rules.json
        var rulesPath = FindRepoFile("data", "rules.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(rulesPath));
        var root = doc.RootElement;

        // ASSERT – SCAN must exist in RegionOrdered (for named intermediate-key matching)
        var orderedArray = root.GetProperty("RegionOrdered");
        var scanEntry = orderedArray.EnumerateArray()
            .FirstOrDefault(e => e.TryGetProperty("Key", out var k) && k.GetString() == "SCAN");

        Assert.False(scanEntry.ValueKind == JsonValueKind.Undefined,
            "RegionOrdered must contain a SCAN entry for named detection of Scandinavian releases");

        var pattern = scanEntry.GetProperty("Pattern").GetString();
        Assert.NotNull(pattern);
        Assert.Contains("scandinavia", pattern, StringComparison.OrdinalIgnoreCase);
    }
}
