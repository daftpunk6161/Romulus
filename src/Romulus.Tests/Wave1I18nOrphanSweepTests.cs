using System.IO;
using System.Text.Json;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Wave 1 — T-W1-I18N-ORPHAN-SWEEP (Cull-driven slice).
/// Pins removal of i18n keys that became orphaned by the prior cull steps
/// (Plugin / ScreenScraper / RetroAchievements / Frontend-Export).
/// Full-blown orphan sweep is gated on T-W1-UI-REDUCTION step D completion.
/// </summary>
public sealed class Wave1I18nOrphanSweepTests
{
    private static readonly string[] LocaleFiles =
    [
        "data/i18n/de.json",
        "data/i18n/en.json",
        "data/i18n/fr.json",
    ];

    [Theory]
    [InlineData("Advanced.DuplicateInspector")]
    [InlineData("Advanced.DuplicateExport")]
    [InlineData("Cmd.PluginNoDir")]
    [InlineData("Cmd.PluginTitle")]
    [InlineData("Cmd.PluginHeader")]
    [InlineData("Cmd.PluginNone")]
    public void RemovedCullDrivenKey_IsAbsent_FromAllLocales(string key)
    {
        var repoRoot = FindRepoRoot();
        foreach (var rel in LocaleFiles)
        {
            var path = Path.Combine(repoRoot, rel);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.False(
                doc.RootElement.TryGetProperty(key, out _),
                $"Locale '{rel}' must not contain orphaned cull-driven key '{key}'.");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Romulus.sln"))
                                && !Directory.Exists(Path.Combine(dir.FullName, "data", "i18n")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
