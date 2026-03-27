using System.Reflection;
using RomCleanup.Infrastructure.Paths;
using Xunit;

namespace RomCleanup.Tests;

/// <summary>
/// Regression tests for the release-clean hygiene cleanup.
/// Ensures removed dead code stays removed and centralized path logic works.
/// </summary>
public sealed class HygieneCleanupRegressionTests
{
    private static readonly Assembly WpfAssembly = typeof(RomCleanup.UI.Wpf.App).Assembly;

    // ═══ Dead wrapper services must stay removed ═══════════════════════

    [Theory]
    [InlineData("ICollectionService")]
    [InlineData("CollectionService")]
    [InlineData("IHealthAnalyzer")]
    [InlineData("HealthAnalyzer")]
    [InlineData("IConversionEstimator")]
    [InlineData("ConversionEstimator")]
    [InlineData("IExportService")]
    [InlineData("ExportService")]
    [InlineData("IDatManagementService")]
    [InlineData("DatManagementService")]
    [InlineData("IWorkflowService")]
    [InlineData("WorkflowService")]
    public void RemovedWrapperService_MustNotExistInWpfAssembly(string typeName)
    {
        var type = WpfAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == typeName);

        Assert.Null(type);
    }

    // ═══ ArtifactPathResolver.FindContainingRoot (centralized) ════════

    [Fact]
    public void FindContainingRoot_ReturnsMatchingRoot()
    {
        var roots = new List<string>
        {
            ArtifactPathResolver.NormalizeRoot(@"C:\Games\SNES"),
            ArtifactPathResolver.NormalizeRoot(@"C:\Games\NES")
        };

        var result = ArtifactPathResolver.FindContainingRoot(@"C:\Games\SNES\Mario.zip", roots);

        Assert.NotNull(result);
        Assert.EndsWith("SNES", result);
    }

    [Fact]
    public void FindContainingRoot_ReturnsNull_WhenNoRootMatches()
    {
        var roots = new List<string>
        {
            ArtifactPathResolver.NormalizeRoot(@"C:\Games\SNES")
        };

        var result = ArtifactPathResolver.FindContainingRoot(@"D:\Other\file.zip", roots);

        Assert.Null(result);
    }

    [Fact]
    public void FindContainingRoot_DoesNotMatchPartialName()
    {
        // "C:\Games\SNES" must NOT match "C:\Games\SNES-Hacks\file.zip"
        var roots = new List<string>
        {
            ArtifactPathResolver.NormalizeRoot(@"C:\Games\SNES")
        };

        var result = ArtifactPathResolver.FindContainingRoot(@"C:\Games\SNES-Hacks\file.zip", roots);

        Assert.Null(result);
    }

    [Fact]
    public void FindContainingRoot_IsCaseInsensitive()
    {
        var roots = new List<string>
        {
            ArtifactPathResolver.NormalizeRoot(@"C:\Games\SNES")
        };

        var result = ArtifactPathResolver.FindContainingRoot(@"c:\games\snes\mario.zip", roots);

        Assert.NotNull(result);
    }

    // ═══ NormalizeRoot consistency ════════════════════════════════════

    [Fact]
    public void NormalizeRoot_ProducesConsistentOutput()
    {
        var a = ArtifactPathResolver.NormalizeRoot(@"C:\Games\SNES\");
        var b = ArtifactPathResolver.NormalizeRoot(@"C:\Games\SNES");
        var c = ArtifactPathResolver.NormalizeRoot(@"C:\Games\SNES\\");

        Assert.Equal(a, b);
        Assert.Equal(b, c);
    }
}
