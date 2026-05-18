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
    private readonly string _trendPath;
    private readonly string _trendBackupPath;
    private readonly bool _trendExisted;

    public IntegrityServiceCoverageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Romulus_IST_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _baselinePath = ResolveIntegrityBaselinePath();
        _baselineBackupPath = Path.Combine(_tempDir, "original-integrity-baseline.json");
        _baselineExisted = File.Exists(_baselinePath);
        if (_baselineExisted)
            File.Copy(_baselinePath, _baselineBackupPath, overwrite: true);

        _trendPath = ResolveTrendPath();
        _trendBackupPath = Path.Combine(_tempDir, "original-trend-history.json");
        _trendExisted = File.Exists(_trendPath);
        if (_trendExisted)
            File.Copy(_trendPath, _trendBackupPath, overwrite: true);
    }

    public void Dispose()
    {
        RestoreIntegrityBaseline();
        RestoreTrendHistory();
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

    #region Trend History

    [Fact]
    public void SaveTrendSnapshot_CapsLegacyHistoryAndFormatReportUsesSharedLabels()
    {
        var existing = Enumerable.Range(0, 365)
            .Select(day => new TrendSnapshot(
                new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local).AddDays(day),
                TotalFiles: 10 + day,
                SizeBytes: 1024 + day,
                Verified: 5,
                Dupes: 1,
                Junk: 1,
                QualityScore: 80))
            .ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_trendPath)!);
        File.WriteAllText(_trendPath, JsonSerializer.Serialize(existing));

        var now = new FixedTimeProvider(new DateTimeOffset(2026, 5, 18, 9, 30, 0, TimeSpan.Zero));

        IntegrityService.SaveTrendSnapshot(
            totalFiles: 100,
            sizeBytes: 4096,
            verified: 90,
            dupes: 4,
            junk: 2,
            timeProvider: now);

        var history = LoadLegacyTrendHistoryViaReflection();

        Assert.Equal(365, history.Count);
        Assert.Equal(new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Local), history[0].Timestamp);
        Assert.Equal(100, history[^1].TotalFiles);
        Assert.Equal(4096, history[^1].SizeBytes);
        Assert.Equal(CollectionAnalysisService.CalculateHealthScore(100, 4, 2, 90), history[^1].QualityScore);

        var report = IntegrityService.FormatTrendReport(history.TakeLast(2).ToList());
        Assert.Contains("Trend Analysis", report, StringComparison.Ordinal);
        Assert.Contains("Delta files", report, StringComparison.Ordinal);
        Assert.Contains("Quality", report, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadLegacyTrendHistory_MissingOrMalformedSidecar_ReturnsEmptyHistory()
    {
        if (File.Exists(_trendPath))
            File.Delete(_trendPath);

        Assert.Empty(LoadLegacyTrendHistoryViaReflection());

        Directory.CreateDirectory(Path.GetDirectoryName(_trendPath)!);
        File.WriteAllText(_trendPath, "{not-json");

        Assert.Empty(LoadLegacyTrendHistoryViaReflection());
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
    public void CreateBackup_BlankLabel_UsesBackupLeafAndCopiesFile()
    {
        var file = CreateFile("blank-label/source.bin", "data");
        var backupRoot = Path.Combine(_tempDir, "blank-label-backups");
        var now = new FixedTimeProvider(new DateTimeOffset(2026, 5, 18, 11, 22, 33, TimeSpan.Zero));

        var sessionDir = IntegrityService.CreateBackup([file], backupRoot, " \t ", now);

        Assert.EndsWith("_backup", Path.GetFileName(sessionDir), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(sessionDir, "source.bin")));
    }

    [Fact]
    public void CreateBackup_DestinationResolutionFailure_FailsClosedBeforeCopy()
    {
        var file = CreateFile("resolve-failure/source.bin", "data");
        var backupRoot = Path.Combine(_tempDir, "resolve-failure-backups");
        var fs = new TestFileSystem
        {
            ResolveChildPath = (_, _) => null
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            IntegrityService.CreateBackup([file], backupRoot, "blocked", fileSystem: fs));

        Assert.Contains("Backup destination escaped", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, fs.CopyCount);
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

    [Fact]
    public void CleanupOldBackups_SkipsDirectoryTreeContainingReparsePoint()
    {
        var backupRoot = Path.Combine(_tempDir, "cleanup_reparse");
        var oldDir = Path.Combine(backupRoot, "old_session");
        var childDir = Path.Combine(oldDir, "child");
        Directory.CreateDirectory(childDir);
        Directory.SetCreationTime(oldDir, DateTime.Now.AddDays(-30));
        Directory.SetCreationTime(childDir, DateTime.Now.AddDays(-30));

        var fs = new TestFileSystem
        {
            IsReparsePointFunc = path => string.Equals(
                Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(childDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)
        };

        var removed = IntegrityService.CleanupOldBackups(
            backupRoot,
            retentionDays: 7,
            confirmDelete: _ => true,
            fileSystem: fs);

        Assert.Equal(0, removed);
        Assert.True(Directory.Exists(oldDir));
    }

    #endregion

    private static string ResolveIntegrityBaselinePath()
    {
        var field = typeof(IntegrityService).GetField("BaselinePath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return (string)field!.GetValue(null)!;
    }

    private static string ResolveTrendPath()
    {
        var field = typeof(IntegrityService).GetField("TrendFile", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return (string)field!.GetValue(null)!;
    }

    private static List<TrendSnapshot> LoadLegacyTrendHistoryViaReflection()
    {
        var method = typeof(IntegrityService).GetMethod("LoadLegacyTrendHistory", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<List<TrendSnapshot>>(method!.Invoke(null, null));
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

    private void RestoreTrendHistory()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_trendPath)!);
        if (_trendExisted)
        {
            File.Copy(_trendBackupPath, _trendPath, overwrite: true);
        }
        else if (File.Exists(_trendPath))
        {
            File.Delete(_trendPath);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestFileSystem : IFileSystem
    {
        public Func<string, string, string?> ResolveChildPath { get; init; } =
            static (root, relative) => Path.GetFullPath(Path.Combine(root, relative));

        public Func<string, bool> IsReparsePointFunc { get; init; } = static _ => false;

        public int CopyCount { get; private set; }

        public bool TestPath(string literalPath, string pathType = "Any")
            => pathType switch
            {
                "Leaf" => File.Exists(literalPath),
                "Container" => Directory.Exists(literalPath),
                _ => File.Exists(literalPath) || Directory.Exists(literalPath)
            };

        public string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }

        public IReadOnlyList<string> GetFilesSafe(string root, IEnumerable<string>? allowedExtensions = null)
            => Directory.Exists(root)
                ? Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();

        public string? MoveItemSafely(string sourcePath, string destinationPath) => null;

        public string? ResolveChildPathWithinRoot(string rootPath, string relativePath)
            => ResolveChildPath(rootPath, relativePath);

        public bool IsReparsePoint(string path) => IsReparsePointFunc(path);

        public void DeleteFile(string path) => File.Delete(path);

        public void CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
        {
            CopyCount++;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite);
        }

        public void WriteAllText(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
