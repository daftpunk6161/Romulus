using Romulus.Infrastructure.Safety;
using Xunit;

namespace Romulus.Tests;

public sealed class AllowedRootPathPolicyCoverageTests
{
    // ─── Constructor ───

    [Fact]
    public void Ctor_NullRoots_NotEnforced()
    {
        var policy = new AllowedRootPathPolicy(null);
        Assert.False(policy.IsEnforced);
        Assert.Empty(policy.AllowedRoots);
    }

    [Fact]
    public void Ctor_EmptyList_NotEnforced()
    {
        var policy = new AllowedRootPathPolicy([]);
        Assert.False(policy.IsEnforced);
    }

    [Fact]
    public void Ctor_OnlyWhitespace_NotEnforced()
    {
        var policy = new AllowedRootPathPolicy(["", "   ", "\t"]);
        Assert.False(policy.IsEnforced);
        Assert.Empty(policy.AllowedRoots);
    }

    [Fact]
    public void Ctor_ValidRoot_Enforced()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.True(policy.IsEnforced);
        Assert.Single(policy.AllowedRoots);
    }

    [Fact]
    public void Ctor_DeduplicatesRoots_CaseInsensitive()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms", @"c:\roms"]);
        Assert.Single(policy.AllowedRoots);
    }

    [Fact]
    public void Ctor_FiltersWhitespaceEntries()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms", "", "  ", @"D:\Games"]);
        Assert.Equal(2, policy.AllowedRoots.Count);
    }

    [Fact]
    public void Ctor_NormalizesTrailingSeparator()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms\"]);
        // The stored root should not end with a separator
        Assert.DoesNotMatch("[/\\\\]$", policy.AllowedRoots[0]);
    }

    // ─── IsPathAllowed: Not enforced ───

    [Fact]
    public void IsPathAllowed_NotEnforced_AlwaysTrue()
    {
        var policy = new AllowedRootPathPolicy(null);
        Assert.True(policy.IsPathAllowed(@"C:\Anything\Goes"));
    }

    [Fact]
    public void IsPathAllowed_NotEnforced_TrueEvenForNull()
    {
        var policy = new AllowedRootPathPolicy(null);
        // Not enforced → permissive
        Assert.True(policy.IsPathAllowed(null!));
    }

    // ─── IsPathAllowed: Enforced - null/blank ───

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsPathAllowed_Enforced_NullOrBlank_False(string? path)
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.False(policy.IsPathAllowed(path!));
    }

    // ─── IsPathAllowed: Exact match ───

    [Fact]
    public void IsPathAllowed_ExactRoot_True()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.True(policy.IsPathAllowed(@"C:\Roms"));
    }

    [Fact]
    public void IsPathAllowed_ExactRoot_CaseInsensitive()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.True(policy.IsPathAllowed(@"c:\roms"));
    }

    [Fact]
    public void IsPathAllowed_ExactRoot_WithTrailingSeparator()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.True(policy.IsPathAllowed(@"C:\Roms\"));
    }

    // ─── IsPathAllowed: Subpath ───

    [Fact]
    public void IsPathAllowed_SubPath_True()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.True(policy.IsPathAllowed(@"C:\Roms\SNES\game.zip"));
    }

    [Fact]
    public void IsPathAllowed_NestedSubPath_True()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.True(policy.IsPathAllowed(@"C:\Roms\SNES\USA\game.zip"));
    }

    // ─── IsPathAllowed: Outside root ───

    [Fact]
    public void IsPathAllowed_OutsideRoot_False()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.False(policy.IsPathAllowed(@"D:\Other\file.zip"));
    }

    [Fact]
    public void IsPathAllowed_SiblingFolder_False()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.False(policy.IsPathAllowed(@"C:\RomsBackup\file.zip"));
    }

    [Fact]
    public void IsPathAllowed_ParentFolder_False()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms\SNES"]);
        Assert.False(policy.IsPathAllowed(@"C:\Roms\file.zip"));
    }

    // ─── IsPathAllowed: Path traversal attempts ───

    [Fact]
    public void IsPathAllowed_TraversalDoubleDot_Blocked()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        // C:\Roms\..\Windows resolves to C:\Windows
        Assert.False(policy.IsPathAllowed(@"C:\Roms\..\Windows\System32"));
    }

    [Fact]
    public void IsPathAllowed_TraversalForwardSlash_Blocked()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.False(policy.IsPathAllowed(@"C:\Roms/../Windows"));
    }

    // ─── IsPathAllowed: Multiple roots ───

    [Fact]
    public void IsPathAllowed_MultipleRoots_MatchesAny()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms", @"D:\Games"]);
        Assert.True(policy.IsPathAllowed(@"C:\Roms\file.zip"));
        Assert.True(policy.IsPathAllowed(@"D:\Games\file.zip"));
        Assert.False(policy.IsPathAllowed(@"E:\Other\file.zip"));
    }

    // ─── IsPathAllowed: Invalid paths ───

    [Fact]
    public void IsPathAllowed_InvalidPathCharacters_False()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        // Pipe and null chars are invalid
        Assert.False(policy.IsPathAllowed("C:\\Roms\\fi\0le.zip"));
    }

    // ─── AllowedRoots property ───

    [Fact]
    public void AllowedRoots_ReturnsNormalizedPaths()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms\", @"D:\Games\"]);
        foreach (var root in policy.AllowedRoots)
        {
            Assert.False(root.EndsWith('\\'));
            Assert.False(root.EndsWith('/'));
        }
    }

    [Fact]
    public void IsEnforced_WithValidRoots_True()
    {
        var policy = new AllowedRootPathPolicy([@"C:\Roms"]);
        Assert.True(policy.IsEnforced);
    }

    [Fact]
    public void IsEnforced_WithOnlyFilteredRoots_False()
    {
        var policy = new AllowedRootPathPolicy([""]);
        Assert.False(policy.IsEnforced);
    }

    [Fact]
    public void IsPathAllowed_FileSymlinkInsideRoot_ReturnsFalse()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "Romulus_AllowedRoots_" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "Romulus_AllowedRoots_Outside_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);

        try
        {
            var outsideFile = Path.Combine(outside, "outside.rom");
            File.WriteAllText(outsideFile, "x");

            var linkPath = Path.Combine(root, "linked.rom");
            try
            {
                File.CreateSymbolicLink(linkPath, outsideFile);
            }
            catch
            {
                return;
            }

            var policy = new AllowedRootPathPolicy([root]);
            Assert.False(policy.IsPathAllowed(linkPath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch
            {
            }

            try
            {
                if (Directory.Exists(outside))
                    Directory.Delete(outside, true);
            }
            catch
            {
            }
        }
    }
}
