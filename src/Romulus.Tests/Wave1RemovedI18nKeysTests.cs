using System.IO;
using System.Text.Json;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Wave-1 / T-W1-UI-REDUCTION (Schritt G): Plugin/Marketplace-Stub-Cleanup.
/// Pinnt entfernte i18n-Keys, damit sie nicht still wieder einsickern.
/// Quelle: docs/plan/strategic-reduction-2026/feature-cull-list.md, Abschnitt G.
/// </summary>
public sealed class Wave1RemovedI18nKeysTests
{
    private static readonly string[] Locales = ["de", "en", "fr"];

    /// <summary>
    /// "Advanced.PluginManager" ist ein toter Stub-Key ohne Service-Implementierung.
    /// Plan-Task T-W1-UI-REDUCTION (Schritt G) entfernt ihn aus allen Sprach-Dateien.
    /// </summary>
    [Theory]
    [InlineData("Advanced.PluginManager")]
    public void RemovedI18nKey_MustNotExist_InAnyLocale(string removedKey)
    {
        foreach (var locale in Locales)
        {
            var path = Path.Combine(FindDataDir(), "i18n", $"{locale}.json");
            Assert.True(File.Exists(path), $"Missing i18n file: {locale}.json");

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            Assert.False(
                root.TryGetProperty(removedKey, out _),
                $"Removed i18n key '{removedKey}' must not exist in {locale}.json (Wave-1 Stub-Cleanup, Abschnitt G).");
        }
    }

    private static string FindDataDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data")))
            dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir!, "data");
    }
}
