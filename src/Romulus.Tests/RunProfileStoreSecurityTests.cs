using System.Text.Json;
using Romulus.Contracts.Models;
using Romulus.Infrastructure.Profiles;
using Xunit;

namespace Romulus.Tests;

public sealed class RunProfileStoreSecurityTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _profileDir;

    public RunProfileStoreSecurityTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "Romulus_ProfileStoreSecurity_" + Guid.NewGuid().ToString("N"));
        _profileDir = Path.Combine(_tempRoot, "profiles");
        Directory.CreateDirectory(_profileDir);
    }

    [Fact]
    public async Task TryGetAsync_WithParentTraversalId_DoesNotReadOutsideProfileDirectory()
    {
        var outsidePath = Path.Combine(_tempRoot, "outside.json");
        await File.WriteAllTextAsync(outsidePath, JsonSerializer.Serialize(new RunProfileDocument
        {
            Version = 1,
            Id = "outside",
            Name = "Outside",
            Description = "outside",
            Settings = new RunProfileSettings
            {
                Mode = "DryRun"
            }
        }));

        var store = new JsonRunProfileStore(new RunProfilePathOptions
        {
            DirectoryPath = _profileDir
        });

        var profile = await store.TryGetAsync("..\\outside");

        Assert.Null(profile);
    }

    [Fact]
    public async Task DeleteAsync_WithParentTraversalId_DoesNotDeleteOutsideProfileDirectory()
    {
        var outsidePath = Path.Combine(_tempRoot, "outside.json");
        await File.WriteAllTextAsync(outsidePath, JsonSerializer.Serialize(new RunProfileDocument
        {
            Version = 1,
            Id = "outside",
            Name = "Outside",
            Description = "outside",
            Settings = new RunProfileSettings
            {
                Mode = "DryRun"
            }
        }));

        var store = new JsonRunProfileStore(new RunProfilePathOptions
        {
            DirectoryPath = _profileDir
        });

        var deleted = await store.DeleteAsync("..\\outside");

        Assert.False(deleted);
        Assert.True(File.Exists(outsidePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
