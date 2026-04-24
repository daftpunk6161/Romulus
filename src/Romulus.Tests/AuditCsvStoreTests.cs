using Romulus.Infrastructure.Audit;
using Romulus.Contracts.Models;
using Romulus.Infrastructure.FileSystem;
using Xunit;

namespace Romulus.Tests;

public class AuditCsvStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _keyPath;
    private readonly AuditCsvStore _audit;

    public AuditCsvStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Romulus_AuditTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _keyPath = Path.Combine(_tempDir, "audit-signing.key");
        _audit = new AuditCsvStore(keyFilePath: _keyPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void WriteMetadataSidecar_CreatesJsonFile()
    {
        var csvPath = Path.Combine(_tempDir, "audit.csv");
        File.WriteAllLines(csvPath, [
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
            $"{_tempDir},{Path.Combine(_tempDir, "old.rom")},{Path.Combine(_tempDir, "new.rom")},Move,GAME,,,2025-01-01T00:00:00Z"
        ]);

        var metadata = new Dictionary<string, object>
        {
            ["RunId"] = "abc-123",
            ["Mode"] = "DryRun",
            ["Timestamp"] = "2025-01-01T00:00:00Z"
        };

        _audit.WriteMetadataSidecar(csvPath, metadata);

        var sidecar = csvPath + ".meta.json";
        Assert.True(File.Exists(sidecar));
        var content = File.ReadAllText(sidecar);
        Assert.Contains("abc-123", content);
        Assert.Contains("DryRun", content);
        Assert.Contains("CsvSha256", content);
    }

    [Fact]
    public void TestMetadataSidecar_ReturnsTrueIfSidecarIsValid()
    {
        var csvPath = Path.Combine(_tempDir, "audit.csv");
        File.WriteAllLines(csvPath, [
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
            $"{_tempDir},{Path.Combine(_tempDir, "old.rom")},{Path.Combine(_tempDir, "new.rom")},Move,GAME,,,2025-01-01T00:00:00Z"
        ]);
        _audit.WriteMetadataSidecar(csvPath, new Dictionary<string, object>());

        Assert.True(_audit.TestMetadataSidecar(csvPath));
    }

    [Fact]
    public void TestMetadataSidecar_ReturnsFalseIfTampered()
    {
        var csvPath = Path.Combine(_tempDir, "audit.csv");
        File.WriteAllLines(csvPath, [
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
            $"{_tempDir},{Path.Combine(_tempDir, "old.rom")},{Path.Combine(_tempDir, "new.rom")},Move,GAME,,,2025-01-01T00:00:00Z"
        ]);
        _audit.WriteMetadataSidecar(csvPath, new Dictionary<string, object>());

        File.AppendAllText(csvPath, "tampered\n");

        Assert.False(_audit.TestMetadataSidecar(csvPath));
    }

    [Fact]
    public void AppendAuditRow_SanitizesUncPathsForSpreadsheetConsumers()
    {
        var csvPath = Path.Combine(_tempDir, "audit-unc.csv");

        _audit.AppendAuditRow(
            csvPath,
            _tempDir,
            @"\\nas\roms\game.zip",
            Path.Combine(_tempDir, "game.zip"),
            "MOVE");

        var row = File.ReadLines(csvPath).Skip(1).Single();
        var fields = AuditCsvParser.ParseCsvLine(row);

        Assert.StartsWith(@"'\\nas\roms", fields[1], StringComparison.Ordinal);
    }

    [Fact]
    public void CountAuditRows_CountsLogicalCsvRowsWithQuotedNewlines()
    {
        var csvPath = Path.Combine(_tempDir, "audit-multiline.csv");
        File.WriteAllText(
            csvPath,
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp\n" +
            $"{_tempDir},old,new,MOVE,GAME,,\"line 1\nline 2\",2026-04-24T00:00:00Z\n");

        var count = AuditCsvStore.CountAuditRows(csvPath);

        Assert.Equal(1, count);
    }

    [Fact]
    public void AuditSigningService_CorruptPersistedKey_QuarantinesAndThrows()
    {
        var keyPath = Path.Combine(_tempDir, "corrupt.key");
        File.WriteAllText(keyPath, "not-hex");
        var service = new AuditSigningService(new FileSystemAdapter(), keyFilePath: keyPath);

        var ex = Assert.Throws<InvalidOperationException>(() => service.ComputeHmacSha256("payload"));

        Assert.Contains("HMAC key file", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(keyPath));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "quarantine")));
        Assert.Single(Directory.GetFiles(Path.Combine(_tempDir, "quarantine"), "*.bad"));
    }

    [Fact]
    public void VerifyMetadataSidecar_ReplayedOlderCheckpoint_IsRejected()
    {
        var keyPath = Path.Combine(_tempDir, "replay.key");
        var service = new AuditSigningService(new FileSystemAdapter(), keyFilePath: keyPath);
        var csvPath = Path.Combine(_tempDir, "replay.csv");
        var oldPath = Path.Combine(_tempDir, "old.rom");
        var newPath = Path.Combine(_tempDir, "new.rom");

        File.WriteAllText(
            csvPath,
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp\n" +
            $"{_tempDir},{oldPath},{newPath},Move,GAME,abc,first,2026-04-24T00:00:00Z\n");
        service.WriteMetadataSidecar(csvPath, 1);
        var replayCsv = File.ReadAllText(csvPath);
        var replayMeta = File.ReadAllText(csvPath + ".meta.json");

        File.AppendAllText(csvPath, $"{_tempDir},{oldPath}2,{newPath}2,Move,GAME,abc,second,2026-04-24T00:00:01Z\n");
        service.WriteMetadataSidecar(csvPath, 2);

        File.WriteAllText(csvPath, replayCsv);
        File.WriteAllText(csvPath + ".meta.json", replayMeta);

        var ex = Assert.Throws<InvalidDataException>(() => service.VerifyMetadataSidecar(csvPath));
        Assert.Contains("replay", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rollback_WithMissingCsvAndPresentSidecar_ReportsTampered()
    {
        var keyPath = Path.Combine(_tempDir, "missing-csv.key");
        var service = new AuditSigningService(new FileSystemAdapter(), keyFilePath: keyPath);
        var csvPath = Path.Combine(_tempDir, "missing-csv.csv");
        File.WriteAllText(
            csvPath,
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp\n" +
            $"{_tempDir},{Path.Combine(_tempDir, "old.rom")},{Path.Combine(_tempDir, "new.rom")},Move,GAME,abc,reason,2026-04-24T00:00:00Z\n");
        service.WriteMetadataSidecar(csvPath, 1);
        File.Delete(csvPath);

        var result = service.Rollback(csvPath, [_tempDir], [_tempDir], dryRun: true);

        Assert.True(result.Tampered);
        Assert.Equal("AUDIT_CSV_MISSING_WITH_SIDECAR", result.IntegrityError);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public void AppendAuditRow_WritesImmediateCheckpoint_AndDetectsTailTampering()
    {
        var auditPath = Path.Combine(_tempDir, "tail-protected.csv");

        _audit.AppendAuditRow(
            auditPath,
            _tempDir,
            Path.Combine(_tempDir, "old.zip"),
            Path.Combine(_tempDir, "trash", "old.zip"),
            "MOVE",
            "Game",
            "hash",
            "tail-checkpoint");

        Assert.True(_audit.TestMetadataSidecar(auditPath));

        File.AppendAllText(
            auditPath,
            $"{_tempDir},{Path.Combine(_tempDir, "evil.zip")},{Path.Combine(_tempDir, "trash", "evil.zip")},MOVE,Game,,tampered,{DateTimeOffset.UtcNow:O}\n");

        Assert.False(_audit.TestMetadataSidecar(auditPath));
    }

    [Fact]
    public void TestMetadataSidecar_ReturnsFalseIfMissing()
    {
        var csvPath = Path.Combine(_tempDir, "nope.csv");
        Assert.False(_audit.TestMetadataSidecar(csvPath));
    }

    [Fact]
    public void Rollback_DryRun_DoesNotMoveFiles()
    {
        var csvPath = Path.Combine(_tempDir, "audit.csv");
        var oldPath = Path.Combine(_tempDir, "original", "game.rom");
        var newPath = Path.Combine(_tempDir, "moved", "game.rom");

        // Create the moved file
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.WriteAllText(newPath, "data");

        // Write CSV with header + move entry
        File.WriteAllLines(csvPath, new[]
        {
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
            $"{_tempDir},{oldPath},{newPath},Move,GAME,abc,dedupe,2025-01-01"
        });
        _audit.WriteMetadataSidecar(csvPath, new Dictionary<string, object>());

        var result = _audit.Rollback(csvPath,
            allowedRestoreRoots: new[] { _tempDir },
            allowedCurrentRoots: new[] { _tempDir },
            dryRun: true);

        Assert.Single(result);
        Assert.Equal(oldPath, result[0]);
        // File should NOT have been moved in dry run
        Assert.True(File.Exists(newPath));
        Assert.False(File.Exists(oldPath));
    }

    [Fact]
    public void Rollback_DryRun_MissingCurrentFile_DoesNotReportRestorablePath()
    {
        var csvPath = Path.Combine(_tempDir, "audit.csv");
        var oldPath = Path.Combine(_tempDir, "original", "game.rom");
        var newPath = Path.Combine(_tempDir, "moved", "game.rom");

        // Create then remove the current file to simulate stale audit entries.
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.WriteAllText(newPath, "data");
        File.Delete(newPath);

        File.WriteAllLines(csvPath, new[]
        {
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
            $"{_tempDir},{oldPath},{newPath},Move,GAME,abc,dedupe,2025-01-01"
        });

        var result = _audit.Rollback(csvPath,
            allowedRestoreRoots: new[] { _tempDir },
            allowedCurrentRoots: new[] { _tempDir },
            dryRun: true);

        Assert.Empty(result);
    }

    [Fact]
    public void Rollback_ActualMove_RestoresFile()
    {
        var csvPath = Path.Combine(_tempDir, "audit.csv");
        var oldDir = Path.Combine(_tempDir, "original");
        var oldPath = Path.Combine(oldDir, "game.rom");
        var newPath = Path.Combine(_tempDir, "moved", "game.rom");

        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
        File.WriteAllText(newPath, "data");

        File.WriteAllLines(csvPath, new[]
        {
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
            $"{_tempDir},{oldPath},{newPath},Move,GAME,abc,dedupe,2025-01-01"
        });
        _audit.WriteMetadataSidecar(csvPath, new Dictionary<string, object> { ["Mode"] = "Move" });

        var result = _audit.Rollback(csvPath,
            allowedRestoreRoots: new[] { _tempDir },
            allowedCurrentRoots: new[] { _tempDir },
            dryRun: false);

        Assert.Single(result);
        Assert.True(File.Exists(oldPath));
        Assert.False(File.Exists(newPath));
    }

    [Fact]
    public void Rollback_PathOutsideAllowedRoot_Skipped()
    {
        var csvPath = Path.Combine(_tempDir, "audit.csv");
        File.WriteAllLines(csvPath, new[]
        {
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
            @"C:\Other,C:\Other\game.rom,C:\Other\moved\game.rom,Move,GAME,abc,dedupe,2025-01-01"
        });

        var result = _audit.Rollback(csvPath,
            allowedRestoreRoots: new[] { _tempDir },
            allowedCurrentRoots: new[] { _tempDir },
            dryRun: true);

        Assert.Empty(result);
    }

    [Fact]
    public void Rollback_NonExistentCsv_ReturnsEmpty()
    {
        var result = _audit.Rollback(
            Path.Combine(_tempDir, "nope.csv"),
            new[] { _tempDir }, new[] { _tempDir });

        Assert.Empty(result);
    }

    [Fact]
    public void AppendAuditRows_WhenReplaceFails_LeavesExistingAuditUnchanged()
    {
        var csvPath = Path.Combine(_tempDir, "audit.csv");
        File.WriteAllLines(csvPath, new[]
        {
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
            $"{_tempDir},{Path.Combine(_tempDir, "a.rom")},{Path.Combine(_tempDir, "b.rom")},Move,GAME,,,2025-01-01"
        });

        var originalContent = File.ReadAllText(csvPath);
        using var lockHandle = new FileStream(csvPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.ThrowsAny<IOException>(() => _audit.AppendAuditRows(csvPath,
        [
            new AuditAppendRow(_tempDir, "source.iso", "target.chd", "CONVERT", "GAME", "", "format-convert:test"),
            new AuditAppendRow(_tempDir, "source.iso", "trash\\source.iso", "CONVERT_SOURCE", "GAME", "", "source-convert-trash")
        ]));

        lockHandle.Dispose();
        Assert.Equal(originalContent, File.ReadAllText(csvPath));
    }
}
