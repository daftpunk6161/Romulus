using System.Reflection;
using System.Text.Json;
using Romulus.CLI;
using Romulus.Contracts.Models;
using Romulus.Infrastructure.Analysis;
using Romulus.Infrastructure.Index;
using Xunit;
using CliProgram = Romulus.CLI.Program;

namespace Romulus.Tests;

public sealed class CliIntegrityHealthSubcommandTests : IDisposable
{
    private readonly string _root;
    private readonly string _stateRoot;
    private readonly string _collectionDbPath;
    private readonly string _auditKeyPath;

    public CliIntegrityHealthSubcommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Romulus.CliIntegrityHealth", Guid.NewGuid().ToString("N"));
        _stateRoot = Path.Combine(Path.GetTempPath(), "Romulus.CliIntegrityHealth.State", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_stateRoot);
        _collectionDbPath = Path.Combine(_stateRoot, "collection.db");
        _auditKeyPath = Path.Combine(_stateRoot, "audit-signing.key");
    }

    public void Dispose()
    {
        TryDelete(_root);
        TryDelete(_stateRoot);
    }

    [Fact]
    public async Task HealthSubcommand_Json_UsesIsolatedCollectionIndex()
    {
        using (var index = new LiteDbCollectionIndex(_collectionDbPath))
        {
            await index.UpsertEntriesAsync(
            [
                IndexEntry("verified.sfc", FileCategory.Game, datMatch: true, consoleKey: "SNES"),
                IndexEntry("junk.sfc", FileCategory.Junk, datMatch: false, consoleKey: "SNES"),
                IndexEntry("other.nes", FileCategory.Game, datMatch: true, consoleKey: "NES")
            ]);
        }

        using var overrides = CreateCliPathOverrides();
        var (exit, stdout, _) = await ProgramTestRunner.RunSubcommandAsync(() =>
            InvokePrivateCliSubcommandAsync("SubcommandHealthAsync", new CliRunOptions
            {
                ConsoleKey = "SNES",
                ExportFormat = "json"
            }));

        Assert.Equal(0, exit);
        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        Assert.Equal("SNES", root.GetProperty("ConsoleFilter").GetString());
        var breakdown = root.GetProperty("Breakdown");
        Assert.Equal(2, breakdown.GetProperty("TotalFiles").GetInt32());
        Assert.Equal(1, breakdown.GetProperty("Games").GetInt32());
        Assert.Equal(1, breakdown.GetProperty("Junk").GetInt32());
        Assert.Equal(1, breakdown.GetProperty("DatVerified").GetInt32());
    }

    [Fact]
    public async Task IntegrityBaselineAndCheckSubcommands_RoundTripAndReportChangedFiles()
    {
        using var baseline = IntegrityBaselineScope.Capture();
        var first = CreateFile("roms/first.bin", "stable");
        var second = CreateFile("roms/second.bin", "before");

        var (baselineExit, baselineStdout, _) = await ProgramTestRunner.RunSubcommandAsync(() =>
            InvokePrivateCliSubcommandAsync("SubcommandIntegrityBaselineAsync", new CliRunOptions
            {
                Roots = [_root]
            }));

        Assert.Equal(0, baselineExit);
        Assert.Contains("Baseline created: 2 entries", baselineStdout, StringComparison.Ordinal);

        var (cleanExit, cleanStdout, _) = await ProgramTestRunner.RunSubcommandAsync(() =>
            InvokePrivateCliSubcommandAsync("SubcommandIntegrityCheckAsync"));

        Assert.Equal(0, cleanExit);
        Assert.Contains("Intact:   2", cleanStdout, StringComparison.Ordinal);
        Assert.Contains("Changed:  0", cleanStdout, StringComparison.Ordinal);

        File.WriteAllText(second, "after");

        var (changedExit, changedStdout, changedStderr) = await ProgramTestRunner.RunSubcommandAsync(() =>
            InvokePrivateCliSubcommandAsync("SubcommandIntegrityCheckAsync"));

        Assert.Equal(1, changedExit);
        Assert.Contains("Intact:   1", changedStdout, StringComparison.Ordinal);
        Assert.Contains("Changed:  1", changedStdout, StringComparison.Ordinal);
        Assert.Contains(Path.GetFileName(second), changedStderr, StringComparison.Ordinal);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    private IDisposable CreateCliPathOverrides()
        => CliProgram.SetTestPathOverrides(new CliPathOverrides
        {
            CollectionDbPath = _collectionDbPath,
            AuditSigningKeyPath = _auditKeyPath
        });

    private string CreateFile(string relativePath, string content)
    {
        var path = Path.GetFullPath(Path.Combine(_root, relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private CollectionIndexEntry IndexEntry(string fileName, FileCategory category, bool datMatch, string consoleKey)
    {
        var root = Path.Combine(_root, consoleKey);
        var path = Path.Combine(root, fileName);
        return new CollectionIndexEntry
        {
            Path = path,
            Root = root,
            FileName = fileName,
            Extension = Path.GetExtension(fileName),
            SizeBytes = 1024,
            LastWriteUtc = new DateTime(2026, 5, 18, 8, 0, 0, DateTimeKind.Utc),
            LastScannedUtc = new DateTime(2026, 5, 18, 8, 1, 0, DateTimeKind.Utc),
            ConsoleKey = consoleKey,
            GameKey = Path.GetFileNameWithoutExtension(fileName),
            Category = category,
            DatMatch = datMatch,
            SortDecision = datMatch ? SortDecision.DatVerified : SortDecision.Review,
            DecisionClass = datMatch ? DecisionClass.DatVerified : DecisionClass.Unknown,
            EvidenceTier = datMatch ? EvidenceTier.Tier0_ExactDat : EvidenceTier.Tier4_Unknown,
            PrimaryMatchKind = datMatch ? MatchKind.ExactDatHash : MatchKind.None
        };
    }

    private static async Task<int> InvokePrivateCliSubcommandAsync(string methodName, params object[] args)
    {
        var method = typeof(CliProgram).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = method!.Invoke(null, args);
        return await ((Task<int>)result!).ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class IntegrityBaselineScope : IDisposable
    {
        private readonly string _backupPath;
        private readonly bool _existed;

        private IntegrityBaselineScope(string path, string backupPath, bool existed)
        {
            Path = path;
            _backupPath = backupPath;
            _existed = existed;
        }

        public string Path { get; }

        public static IntegrityBaselineScope Capture()
        {
            var field = typeof(IntegrityService).GetField("BaselinePath", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);

            var path = (string)field!.GetValue(null)!;
            var backupPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "Romulus_CliIntegrityBaseline_" + Guid.NewGuid().ToString("N") + ".json");
            var existed = File.Exists(path);
            if (existed)
                File.Copy(path, backupPath, overwrite: true);

            return new IntegrityBaselineScope(path, backupPath, existed);
        }

        public void Dispose()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            if (_existed)
            {
                File.Copy(_backupPath, Path, overwrite: true);
                File.Delete(_backupPath);
            }
            else if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
