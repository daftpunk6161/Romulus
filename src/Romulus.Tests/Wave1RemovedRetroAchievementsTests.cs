using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Wave-1 / T-W1-UI-REDUCTION (Schritt C): RetroAchievements-Compliance entfernt.
/// Pinnt, dass die zugehoerigen Typen aus Contracts und Infrastructure verschwunden sind,
/// damit sie nicht still wieder einsickern.
/// Quelle: docs/plan/strategic-reduction-2026/feature-cull-list.md, Abschnitt C.
/// </summary>
public sealed class Wave1RemovedRetroAchievementsTests
{
    [Theory]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Ports.IRetroAchievementsCatalog")]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Models.RetroAchievementsCatalogEntry")]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Models.RetroAchievementsCheckRequest")]
    [InlineData("Romulus.Contracts", "Romulus.Contracts.Models.RetroAchievementsCheckResult")]
    [InlineData("Romulus.Infrastructure", "Romulus.Infrastructure.Monitoring.RetroAchievementsComplianceService")]
    public void RemovedRetroAchievementsType_MustNotExist(string assemblyName, string fullTypeName)
    {
        var assembly = Assembly.Load(assemblyName);
        var type = assembly.GetType(fullTypeName, throwOnError: false);
        Assert.Null(type);
    }

    /// <summary>
    /// Doppelte Absicherung ueber alle geladenen Assemblies, falls ein Typ in eine andere
    /// Komponente verschoben wurde. Findet auch Reanimation in Tests-/UI-Assemblies.
    /// </summary>
    [Theory]
    [InlineData("RetroAchievements")]
    public void NoLoadedAssembly_MayDefineTypeWithRetroAchievementsPrefix(string forbiddenPrefix)
    {
        // ForceLoad der relevanten Assemblies
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
}
