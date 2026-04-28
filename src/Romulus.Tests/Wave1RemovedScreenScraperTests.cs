using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Wave-1 / T-W1-UI-REDUCTION (Schritt B): ScreenScraper / Metadata-Scraping entfernt.
/// Pinnt, dass alle zugehoerigen Typen aus Contracts, Infrastructure und API verschwunden
/// sind und nicht still wieder einsickern.
/// Quelle: docs/plan/strategic-reduction-2026/feature-cull-list.md, Abschnitt B.
/// </summary>
public sealed class Wave1RemovedScreenScraperTests
{
    [Theory]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Ports.IGameMetadataProvider")]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Ports.IGameMetadataCache")]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Models.MetadataProviderSettings")]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Models.GameMetadata")]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Models.MetadataEnrichmentRequest")]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Models.MetadataEnrichmentResult")]
    [InlineData("Romulus.Infrastructure", "Romulus.Infrastructure.Metadata.ScreenScraperMetadataProvider")]
    [InlineData("Romulus.Infrastructure", "Romulus.Infrastructure.Metadata.ScreenScraperSystemMap")]
    [InlineData("Romulus.Infrastructure", "Romulus.Infrastructure.Metadata.LiteDbGameMetadataCache")]
    [InlineData("Romulus.Infrastructure", "Romulus.Infrastructure.Metadata.MetadataEnrichmentService")]
    public void RemovedScreenScraperType_MustNotExist(string assemblyName, string fullTypeName)
    {
        var assembly = Assembly.Load(assemblyName);
        var type = assembly.GetType(fullTypeName, throwOnError: false);
        Assert.Null(type);
    }

    /// <summary>
    /// Doppelte Absicherung ueber alle geladenen Romulus-Assemblies, falls ein Typ in eine
    /// andere Komponente verschoben wurde. Findet auch Reanimation in CLI/Api/UI-Assemblies.
    /// </summary>
    [Theory]
    [InlineData("ScreenScraper")]
    [InlineData("MetadataEnrichment")]
    [InlineData("GameMetadata")]
    [InlineData("MetadataProviderSettings")]
    public void NoLoadedAssembly_MayDefineTypeWithRemovedPrefix(string forbiddenPrefix)
    {
        _ = Assembly.Load("Romulus.Contracts");
        _ = Assembly.Load("Romulus.Infrastructure");

        var hits = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Romulus.", StringComparison.Ordinal) == true)
            .Where(a => !a.GetName().Name!.Equals("Romulus.Tests", StringComparison.Ordinal))
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
            })
            .Where(t => t!.Name.StartsWith(forbiddenPrefix, StringComparison.Ordinal)
                     || (t.FullName?.Contains("." + forbiddenPrefix, StringComparison.Ordinal) ?? false))
            .Select(t => t!.FullName)
            .ToList();

        Assert.True(
            hits.Count == 0,
            $"Removed feature '{forbiddenPrefix}' must not be present in any active Romulus assembly. Found: {string.Join(", ", hits)}");
    }

    /// <summary>
    /// RomulusSettings darf keine MetadataProvider-Property mehr besitzen, sonst koennte
    /// ein Settings-Loader still einen alten ScreenScraper-Konfigurationsblock weiterleiten.
    /// </summary>
    [Fact]
    public void RomulusSettings_MustNotExposeMetadataProviderProperty()
    {
        var settingsType = Assembly.Load("Romulus.Contracts")
            .GetType("Romulus.Contracts.Models.RomulusSettings", throwOnError: true)!;
        var prop = settingsType.GetProperty("MetadataProvider", BindingFlags.Public | BindingFlags.Instance);
        Assert.Null(prop);
    }

    /// <summary>
    /// CliCommand-Enum darf keinen Enrich-Wert mehr besitzen, sonst landet 'enrich' wieder
    /// im CLI-Routing.
    /// </summary>
    [Fact]
    public void CliCommand_MustNotDefineEnrichValue()
    {
        Assembly cliAssembly;
        try
        {
            cliAssembly = Assembly.Load("Romulus.CLI");
        }
        catch (System.IO.FileNotFoundException)
        {
            // Romulus.CLI ist eine Exe und wird nicht in jedem Test-Host-Kontext geladen.
            // Build-Bruch durch geloeschte Typen (ScreenScraperMetadataProvider etc.) sichert
            // die CLI-Seite zusaetzlich ab.
            return;
        }

        var cliCommandType = cliAssembly.GetType("Romulus.CLI.CliCommand", throwOnError: false);
        if (cliCommandType is null)
            return;

        var values = Enum.GetNames(cliCommandType);
        Assert.DoesNotContain("Enrich", values);
    }
}
