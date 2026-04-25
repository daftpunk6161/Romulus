using Romulus.Tests.TestFixtures;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// Block E4 - lock the centralized <see cref="RepoPaths"/> contract that
/// replaced 8 duplicated <c>FindRepoFile</c> and 2 duplicated <c>FindSrcRoot</c>
/// helpers across the test suite.
/// </summary>
public sealed class BlockE_RepoPathsTests
{
    [Fact]
    public void E4_RepoPaths_RepoFile_ReturnsExistingDataFile()
    {
        var path = RepoPaths.RepoFile("data", "consoles.json");
        Assert.True(File.Exists(path), $"Expected consoles.json at {path}");
    }

    [Fact]
    public void E4_RepoPaths_SrcRoot_ContainsKnownProjects()
    {
        var src = RepoPaths.SrcRoot();
        Assert.True(Directory.Exists(Path.Combine(src, "Romulus.Core")),
            "src/Romulus.Core must exist under resolved SrcRoot.");
        Assert.True(Directory.Exists(Path.Combine(src, "Romulus.Infrastructure")),
            "src/Romulus.Infrastructure must exist under resolved SrcRoot.");
        Assert.True(Directory.Exists(Path.Combine(src, "Romulus.Contracts")),
            "src/Romulus.Contracts must exist under resolved SrcRoot.");
    }

    [Fact]
    public void E4_RepoPaths_RepoFile_ThrowsArgumentNullForNullParts()
    {
        Assert.Throws<ArgumentNullException>(() => RepoPaths.RepoFile(null!));
    }
}
