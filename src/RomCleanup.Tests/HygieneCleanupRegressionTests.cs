using System.Reflection;
using RomCleanup.Infrastructure.Configuration;
using RomCleanup.Infrastructure.Paths;
using RomCleanup.UI.Wpf.Models;
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

    // ═══ Dead Result<T> type must stay removed ════════════════════════

    [Fact]
    public void RemovedResultT_MustNotExistInWpfAssembly()
    {
        // Result<T> was dead code (GUI-042 discriminated result, never used in production).
        // OperationResult from Contracts is the canonical result type.
        var resultType = WpfAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "Result`1");

        Assert.Null(resultType);
    }

    // ═══ FeatureCommandKeys completeness ══════════════════════════════

    [Fact]
    public void FeatureCommandKeys_AllConstantsAreNonEmpty()
    {
        var fields = typeof(FeatureCommandKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string));

        foreach (var field in fields)
        {
            var value = (string?)field.GetValue(null);
            Assert.False(string.IsNullOrWhiteSpace(value),
                $"FeatureCommandKeys.{field.Name} must not be null or empty");
        }
    }

    [Theory]
    [InlineData(nameof(FeatureCommandKeys.ProfileShare))]
    [InlineData(nameof(FeatureCommandKeys.CliCommandCopy))]
    [InlineData(nameof(FeatureCommandKeys.SchedulerApply))]
    [InlineData(nameof(FeatureCommandKeys.SystemTray))]
    public void NewlyAddedCommandKeys_ExistInFeatureCommandKeys(string fieldName)
    {
        var field = typeof(FeatureCommandKeys).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field);
    }

    // ═══ Round 2: Dead methods must stay removed ══════════════════════

    [Theory]
    [InlineData("BuildCoverReport")]
    [InlineData("BuildFilterReport")]
    public void RemovedFeatureServiceMethods_MustNotExist(string methodName)
    {
        var featureServiceType = WpfAssembly.GetTypes()
            .First(t => t.Name == "FeatureService");
        var method = featureServiceType.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        Assert.Null(method);
    }

    [Fact]
    public void RemovedInitFeatureCommands_MustNotExistOnMainViewModel()
    {
        var vmType = WpfAssembly.GetTypes()
            .First(t => t.Name == "MainViewModel");
        var method = vmType.GetMethod("InitFeatureCommands",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Null(method);
    }

    // ═══ Round 2: ToolPathValidator security checks ═══════════════════

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToolPathValidator_EmptyPath_ReturnsNullWithNoReason(string? path)
    {
        var (normalized, reason) = ToolPathValidator.Validate(path);
        Assert.Null(normalized);
        Assert.Null(reason);
    }

    [Fact]
    public void ToolPathValidator_NonExistentFile_RejectsWithReason()
    {
        var (normalized, reason) = ToolPathValidator.Validate(@"C:\NonExistent\tool.exe");
        Assert.Null(normalized);
        Assert.NotNull(reason);
        Assert.Contains("nicht gefunden", reason);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".dll")]
    [InlineData(".ps1")]
    [InlineData(".sh")]
    public void ToolPathValidator_DisallowedExtension_Rejects(string ext)
    {
        // Use a temp file with the wrong extension to test extension check
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-tool{ext}");
        try
        {
            File.WriteAllText(tempFile, "dummy");
            var (normalized, reason) = ToolPathValidator.Validate(tempFile);
            Assert.Null(normalized);
            Assert.NotNull(reason);
            Assert.Contains("nicht erlaubt", reason);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData(".exe")]
    [InlineData(".bat")]
    [InlineData(".cmd")]
    public void ToolPathValidator_AllowedExtension_Accepts(string ext)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-tool{ext}");
        try
        {
            File.WriteAllText(tempFile, "dummy");
            var (normalized, reason) = ToolPathValidator.Validate(tempFile);
            Assert.NotNull(normalized);
            Assert.Null(reason);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ToolPathValidator_ValidateOrEmpty_ReturnsEmptyForInvalid()
    {
        var result = ToolPathValidator.ValidateOrEmpty(@"C:\NonExistent\tool.exe");
        Assert.Equal("", result);
    }

    // ═══ Round 2: Orphaned AllowedToolExtensions must stay removed ════

    [Fact]
    public void AllowedToolExtensions_MustNotExistInSettingsLoader()
    {
        var settingsLoaderType = typeof(RomCleanup.Infrastructure.Configuration.SettingsLoader);
        var field = settingsLoaderType.GetField("AllowedToolExtensions",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.Null(field);
    }
}
