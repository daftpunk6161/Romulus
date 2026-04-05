using RomCleanup.Contracts;
using RomCleanup.Infrastructure.Paths;
using Xunit;

namespace RomCleanup.Tests;

public class AppStoragePathResolverTests
{
    [Fact]
    public void ResolvePortableRootDirectory_IsUnderBaseDirectory()
    {
        var expected = Path.Combine(AppContext.BaseDirectory, ".romcleanup");
        Assert.Equal(expected, AppStoragePathResolver.ResolvePortableRootDirectory());
    }

    [Fact]
    public void ResolveRoamingAppDirectory_MatchesCurrentMode()
    {
        var resolved = AppStoragePathResolver.ResolveRoamingAppDirectory();

        if (AppStoragePathResolver.IsPortableMode())
        {
            Assert.Equal(AppStoragePathResolver.ResolvePortableRootDirectory(), resolved);
            return;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.StartsWith(appData, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(AppIdentity.AppFolderName, resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveLocalAppDirectory_MatchesCurrentMode()
    {
        var resolved = AppStoragePathResolver.ResolveLocalAppDirectory();

        if (AppStoragePathResolver.IsPortableMode())
        {
            Assert.Equal(AppStoragePathResolver.ResolvePortableRootDirectory(), resolved);
            return;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.StartsWith(localAppData, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(AppIdentity.AppFolderName, resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveRoamingPath_CombinesAndSkipsEmptySegments()
    {
        var path = AppStoragePathResolver.ResolveRoamingPath("reports", "", "daily.html");
        var expected = Path.Combine(AppStoragePathResolver.ResolveRoamingAppDirectory(), "reports", "daily.html");

        Assert.Equal(expected, path);
    }

    [Fact]
    public void ResolveLocalPath_CombinesAndSkipsEmptySegments()
    {
        var path = AppStoragePathResolver.ResolveLocalPath("cache", "", "hashes.json");
        var expected = Path.Combine(AppStoragePathResolver.ResolveLocalAppDirectory(), "cache", "hashes.json");

        Assert.Equal(expected, path);
    }
}