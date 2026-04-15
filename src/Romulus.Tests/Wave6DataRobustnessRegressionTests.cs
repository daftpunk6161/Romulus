using System.IO.Compression;
using System.Text.Json;
using Romulus.Contracts.Models;
using Romulus.Infrastructure.Dat;
using Romulus.Infrastructure.Hashing;
using Xunit;

namespace Romulus.Tests;

public sealed class Wave6DataRobustnessRegressionTests : IDisposable
{
    private readonly string _tempDir;

    public Wave6DataRobustnessRegressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "romulus-wave6-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void DB013_DatIndex_WhenHashEntryChangesName_RemovesOldNameIndexEntry()
    {
        var index = new DatIndex();
        index.Add("NES", "hash-1", "Old Name");
        index.Add("NES", "hash-1", "New Name");

        Assert.Null(index.LookupByName("NES", "Old Name"));
        Assert.Equal("New Name", index.LookupByName("NES", "New Name")?.GameName);
    }

    [Fact]
    public void DB016_M3uRewrite_DoesNotRemoveAllAmbiguousFileNameMappings_InSource()
    {
        var sourcePath = Path.Combine(GetRepoRoot(), "src", "Romulus.Infrastructure", "Sorting", "ConsoleSorter.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("fileNameRenameMap.Remove(ambiguousFileName)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DB020_PbpRegistry_pbp2chdCommand_UsesChdTargetExtension()
    {
        var registryPath = Path.Combine(GetRepoRoot(), "data", "conversion-registry.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(registryPath));

        var capability = doc.RootElement
            .GetProperty("capabilities")
            .EnumerateArray()
            .First(entry =>
                string.Equals(entry.GetProperty("sourceExtension").GetString(), ".pbp", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("pbp2chd", capability.GetProperty("command").GetString());
        Assert.Equal(".chd", capability.GetProperty("targetExtension").GetString());
    }

    [Fact]
    public void DB021_HardlinkService_IncludesReFsSupport_InSource()
    {
        var sourcePath = Path.Combine(GetRepoRoot(), "src", "Romulus.Infrastructure", "Linking", "HardlinkService.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("\"ReFS\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DB027_DatRepository_TrimmedHashStillMatchesLookup()
    {
        var datPath = Path.Combine(_tempDir, "whitespace-hash.dat");
        File.WriteAllText(datPath,
            "<datafile><game name=\"Whitespace Game\"><rom name=\"game.bin\" sha1=\"  abc123  \" /></game></datafile>");

        var adapter = new DatRepositoryAdapter();
        var index = adapter.GetDatIndex(_tempDir, new Dictionary<string, string> { ["NES"] = Path.GetFileName(datPath) });

        Assert.Equal("Whitespace Game", index.Lookup("NES", "abc123"));
    }

    [Fact]
    public void DB030_ArchiveHash_ZipZeroLengthFiles_AreHashedLikeRegularEntries()
    {
        var zipPath = Path.Combine(_tempDir, "zero-byte-entry.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntry("empty.bin");
        }

        var service = new ArchiveHashService();
        var hashes = service.GetArchiveHashes(zipPath, "SHA1");

        Assert.Single(hashes);
        Assert.Equal("DA39A3EE5E6B4B0D3255BFEF95601890AFD80709", hashes[0], StringComparer.OrdinalIgnoreCase);
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
}