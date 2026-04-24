using System.ComponentModel;
using System.Runtime.InteropServices;
using Romulus.Contracts.Ports;
using Romulus.Infrastructure.FileSystem;
using Romulus.Infrastructure.Safety;
using Xunit;

namespace Romulus.Tests;

public sealed class FileSystemAllowedRootTests : IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes);

    private readonly string _tempDir;

    public FileSystemAllowedRootTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FsAllowedRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void MoveItemSafely_WithAllowedRootOutsideDestination_ReturnsNull()
    {
        IFileSystem fs = new FileSystemAdapter();
        var source = Path.Combine(_tempDir, "a.txt");
        var outside = Path.Combine(_tempDir, "outside");
        var target = Path.Combine(outside, "a.txt");
        var allowedRoot = Path.Combine(_tempDir, "allowed");

        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(allowedRoot);
        File.WriteAllText(source, "x");

        var result = fs.MoveItemSafely(source, target, allowedRoot);

        Assert.Null(result);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void MoveItemSafely_WithAllowedRootInsideDestination_MovesFile()
    {
        IFileSystem fs = new FileSystemAdapter();
        var source = Path.Combine(_tempDir, "b.txt");
        var allowedRoot = Path.Combine(_tempDir, "allowed");
        var target = Path.Combine(allowedRoot, "b.txt");

        Directory.CreateDirectory(allowedRoot);
        File.WriteAllText(source, "x");

        var result = fs.MoveItemSafely(source, target, allowedRoot);

        Assert.NotNull(result);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(target));
    }

    [Fact]
    public void MoveItemSafely_WithMultipleHardLinks_ThrowsAndKeepsSource()
    {
        if (!OperatingSystem.IsWindows())
            return;

        IFileSystem fs = new FileSystemAdapter();
        var source = Path.Combine(_tempDir, "hardlink-source.bin");
        var secondLink = Path.Combine(_tempDir, "hardlink-second.bin");
        var target = Path.Combine(_tempDir, "target", "hardlink-source.bin");

        File.WriteAllText(source, "same-bytes");
        var created = CreateHardLink(secondLink, source, IntPtr.Zero);
        Assert.True(created, new Win32Exception(Marshal.GetLastWin32Error()).Message);

        var ex = Assert.Throws<InvalidOperationException>(() => fs.MoveItemSafely(source, target));

        Assert.Contains("multiple hard links", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(secondLink));
        Assert.False(File.Exists(target));
    }

    [Theory]
    [InlineData(@"\\?\C:\Roms\Game.zip")]
    [InlineData(@"\\.\C:\Roms\Game.zip")]
    [InlineData(@"C:\Roms\Game.zip:evil")]
    public void AllowedRootPathPolicy_RejectsDeviceAndAdsPaths(string unsafePath)
    {
        var policy = new AllowedRootPathPolicy([_tempDir]);

        Assert.False(policy.IsPathAllowed(unsafePath));
    }

    [Fact]
    public void AllowedRootPathPolicy_UsesCentralNormalizer_ForTrailingDotSegments()
    {
        var policy = new AllowedRootPathPolicy([_tempDir]);
        var unsafePath = Path.Combine(_tempDir, "folder.", "game.zip");

        Assert.False(policy.IsPathAllowed(unsafePath));
    }
}
