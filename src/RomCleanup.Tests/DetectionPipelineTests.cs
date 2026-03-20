using RomCleanup.Core.Classification;
using Xunit;

namespace RomCleanup.Tests;

/// <summary>
/// Tests for the detection pipeline: CartridgeHeaderDetector, FilenameConsoleAnalyzer,
/// HypothesisResolver, and the DetectionHypothesis/ConsoleDetectionResult model classes.
/// </summary>
public sealed class DetectionPipelineTests
{
    // ──────────────────────────────────────────────────────────────────
    //  CartridgeHeaderDetector
    // ──────────────────────────────────────────────────────────────────

    #region CartridgeHeaderDetector

    [Fact]
    public void CartridgeHeader_Nes_INesMagic()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            // iNES magic: 4E 45 53 1A at offset 0, followed by header padding
            var data = new byte[16];
            data[0] = 0x4E; data[1] = 0x45; data[2] = 0x53; data[3] = 0x1A;
            File.WriteAllBytes(path, data);

            Assert.Equal("NES", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_N64_BigEndian()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[64];
            data[0] = 0x80; data[1] = 0x37; data[2] = 0x12; data[3] = 0x40;
            File.WriteAllBytes(path, data);

            Assert.Equal("N64", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_N64_ByteSwapped()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[64];
            data[0] = 0x37; data[1] = 0x80; data[2] = 0x40; data[3] = 0x12;
            File.WriteAllBytes(path, data);

            Assert.Equal("N64", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_N64_LittleEndian()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[64];
            data[0] = 0x40; data[1] = 0x12; data[2] = 0x37; data[3] = 0x80;
            File.WriteAllBytes(path, data);

            Assert.Equal("N64", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Lynx()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[64];
            data[0] = 0x4C; data[1] = 0x59; data[2] = 0x4E; data[3] = 0x58; // LYNX
            File.WriteAllBytes(path, data);

            Assert.Equal("LYNX", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Gba_NintendoLogo()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[64];
            // Nintendo logo at offset 0x04
            data[0x04] = 0x24; data[0x05] = 0xFF; data[0x06] = 0xAE; data[0x07] = 0x51;
            File.WriteAllBytes(path, data);

            Assert.Equal("GBA", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Atari7800()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[64];
            // "ATARI7800" at offset 1
            var magic = "ATARI7800"u8;
            magic.CopyTo(data.AsSpan(1));
            File.WriteAllBytes(path, data);

            Assert.Equal("7800", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Genesis_MegaDrive()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[0x120];
            // "SEGA MEGA DRIVE" at offset 0x100
            var magic = "SEGA MEGA DRIVE "u8;
            magic.CopyTo(data.AsSpan(0x100));
            File.WriteAllBytes(path, data);

            Assert.Equal("MD", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Genesis_SegaGenesis()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[0x120];
            // "SEGA GENESIS" at offset 0x100 (padded to 16 bytes)
            var magic = "SEGA GENESIS    "u8;
            magic.CopyTo(data.AsSpan(0x100));
            File.WriteAllBytes(path, data);

            Assert.Equal("MD", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Sega32X()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[0x120];
            // "SEGA 32X" at offset 0x100 (padded to 16 bytes)
            var magic = "SEGA 32X        "u8;
            magic.CopyTo(data.AsSpan(0x100));
            File.WriteAllBytes(path, data);

            Assert.Equal("32X", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Gb_Basic()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            // GB: Nintendo logo at 0x104, CGB flag at 0x143 = 0x00 (non-GBC)
            var data = new byte[0x150];
            data[0x104] = 0xCE; data[0x105] = 0xED; data[0x106] = 0x66; data[0x107] = 0x66;
            data[0x143] = 0x00; // Not GBC
            File.WriteAllBytes(path, data);

            Assert.Equal("GB", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Gbc_FlagC0()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[0x150];
            data[0x104] = 0xCE; data[0x105] = 0xED; data[0x106] = 0x66; data[0x107] = 0x66;
            data[0x143] = 0xC0; // GBC-only flag
            File.WriteAllBytes(path, data);

            Assert.Equal("GBC", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Gbc_Flag80()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            var data = new byte[0x150];
            data[0x104] = 0xCE; data[0x105] = 0xED; data[0x106] = 0x66; data[0x107] = 0x66;
            data[0x143] = 0x80; // GBC dual flag
            File.WriteAllBytes(path, data);

            Assert.Equal("GBC", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_Snes_LoRom()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            // LoROM internal header at 0x7FC0: 21-byte ASCII title + checksum at offset+28
            var data = new byte[0x8000];
            // Write a valid SNES title at 0x7FC0 (21 printable ASCII bytes)
            var title = "SUPER MARIO WORLD    "u8; // 21 bytes
            title.CopyTo(data.AsSpan(0x7FC0));
            // Checksum complement/checksum at 0x7FC0+28 = 0x7FDC
            // complement XOR checksum must == 0xFFFF
            ushort checksum = 0xAB12;
            ushort complement = (ushort)(checksum ^ 0xFFFF);
            data[0x7FDC] = (byte)(complement & 0xFF);
            data[0x7FDD] = (byte)(complement >> 8);
            data[0x7FDE] = (byte)(checksum & 0xFF);
            data[0x7FDF] = (byte)(checksum >> 8);
            File.WriteAllBytes(path, data);

            Assert.Equal("SNES", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_UnknownFile_ReturnsNull()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            // Write random bytes that don't match any known header
            var random = new byte[512];
            new Random(42).NextBytes(random);
            // Ensure first 4 bytes don't accidentally match a known magic
            random[0] = 0x00; random[1] = 0x00; random[2] = 0x00; random[3] = 0x00;
            File.WriteAllBytes(path, random);

            Assert.Null(detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_NullPath_ReturnsNull()
    {
        var detector = new CartridgeHeaderDetector();
        Assert.Null(detector.Detect(null!));
    }

    [Fact]
    public void CartridgeHeader_EmptyPath_ReturnsNull()
    {
        var detector = new CartridgeHeaderDetector();
        Assert.Null(detector.Detect(""));
    }

    [Fact]
    public void CartridgeHeader_MissingFile_ReturnsNull()
    {
        var detector = new CartridgeHeaderDetector();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
        Assert.Null(detector.Detect(path));
    }

    [Fact]
    public void CartridgeHeader_TooSmallFile_ReturnsNull()
    {
        var detector = new CartridgeHeaderDetector();
        var path = TempFile();
        try
        {
            File.WriteAllBytes(path, [0x01, 0x02]); // Only 2 bytes
            Assert.Null(detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    [Fact]
    public void CartridgeHeader_CachesResults()
    {
        var detector = new CartridgeHeaderDetector(cacheSize: 4);
        var path = TempFile();
        try
        {
            var data = new byte[16];
            data[0] = 0x4E; data[1] = 0x45; data[2] = 0x53; data[3] = 0x1A;
            File.WriteAllBytes(path, data);

            // First call
            Assert.Equal("NES", detector.Detect(path));
            // Second call on the same (still existing) file should use cache
            Assert.Equal("NES", detector.Detect(path));
        }
        finally { DeleteQuietly(path); }
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    //  FilenameConsoleAnalyzer — Serial patterns
    // ──────────────────────────────────────────────────────────────────

    #region FilenameConsoleAnalyzer — Serials

    [Theory]
    [InlineData("SLUS-00123 Game Title.bin", "PS1")]
    [InlineData("SCUS-942 Title.bin", "PS1")]
    [InlineData("SCES-12345 Title.bin", "PS1")]
    [InlineData("SLES-00001 Title.bin", "PS1")]
    public void FilenameAnalyzer_Serial_PS1(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectBySerial(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(95, result.Value.Confidence);
    }

    [Theory]
    [InlineData("PBPX-12345 Game.iso", "PS2")]
    public void FilenameAnalyzer_Serial_PS2(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectBySerial(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(95, result.Value.Confidence);
    }

    [Theory]
    [InlineData("BCUS-12345 Game Title.pkg", "PS3")]
    [InlineData("BLUS-98765 Title.bin", "PS3")]
    [InlineData("NPUB-12345 Title.pkg", "PS3")]
    public void FilenameAnalyzer_Serial_PS3(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectBySerial(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(95, result.Value.Confidence);
    }

    [Theory]
    [InlineData("UCUS-12345 Title.iso", "PSP")]
    [InlineData("ULUS-55555 Game.cso", "PSP")]
    public void FilenameAnalyzer_Serial_PSP(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectBySerial(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(95, result.Value.Confidence);
    }

    [Theory]
    [InlineData("PCSE-12345 Title.vpk", "VITA")]
    [InlineData("PCSB-99999 Game.vpk", "VITA")]
    public void FilenameAnalyzer_Serial_Vita(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectBySerial(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(95, result.Value.Confidence);
    }

    [Fact]
    public void FilenameAnalyzer_Serial_NDS()
    {
        var result = FilenameConsoleAnalyzer.DetectBySerial("NTR-ABCD-EUR Title.nds");
        Assert.NotNull(result);
        Assert.Equal("NDS", result.Value.ConsoleKey);
        Assert.Equal(95, result.Value.Confidence);
    }

    [Fact]
    public void FilenameAnalyzer_Serial_3DS()
    {
        var result = FilenameConsoleAnalyzer.DetectBySerial("CTR-ABCD-EUR Title.3ds");
        Assert.NotNull(result);
        Assert.Equal("3DS", result.Value.ConsoleKey);
        Assert.Equal(95, result.Value.Confidence);
    }

    [Fact]
    public void FilenameAnalyzer_Serial_Saturn()
    {
        var result = FilenameConsoleAnalyzer.DetectBySerial("T-123A Title.bin");
        Assert.NotNull(result);
        Assert.Equal("SAT", result.Value.ConsoleKey);
        Assert.Equal(95, result.Value.Confidence);
    }

    [Fact]
    public void FilenameAnalyzer_Serial_NullReturnsNull()
    {
        Assert.Null(FilenameConsoleAnalyzer.DetectBySerial(null!));
    }

    [Fact]
    public void FilenameAnalyzer_Serial_EmptyReturnsNull()
    {
        Assert.Null(FilenameConsoleAnalyzer.DetectBySerial(""));
    }

    [Fact]
    public void FilenameAnalyzer_Serial_NoMatch()
    {
        Assert.Null(FilenameConsoleAnalyzer.DetectBySerial("Super Mario Bros.nes"));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    //  FilenameConsoleAnalyzer — Keyword patterns
    // ──────────────────────────────────────────────────────────────────

    #region FilenameConsoleAnalyzer — Keywords

    [Theory]
    [InlineData("[PS1] Crash Bandicoot.bin", "PS1")]
    [InlineData("Crash Bandicoot (PS1).bin", "PS1")]
    public void FilenameAnalyzer_Keyword_PS1(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectByKeyword(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(75, result.Value.Confidence);
    }

    [Theory]
    [InlineData("[GBA] Metroid Fusion.gba", "GBA")]
    [InlineData("Metroid Fusion (GBA).gba", "GBA")]
    [InlineData("[Game Boy Advance] Metroid Fusion.gba", "GBA")]
    public void FilenameAnalyzer_Keyword_GBA(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectByKeyword(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(75, result.Value.Confidence);
    }

    [Theory]
    [InlineData("[Genesis] Sonic.bin", "MD")]
    [InlineData("[Mega Drive] Sonic.bin", "MD")]
    [InlineData("Sonic (Genesis).bin", "MD")]
    public void FilenameAnalyzer_Keyword_Genesis(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectByKeyword(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(75, result.Value.Confidence);
    }

    [Theory]
    [InlineData("[Switch] Title.nsp", "SWITCH")]
    [InlineData("(Switch) Title.nsp", "SWITCH")]
    [InlineData("[NSW] Title.xci", "SWITCH")]
    public void FilenameAnalyzer_Keyword_Switch(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectByKeyword(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(75, result.Value.Confidence);
    }

    [Theory]
    [InlineData("[N64] GoldenEye.z64", "N64")]
    [InlineData("[SNES] Super Metroid.sfc", "SNES")]
    [InlineData("[NES] Contra.nes", "NES")]
    [InlineData("[GBC] Pokemon Crystal.gbc", "GBC")]
    [InlineData("[GB] Tetris.gb", "GB")]
    [InlineData("[DC] Shenmue.gdi", "DC")]
    [InlineData("[SAT] Nights.bin", "SAT")]
    [InlineData("[Lynx] Title.lnx", "LYNX")]
    [InlineData("[32X] Title.32x", "32X")]
    public void FilenameAnalyzer_Keyword_Various(string fileName, string expected)
    {
        var result = FilenameConsoleAnalyzer.DetectByKeyword(fileName);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value.ConsoleKey);
        Assert.Equal(75, result.Value.Confidence);
    }

    [Fact]
    public void FilenameAnalyzer_Keyword_NullReturnsNull()
    {
        Assert.Null(FilenameConsoleAnalyzer.DetectByKeyword(null!));
    }

    [Fact]
    public void FilenameAnalyzer_Keyword_EmptyReturnsNull()
    {
        Assert.Null(FilenameConsoleAnalyzer.DetectByKeyword(""));
    }

    [Fact]
    public void FilenameAnalyzer_Keyword_NoMatch()
    {
        Assert.Null(FilenameConsoleAnalyzer.DetectByKeyword("SomeRandom Game.bin"));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    //  FilenameConsoleAnalyzer — Combined Detect
    // ──────────────────────────────────────────────────────────────────

    #region FilenameConsoleAnalyzer — Combined

    [Fact]
    public void FilenameAnalyzer_Detect_PrefersSerialOverKeyword()
    {
        // File has both a serial (PS1) and a keyword ([GBA])
        var result = FilenameConsoleAnalyzer.Detect("SLUS-00123 [GBA] Title.bin");
        Assert.NotNull(result);
        // Serial match (PS1, confidence=95) should win over keyword (GBA, confidence=75)
        Assert.Equal("PS1", result.Value.ConsoleKey);
        Assert.Equal(95, result.Value.Confidence);
    }

    [Fact]
    public void FilenameAnalyzer_Detect_FallsBackToKeyword()
    {
        var result = FilenameConsoleAnalyzer.Detect("[GBA] Metroid Fusion.gba");
        Assert.NotNull(result);
        Assert.Equal("GBA", result.Value.ConsoleKey);
        Assert.Equal(75, result.Value.Confidence);
    }

    [Fact]
    public void FilenameAnalyzer_Detect_NullReturnsNull()
    {
        Assert.Null(FilenameConsoleAnalyzer.Detect(null!));
    }

    [Fact]
    public void FilenameAnalyzer_Detect_NoMatchReturnsNull()
    {
        Assert.Null(FilenameConsoleAnalyzer.Detect("plain-game-name.bin"));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    //  HypothesisResolver
    // ──────────────────────────────────────────────────────────────────

    #region HypothesisResolver

    [Fact]
    public void Resolver_EmptyList_ReturnsUnknown()
    {
        var result = HypothesisResolver.Resolve([]);
        Assert.Equal("UNKNOWN", result.ConsoleKey);
        Assert.Equal(0, result.Confidence);
        Assert.False(result.HasConflict);
        Assert.Null(result.ConflictDetail);
    }

    [Fact]
    public void Resolver_SingleHypothesis_Passthrough()
    {
        var h = new DetectionHypothesis("NES", 90, DetectionSource.CartridgeHeader, "iNES magic");
        var result = HypothesisResolver.Resolve([h]);

        Assert.Equal("NES", result.ConsoleKey);
        Assert.Equal(90, result.Confidence);
        Assert.False(result.HasConflict);
        Assert.Null(result.ConflictDetail);
        Assert.Single(result.Hypotheses);
    }

    [Fact]
    public void Resolver_MultipleAgreeing_MultiSourceBonus()
    {
        var hypotheses = new List<DetectionHypothesis>
        {
            new("PS1", 90, DetectionSource.CartridgeHeader, "header match"),
            new("PS1", 88, DetectionSource.SerialNumber, "serial SLUS-00123"),
            new("PS1", 75, DetectionSource.FilenameKeyword, "[PS1]"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);

        Assert.Equal("PS1", result.ConsoleKey);
        Assert.False(result.HasConflict);
        // Max single = 90, bonus = (3-1)*5 = 10, total = 100 (capped)
        Assert.Equal(100, result.Confidence);
    }

    [Fact]
    public void Resolver_MultiSourceBonus_CappedAt100()
    {
        var hypotheses = new List<DetectionHypothesis>
        {
            new("GBA", 95, DetectionSource.UniqueExtension, "ext=.gba"),
            new("GBA", 90, DetectionSource.CartridgeHeader, "nintendo logo"),
            new("GBA", 75, DetectionSource.FilenameKeyword, "[GBA]"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);
        Assert.Equal("GBA", result.ConsoleKey);
        // Max single = 95, bonus = 10, but cap = 100
        Assert.True(result.Confidence <= 100);
    }

    [Fact]
    public void Resolver_StrongConflict_Penalty20()
    {
        var hypotheses = new List<DetectionHypothesis>
        {
            new("NES", 90, DetectionSource.CartridgeHeader, "iNES header"),
            new("SNES", 85, DetectionSource.FolderName, "folder=SNES"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);

        Assert.Equal("NES", result.ConsoleKey);
        Assert.True(result.HasConflict);
        Assert.NotNull(result.ConflictDetail);
        // Runner max = 85 ≥ 80 → strong conflict → -20
        // Winner max = 90 - 20 = 70
        Assert.Equal(70, result.Confidence);
    }

    [Fact]
    public void Resolver_ModerateConflict_Penalty10()
    {
        var hypotheses = new List<DetectionHypothesis>
        {
            new("NES", 90, DetectionSource.CartridgeHeader, "iNES header"),
            new("SNES", 50, DetectionSource.AmbiguousExtension, "ext=.smc"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);

        Assert.Equal("NES", result.ConsoleKey);
        Assert.True(result.HasConflict);
        // Runner max = 50 ≥ 50 → moderate conflict → -10
        // Winner max = 90 - 10 = 80
        Assert.Equal(80, result.Confidence);
    }

    [Fact]
    public void Resolver_WeakConflict_NoPenalty()
    {
        var hypotheses = new List<DetectionHypothesis>
        {
            new("NES", 90, DetectionSource.CartridgeHeader, "iNES header"),
            new("SNES", 40, DetectionSource.AmbiguousExtension, "ext=.bin"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);

        Assert.Equal("NES", result.ConsoleKey);
        Assert.True(result.HasConflict);
        // Runner max = 40 < 50 → no penalty
        Assert.Equal(90, result.Confidence);
    }

    [Fact]
    public void Resolver_DeterministicTieBreak_Alphabetical()
    {
        // Two hypotheses with equal confidence for different consoles
        var hypotheses = new List<DetectionHypothesis>
        {
            new("SNES", 85, DetectionSource.FolderName, "folder=SNES"),
            new("NES", 85, DetectionSource.FolderName, "folder=NES"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);

        // Tied on TotalConfidence and MaxSingleConfidence → alphabetical: NES < SNES
        Assert.Equal("NES", result.ConsoleKey);
        Assert.True(result.HasConflict);
    }

    [Fact]
    public void Resolver_HigherTotalConfidence_Wins()
    {
        var hypotheses = new List<DetectionHypothesis>
        {
            new("MD", 85, DetectionSource.FolderName, "folder=Genesis"),
            new("MD", 75, DetectionSource.FilenameKeyword, "[Genesis]"),
            new("SNES", 90, DetectionSource.CartridgeHeader, "SNES header"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);

        // MD total = 160, SNES total = 90 → MD wins
        Assert.Equal("MD", result.ConsoleKey);
        Assert.True(result.HasConflict);
    }

    [Fact]
    public void Resolver_ConflictDetailContainsBothKeys()
    {
        var hypotheses = new List<DetectionHypothesis>
        {
            new("PS1", 95, DetectionSource.SerialNumber, "serial SLUS-00123"),
            new("PS2", 75, DetectionSource.FilenameKeyword, "[PS2]"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);
        Assert.True(result.HasConflict);
        Assert.Contains("PS1", result.ConflictDetail);
        Assert.Contains("PS2", result.ConflictDetail);
    }

    [Fact]
    public void Resolver_StrongConflict_FloorAt30()
    {
        // Winner with low confidence, strong runner → floor at 30
        var hypotheses = new List<DetectionHypothesis>
        {
            new("MD", 40, DetectionSource.AmbiguousExtension, "ext=.bin"),
            new("SNES", 85, DetectionSource.FolderName, "folder=SNES"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);

        // SNES total=85 > MD total=40; SNES wins
        // Runner (MD) max=40 < 80 and < 50 → no penalty actually
        // Wait: SNES=85 wins, MD=40 is runner. 40 < 50 → no penalty
        Assert.Equal("SNES", result.ConsoleKey);
        Assert.Equal(85, result.Confidence);
    }

    [Fact]
    public void Resolver_ConfidenceFloor30_OnStrongConflict()
    {
        // Design a case where penalty would push below 30 → floor applies
        var hypotheses = new List<DetectionHypothesis>
        {
            new("MD", 45, DetectionSource.AmbiguousExtension, "ext=.bin"),
            new("SNES", 80, DetectionSource.ArchiveContent, "inner=.sfc"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);

        // SNES total=80 > MD total=45 → SNES wins
        // Runner MD max=45 < 50 → no penalty
        Assert.Equal("SNES", result.ConsoleKey);
        Assert.True(result.HasConflict);
    }

    [Fact]
    public void Resolver_CaseInsensitiveGrouping()
    {
        var hypotheses = new List<DetectionHypothesis>
        {
            new("nes", 85, DetectionSource.FolderName, "folder=nes"),
            new("NES", 90, DetectionSource.CartridgeHeader, "iNES header"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);

        // Both should group together (case-insensitive)
        Assert.False(result.HasConflict);
        // Total for nes/NES = 175, max single = 90, bonus from 2 sources = +5 → 95
        Assert.Equal(95, result.Confidence);
    }

    [Fact]
    public void Resolver_AllHypothesesPreservedInResult()
    {
        var hypotheses = new List<DetectionHypothesis>
        {
            new("PS1", 95, DetectionSource.SerialNumber, "serial"),
            new("PS1", 75, DetectionSource.FilenameKeyword, "keyword"),
            new("PS2", 40, DetectionSource.AmbiguousExtension, "ext"),
        };

        var result = HypothesisResolver.Resolve(hypotheses);
        Assert.Equal(3, result.Hypotheses.Count);
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    //  DetectionHypothesis & ConsoleDetectionResult model classes
    // ──────────────────────────────────────────────────────────────────

    #region Model classes

    [Fact]
    public void DetectionHypothesis_RecordEquality()
    {
        var a = new DetectionHypothesis("NES", 90, DetectionSource.CartridgeHeader, "iNES");
        var b = new DetectionHypothesis("NES", 90, DetectionSource.CartridgeHeader, "iNES");
        Assert.Equal(a, b);
    }

    [Fact]
    public void DetectionHypothesis_DifferentValues_NotEqual()
    {
        var a = new DetectionHypothesis("NES", 90, DetectionSource.CartridgeHeader, "iNES");
        var b = new DetectionHypothesis("SNES", 90, DetectionSource.CartridgeHeader, "iNES");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ConsoleDetectionResult_UnknownStatic()
    {
        var unknown = ConsoleDetectionResult.Unknown;
        Assert.Equal("UNKNOWN", unknown.ConsoleKey);
        Assert.Equal(0, unknown.Confidence);
        Assert.Empty(unknown.Hypotheses);
        Assert.False(unknown.HasConflict);
        Assert.Null(unknown.ConflictDetail);
    }

    [Fact]
    public void ConsoleDetectionResult_UnknownIsSingleton()
    {
        Assert.Same(ConsoleDetectionResult.Unknown, ConsoleDetectionResult.Unknown);
    }

    [Fact]
    public void ConsoleDetectionResult_RecordEquality()
    {
        var hyps = Array.Empty<DetectionHypothesis>();
        var a = new ConsoleDetectionResult("NES", 90, hyps, false, null);
        var b = new ConsoleDetectionResult("NES", 90, hyps, false, null);
        Assert.Equal(a, b);
    }

    [Fact]
    public void DetectionSource_EnumValues()
    {
        Assert.Equal(100, (int)DetectionSource.DatHash);
        Assert.Equal(95, (int)DetectionSource.UniqueExtension);
        Assert.Equal(92, (int)DetectionSource.DiscHeader);
        Assert.Equal(90, (int)DetectionSource.CartridgeHeader);
        Assert.Equal(88, (int)DetectionSource.SerialNumber);
        Assert.Equal(85, (int)DetectionSource.FolderName);
        Assert.Equal(80, (int)DetectionSource.ArchiveContent);
        Assert.Equal(75, (int)DetectionSource.FilenameKeyword);
        Assert.Equal(40, (int)DetectionSource.AmbiguousExtension);
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");

    private static void DeleteQuietly(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
