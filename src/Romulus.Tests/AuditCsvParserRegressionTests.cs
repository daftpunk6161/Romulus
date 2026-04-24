using Romulus.Infrastructure.Audit;
using Xunit;

namespace Romulus.Tests;

public sealed class AuditCsvParserRegressionTests : IDisposable
{
    private readonly string _tempDir;

    public AuditCsvParserRegressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Romulus.AuditCsvParser", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ParseCsvLine_UnclosedQuote_ThrowsInvalidDataException()
    {
        Assert.Throws<InvalidDataException>(() => AuditCsvParser.ParseCsvLine("root,old,new,\"MOVE"));
    }

    [Fact]
    public void SanitizeDatAuditCsvField_PrefixesUncPath()
    {
        var sanitized = AuditCsvParser.SanitizeDatAuditCsvField(@"\\evil\share\rom.zip");

        Assert.StartsWith(@"'\\evil\share", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyTrashIntegrity_MalformedQuotedActionRow_IsSkippedAsCorrupt()
    {
        var auditPath = Path.Combine(_tempDir, "audit.csv");
        var oldPath = Path.Combine(_tempDir, "old.rom");
        var newPath = Path.Combine(_tempDir, "new.rom");
        File.WriteAllText(newPath, "x");

        File.WriteAllLines(auditPath,
        [
            "RootPath,OldPath,NewPath,Action,Category,Hash,Reason,Timestamp",
            $"{_tempDir},{oldPath},{newPath},\"MOVE"
        ]);

        var result = RollbackService.VerifyTrashIntegrity(auditPath, [_tempDir]);

        Assert.Equal(0, result.DryRunPlanned);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }
}
