using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Conversion.ToolInvokers;
using Xunit;

namespace RomCleanup.Tests.Conversion.ToolInvokers;

public sealed class PsxtractInvokerTests : IDisposable
{
    private readonly string _root;
    private readonly PsxtractInvoker _sut;

    public PsxtractInvokerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "RomCleanup.PsxtractInvokerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _sut = new PsxtractInvoker(new TestToolRunner(new Dictionary<string, string?>()));
    }

    // ═══ Verify: CHD magic-byte check ═══════════════════════════════

    [Fact]
    public void Verify_ValidChdMagic_ReturnsVerified()
    {
        var targetPath = Path.Combine(_root, "game.chd");
        // CHD v5 magic: "MComprHD" (8 bytes) + padding to >= 0x10
        var header = new byte[0x10];
        "MComprHD"u8.CopyTo(header);
        File.WriteAllBytes(targetPath, header);

        var status = _sut.Verify(targetPath, Cap());

        Assert.Equal(VerificationStatus.Verified, status);
    }

    [Fact]
    public void Verify_InvalidMagic_ReturnsVerifyFailed()
    {
        var targetPath = Path.Combine(_root, "game.chd");
        File.WriteAllBytes(targetPath, new byte[] { 0x50, 0x42, 0x50, 0x00, 0x01, 0x02, 0x03, 0x04 });

        var status = _sut.Verify(targetPath, Cap());

        Assert.Equal(VerificationStatus.VerifyFailed, status);
    }

    [Fact]
    public void Verify_EmptyFile_ReturnsVerifyFailed()
    {
        var targetPath = Path.Combine(_root, "game.chd");
        File.WriteAllBytes(targetPath, Array.Empty<byte>());

        var status = _sut.Verify(targetPath, Cap());

        Assert.Equal(VerificationStatus.VerifyFailed, status);
    }

    [Fact]
    public void Verify_FileTooShort_ReturnsVerifyFailed()
    {
        var targetPath = Path.Combine(_root, "game.chd");
        File.WriteAllBytes(targetPath, "MCom"u8.ToArray());

        var status = _sut.Verify(targetPath, Cap());

        Assert.Equal(VerificationStatus.VerifyFailed, status);
    }

    [Fact]
    public void Verify_FileDoesNotExist_ReturnsVerifyFailed()
    {
        var targetPath = Path.Combine(_root, "nonexistent.chd");

        var status = _sut.Verify(targetPath, Cap());

        Assert.Equal(VerificationStatus.VerifyFailed, status);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        catch { }
    }

    private static ConversionCapability Cap()
    {
        return new ConversionCapability
        {
            SourceExtension = ".pbp",
            TargetExtension = ".chd",
            Tool = new ToolRequirement { ToolName = "psxtract" },
            Command = "convert",
            ApplicableConsoles = null,
            RequiredSourceIntegrity = null,
            ResultIntegrity = SourceIntegrity.Lossless,
            Lossless = true,
            Cost = 0,
            Verification = VerificationMethod.FileExistenceCheck,
            Description = "test psxtract",
            Condition = ConversionCondition.None
        };
    }
}
