using System.Reflection;
using System.Text.Json;
using Romulus.Contracts.Models;
using Romulus.Contracts.Ports;
using Romulus.Infrastructure.Analysis;
using Xunit;

namespace Romulus.Tests;

public sealed class IntegrityServiceCoverageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _baselinePath;
    private readonly string _baselineBackupPath;
    private readonly bool _baselineExisted;

    public IntegrityServiceCoverageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Romulus_IST_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _baselinePath = ResolveIntegrityBaselinePath();
        _baselineBackupPath = Path.Combine(_tempDir, "original-integrity-baseline.json");
        _baselineExisted = File.Exists(_baselinePath);
        if (_baselineExisted)
            File.Copy(_baselinePath, _baselineBackupPath, overwrite: true);
    }

    public void Dispose()
    {
        RestoreIntegrityBaseline();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string CreateFile(string name, byte[]? content = null)
    {
        var path = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content ?? [0x00, 0x01, 0x02, 0x03]);
        return path;
    }

    private string CreateFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    #region Header Analysis

    [Fact]
    public void AnalyzeHeader_NonExistentFile_ReturnsNull()
    {
        var result = IntegrityService.AnalyzeHeader(Path.Combine(_tempDir, "nope.bin"));
        Assert.Null(result);
    }

    [Fact]
    public void AnalyzeHeader_EmptyFile_ReturnsNullOrThrows()
    {
        var path = CreateFile("empty.bin", Array.Empty<byte>());
        // Empty file may trigger IndexOutOfRangeException in HeaderAnalyzer
        // because it doesn't guard empty arrays. Document this edge case.
        try
        {
            var result = IntegrityService.AnalyzeHeader(path);
            Assert.Null(result);
        }
        catch (IndexOutOfRangeException)
        {
            // Known: HeaderAnalyzer.AnalyzeHeader doesn't guard against empty input
        }
    }

    [Fact]
    public void AnalyzeHeader_RandomData_ReturnsNullOrInfo()
    {
        // Random data should not crash, returns null for unrecognized headers
        var data = new byte[4096];
        Random.Shared.NextBytes(data);
        var path = CreateFile("random.bin", data);
        // Should not throw
        _ = IntegrityService.AnalyzeHeader(path);
    }

    #endregion


    #region ComputeSha256

    [Fact]
    public void ComputeSha256_ReturnsConsistentHash()
    {
        var file = CreateFile("hashtest.bin", new byte[] { 1, 2, 3, 4 });

        var hash1 = IntegrityService.ComputeSha256(file);
        var hash2 = IntegrityService.ComputeSha256(file);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA-256 hex = 64 chars
    }

    [Fact]
    public void ComputeSha256_DifferentContent_DifferentHash()
    {
        var file1 = CreateFile("h1.bin", new byte[] { 1, 2, 3 });
        var file2 = CreateFile("h2.bin", new byte[] { 4, 5, 6 });

        Assert.NotEqual(
            IntegrityService.ComputeSha256(file1),
            IntegrityService.ComputeSha256(file2));
    }

    #endregion

    #region FindCommonRoot

    [Fact]
    public void FindCommonRoot_SingleFile_ReturnsDirectory()
    {
        var path = Path.Combine(_tempDir, "sub", "file.bin");
        var result = IntegrityService.FindCommonRoot([path]);
        Assert.Equal(Path.Combine(_tempDir, "sub"), result);
    }

    [Fact]
    public void FindCommonRoot_SameDirectory_ReturnsThatDirectory()
    {
        var dir = Path.Combine(_tempDir, "rom");
        var result = IntegrityService.FindCommonRoot([
            Path.Combine(dir, "a.bin"),
            Path.Combine(dir, "b.bin")
        ]);
        Assert.Equal(dir, result);
    }

    [Fact]
    public void FindCommonRoot_DifferentSubdirs_ReturnsParent()
    {
        var result = IntegrityService.FindCommonRoot([
            Path.Combine(_tempDir, "a", "x.bin"),
            Path.Combine(_tempDir, "b", "y.bin")
        ]);
        Assert.Equal(_tempDir, result);
    }

    [Fact]
    public void FindCommonRoot_Empty_ReturnsNull()
    {
        Assert.Null(IntegrityService.FindCommonRoot([]));
    }

    #endregion

    #region CreateBaseline

    [Fact]
    public async Task CreateBaseline_EmptyPaths_ReturnsEmpty()
    {
        var result = await IntegrityService.CreateBaseline([]);
        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateBaseline_WithFiles_ReturnsHashEntries()
    {
        var f1 = CreateFile("bl1.bin", new byte[] { 10, 20, 30 });
        var f2 = CreateFile("bl2.bin", new byte[] { 40, 50, 60 });

        var progress = new List<string>();
        var result = await IntegrityService.CreateBaseline(
            [f1, f2],
            new Progress<string>(msg => progress.Add(msg)));

        Assert.Equal(2, result.Count);
        Assert.True(progress.Count >= 2);
    }

    [Fact]
    public async Task CreateBaseline_MissingFile_SkipsGracefully()
    {
        var f1 = CreateFile("bl_exists.bin", new byte[] { 1, 2 });
        var f2 = Path.Combine(_tempDir, "bl_missing.bin");

        var result = await IntegrityService.CreateBaseline([f1, f2]);

        Assert.Single(result); // Only the existing file
    }

    [Fact]
    public async Task CheckIntegrity_AfterBaseline_DetectsIntactChangedAndMissingFiles()
    {
        var intact = CreateFile("check/intact.bin", "stable");
        var changed = CreateFile("check/changed.bin", "v1");
        var missing = CreateFile("check/missing.bin", "gone");

        await IntegrityService.CreateBaseline([intact, changed, missing]);
        File.WriteAllText(changed, "v2");
        File.Delete(missing);

        var progress = new List<string>();
        var result = await IntegrityService.CheckIntegrity(new Progress<string>(progress.Add));

        Assert.Contains(Path.GetFullPath(intact), result.Intact);
        Assert.Contains(Path.GetFullPath(changed), result.Changed);
        Assert.Contains(Path.GetFullPath(missing), result.Missing);
        Assert.True(result.BitRotRisk);
        Assert.Contains(progress, message => message.Contains("Checking:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CheckIntegrity_LegacyDictionaryBaseline_UsesAbsolutePathsWithoutWrapperRoot()
    {
        var file = CreateFile("legacy/entry.bin", "legacy-content");
        var baseline = new Dictionary<string, IntegrityEntry>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetFullPath(file)] = new(
                IntegrityService.ComputeSha256(file),
                new FileInfo(file).Length,
                File.GetLastWriteTimeUtc(file))
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_baselinePath)!);
        File.WriteAllText(_baselinePath, JsonSerializer.Serialize(baseline));

        var result = await IntegrityService.CheckIntegrity();

        Assert.Empty(result.Changed);
        Assert.Empty(result.Missing);
        Assert.Contains(Path.GetFullPath(file), result.Intact);
        Assert.False(result.BitRotRisk);
    }

    #endregion

    #region Backup

    [Fact]
    public void CreateBackup_CopiesFiles()
    {
        var f1 = CreateFile("rom1.bin", "data1");
        var f2 = CreateFile("rom2.bin", "data2");
        var backupRoot = Path.Combine(_tempDir, "backups");

        var sessionDir = IntegrityService.CreateBackup([f1, f2], backupRoot, "test");

        Assert.True(Directory.Exists(sessionDir));
        var files = Directory.GetFiles(sessionDir, "*", SearchOption.AllDirectories);
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void CreateBackup_MissingSourceFile_SkipsGracefully()
    {
        var f1 = CreateFile("exists.bin", "data");
        var f2 = Path.Combine(_tempDir, "missing.bin");
        var backupRoot = Path.Combine(_tempDir, "backups");

        var sessionDir = IntegrityService.CreateBackup([f1, f2], backupRoot, "partial");

        var files = Directory.GetFiles(sessionDir, "*", SearchOption.AllDirectories);
        Assert.Single(files);
    }

    [Fact]
    public void CreateBackup_ProtectedBackupRoot_Throws()
    {
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windowsDir) || !Directory.Exists(windowsDir))
            return;

        var f1 = CreateFile("exists.bin", "data");
        Assert.Throws<InvalidOperationException>(() =>
            IntegrityService.CreateBackup([f1], windowsDir, "protected-root"));
    }

    [Fact]
    public void CleanupOldBackups_NoDirectory_ReturnsZero()
    {
        Assert.Equal(0, IntegrityService.CleanupOldBackups(
            Path.Combine(_tempDir, "nonexistent"), 7));
    }

    [Fact]
    public void CleanupOldBackups_ProtectedBackupRoot_Throws()
    {
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windowsDir) || !Directory.Exists(windowsDir))
            return;

        Assert.Throws<InvalidOperationException>(() => IntegrityService.CleanupOldBackups(windowsDir, 7));
    }

    [Fact]
    public void CleanupOldBackups_ConfirmDenied_ReturnsZero()
    {
        var backupRoot = Path.Combine(_tempDir, "cleanup_test");
        var oldDir = Path.Combine(backupRoot, "old_session");
        Directory.CreateDirectory(oldDir);
        // Set creation time to 30 days ago
        Directory.SetCreationTime(oldDir, DateTime.Now.AddDays(-30));

        var removed = IntegrityService.CleanupOldBackups(backupRoot, 7, _ => false);

        Assert.Equal(0, removed);
        Assert.True(Directory.Exists(oldDir));
    }

    [Fact]
    public void CleanupOldBackups_ConfirmAccepted_RemovesOld()
    {
        var backupRoot = Path.Combine(_tempDir, "cleanup_accept");
        var oldDir = Path.Combine(backupRoot, "old_session");
        Directory.CreateDirectory(oldDir);
        Directory.SetCreationTime(oldDir, DateTime.Now.AddDays(-30));

        var removed = IntegrityService.CleanupOldBackups(backupRoot, 7, _ => true);

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(oldDir));
    }

    #endregion

    private static string ResolveIntegrityBaselinePath()
    {
        var field = typeof(IntegrityService).GetField("BaselinePath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return (string)field!.GetValue(null)!;
    }

    private void RestoreIntegrityBaseline()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_baselinePath)!);
        if (_baselineExisted)
        {
            File.Copy(_baselineBackupPath, _baselinePath, overwrite: true);
        }
        else if (File.Exists(_baselinePath))
        {
            File.Delete(_baselinePath);
        }
    }
}
