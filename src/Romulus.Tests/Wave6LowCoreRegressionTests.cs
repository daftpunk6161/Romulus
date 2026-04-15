using Romulus.Contracts.Ports;
using Romulus.Core.Scoring;
using Romulus.Core.SetParsing;
using Xunit;

namespace Romulus.Tests;

public sealed class Wave6LowCoreRegressionTests : IDisposable
{
    private readonly string _tempDir;

    public Wave6LowCoreRegressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "romulus-wave6-low-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void DB007_GdiParser_UsesLastQuoteAsClosingBoundary_InSource()
    {
        var sourcePath = Path.Combine(GetRepoRoot(), "src", "Romulus.Core", "SetParsing", "GdiSetParser.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("LastIndexOf('\"')", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IndexOf('\"', quoteStart + 1)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DB008_CcdMissingFiles_WhenDescriptorDoesNotExist_ReturnsEmpty()
    {
        var missing = CcdSetParser.GetMissingFiles(Path.Combine(_tempDir, "missing.ccd"));

        Assert.Empty(missing);
    }

    [Fact]
    public void DB009_MdsRelatedFiles_NormalizesRelativeDescriptorPath_ForIoLookups()
    {
        var relativeFolder = Path.Combine("wave6", Guid.NewGuid().ToString("N"));
        var relativeMdsPath = Path.Combine(relativeFolder, "game.mds");
        var absoluteMdsPath = Path.GetFullPath(relativeMdsPath);
        var absoluteMdfPath = Path.GetFullPath(Path.Combine(relativeFolder, "game.mdf"));

        var io = new StrictPathSetParserIo([absoluteMdsPath, absoluteMdfPath]);
        var related = MdsSetParser.GetRelatedFiles(relativeMdsPath, io);

        Assert.Single(related);
        Assert.Equal(absoluteMdfPath, related[0], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void DB029_MdsMissingFiles_WhenDescriptorDoesNotExist_ReturnsEmpty()
    {
        var missing = MdsSetParser.GetMissingFiles(Path.Combine(_tempDir, "missing.mds"));

        Assert.Empty(missing);
    }

    [Fact]
    public void DB023_RegionRank_IgnoresWhitespaceEntriesWithoutPenalizingValidOrder()
    {
        var preferOrder = new[] { "EU", " ", "", "US", "JP" };

        var euScore = FormatScorer.GetRegionScore("EU", preferOrder);
        var usScore = FormatScorer.GetRegionScore("US", preferOrder);
        var jpScore = FormatScorer.GetRegionScore("JP", preferOrder);

        Assert.Equal(1000, euScore);
        Assert.Equal(999, usScore);
        Assert.Equal(998, jpScore);
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class StrictPathSetParserIo : ISetParserIo
    {
        private readonly HashSet<string> _existing;

        public StrictPathSetParserIo(IEnumerable<string> absoluteExistingPaths)
        {
            _existing = new HashSet<string>(
                absoluteExistingPaths.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase);
        }

        public bool Exists(string path)
        {
            if (!Path.IsPathRooted(path))
                return false;

            try
            {
                return _existing.Contains(Path.GetFullPath(path));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        public IEnumerable<string> ReadLines(string path) => Array.Empty<string>();
    }
}