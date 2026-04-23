using System.IO.Compression;
using Romulus.Contracts.Models;
using Romulus.Contracts.Ports;
using Romulus.Core.Classification;
using Romulus.Infrastructure.Audit;
using Romulus.Infrastructure.FileSystem;
using Romulus.Infrastructure.Hashing;
using Romulus.Infrastructure.Metrics;
using Romulus.Infrastructure.Orchestration;
using Xunit;

namespace Romulus.Tests;

public class EnrichmentPipelinePhaseAuditPhase3And4Tests : IDisposable
{
    private readonly string _tempDir;

    public EnrichmentPipelinePhaseAuditPhase3And4Tests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Romulus_EnrichmentAudit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Execute_DatBiosMatch_OverridesCategoryToBios()
    {
        var root = Path.Combine(_tempDir, "phase3");
        Directory.CreateDirectory(root);
        var filePath = CreateFile(root, "mystery.bin", 32);

        var hashService = new FileHashService();
        var hash = hashService.GetHash(filePath, "SHA1");
        Assert.False(string.IsNullOrWhiteSpace(hash));

        var datIndex = new DatIndex();
        datIndex.Add("PSX", hash!, "PlayStation BIOS", "SCPH1001.BIN", isBios: true);

        var detector = new ConsoleDetector([
            new ConsoleInfo(
                Key: "SNES",
                DisplayName: "SNES",
                DiscBased: false,
                UniqueExts: ["sfc"],
                AmbigExts: [],
                FolderAliases: ["SNES"])
        ]);

        var scan = new[] { new ScannedFileEntry(root, filePath, ".bin") };
        var options = new RunOptions
        {
            Roots = [root],
            Extensions = [".bin"],
            Mode = "DryRun",
            HashType = "SHA1"
        };

        var phase = new EnrichmentPipelinePhase();
        var result = phase.Execute(
            new EnrichmentPhaseInput(scan, detector, hashService, null, datIndex),
            CreateContext(options),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.True(candidate.DatMatch);
        Assert.Equal("PSX", candidate.ConsoleKey);
        Assert.Equal("PlayStation BIOS", candidate.DatGameName);
        Assert.Equal(FileCategory.Bios, candidate.Category);
        Assert.Equal(SortDecision.DatVerified, candidate.SortDecision);
    }

    [Fact]
    public void Execute_UnknownConsoleAmbiguousDat_UsesHypothesisIntersectionAndSetsReview()
    {
        var root = Path.Combine(_tempDir, "phase4");
        var snesFolder = Path.Combine(root, "SNES");
        Directory.CreateDirectory(snesFolder);
        var filePath = CreateFile(snesFolder, "mystery (PS1).bin", 48);

        var hashService = new FileHashService();
        var hash = hashService.GetHash(filePath, "SHA1");
        Assert.False(string.IsNullOrWhiteSpace(hash));

        var datIndex = new DatIndex();
        datIndex.Add("MD", hash!, "Game MD", "game-md.bin");
        datIndex.Add("PS1", hash!, "Game PS1", "game-ps1.bin");

        var detector = new ConsoleDetector([
            new ConsoleInfo(
                Key: "PS1",
                DisplayName: "PlayStation",
                DiscBased: true,
                UniqueExts: ["iso"],
                AmbigExts: [],
                FolderAliases: ["PlayStation", "PS1"]),
            new ConsoleInfo(
                Key: "MD",
                DisplayName: "Mega Drive",
                DiscBased: false,
                UniqueExts: ["md"],
                AmbigExts: [],
                FolderAliases: ["MegaDrive", "Genesis"]),
            new ConsoleInfo(
                Key: "SNES",
                DisplayName: "SNES",
                DiscBased: false,
                UniqueExts: ["sfc"],
                AmbigExts: [],
                FolderAliases: ["SNES"])
        ]);

        var scan = new[] { new ScannedFileEntry(root, filePath, ".bin") };
        var options = new RunOptions
        {
            Roots = [root],
            Extensions = [".bin"],
            Mode = "DryRun",
            HashType = "SHA1"
        };

        var phase = new EnrichmentPipelinePhase();
        var result = phase.Execute(
            new EnrichmentPhaseInput(scan, detector, hashService, null, datIndex),
            CreateContext(options),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.True(candidate.DatMatch);
        Assert.Equal("PS1", candidate.ConsoleKey);
        Assert.Equal("Game PS1", candidate.DatGameName);
        Assert.Equal(SortDecision.Review, candidate.SortDecision);
    }

    [Fact]
    public void Execute_KnownBiosHash_OverridesCategoryToBiosWithoutDat()
    {
        var root = Path.Combine(_tempDir, "phase3_known_hash");
        Directory.CreateDirectory(root);
        var filePath = CreateFile(root, "mystery.rom", 64);

        var hashService = new FileHashService();
        var hash = hashService.GetHash(filePath, "SHA1");
        Assert.False(string.IsNullOrWhiteSpace(hash));

        var detector = new ConsoleDetector([
            new ConsoleInfo(
                Key: "SNES",
                DisplayName: "SNES",
                DiscBased: false,
                UniqueExts: ["sfc"],
                AmbigExts: [],
                FolderAliases: ["SNES"])
        ]);

        var scan = new[] { new ScannedFileEntry(root, filePath, ".rom") };
        var options = new RunOptions
        {
            Roots = [root],
            Extensions = [".rom"],
            Mode = "DryRun",
            HashType = "SHA1"
        };

        var phase = new EnrichmentPipelinePhase();
        var result = phase.Execute(
            new EnrichmentPhaseInput(
                scan,
                detector,
                hashService,
                null,
                null,
                null,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { hash! }),
            CreateContext(options),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.Equal(FileCategory.Bios, candidate.Category);
    }

    [Fact]
    public void Execute_DatHashMatch_OverridesJunkToGame()
    {
        var root = Path.Combine(_tempDir, "phase3_dat_game");
        Directory.CreateDirectory(root);
        var filePath = CreateFile(root, "sample (Demo).bin", 96);

        var hashService = new FileHashService();
        var hash = hashService.GetHash(filePath, "SHA1");
        Assert.False(string.IsNullOrWhiteSpace(hash));

        var datIndex = new DatIndex();
        datIndex.Add("PS1", hash!, "Retail Game", "sample.bin", isBios: false);

        var detector = new ConsoleDetector([
            new ConsoleInfo(
                Key: "PS1",
                DisplayName: "PlayStation",
                DiscBased: true,
                UniqueExts: ["cue"],
                AmbigExts: ["bin"],
                FolderAliases: ["PS1", "PlayStation"])
        ]);

        var scan = new[] { new ScannedFileEntry(root, filePath, ".bin") };
        var options = new RunOptions
        {
            Roots = [root],
            Extensions = [".bin"],
            Mode = "DryRun",
            HashType = "SHA1"
        };

        var phase = new EnrichmentPipelinePhase();
        var result = phase.Execute(
            new EnrichmentPhaseInput(scan, detector, hashService, null, datIndex),
            CreateContext(options),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.True(candidate.DatMatch);
        Assert.Equal(FileCategory.Game, candidate.Category);
        Assert.Equal("PS1", candidate.ConsoleKey);
        Assert.Equal(SortDecision.DatVerified, candidate.SortDecision);
    }

    [Fact]
    public void Execute_UnknownConsoleDatHashMatch_UpgradesToDatVerified()
    {
        var root = Path.Combine(_tempDir, "phase3_unknown_dat");
        Directory.CreateDirectory(root);
        var filePath = CreateFile(root, "mystery.rom", 80);

        var hashService = new FileHashService();
        var hash = hashService.GetHash(filePath, "SHA1");
        Assert.False(string.IsNullOrWhiteSpace(hash));

        var datIndex = new DatIndex();
        datIndex.Add("PS1", hash!, "Known Game", "known-game.bin", isBios: false);

        var scan = new[] { new ScannedFileEntry(root, filePath, ".rom") };
        var options = new RunOptions
        {
            Roots = [root],
            Extensions = [".rom"],
            Mode = "DryRun",
            HashType = "SHA1"
        };

        var phase = new EnrichmentPipelinePhase();
        var result = phase.Execute(
            new EnrichmentPhaseInput(scan, null, hashService, null, datIndex),
            CreateContext(options),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.True(candidate.DatMatch);
        Assert.Equal("PS1", candidate.ConsoleKey);
        Assert.Equal(FileCategory.Game, candidate.Category);
        Assert.Equal(SortDecision.DatVerified, candidate.SortDecision);
    }

    [Fact]
    public void Execute_ChdNameOnlyFallback_StripsDiscSuffix_ForTrackBasedDat()
    {
        var root = Path.Combine(_tempDir, "phase4_chd_name_only");
        var ps1Folder = Path.Combine(root, "PS1");
        Directory.CreateDirectory(ps1Folder);
        var filePath = CreateFile(ps1Folder, "Game Title (Disc 1).chd", 128);

        var hashService = new FileHashService();
        var datIndex = new DatIndex();
        datIndex.Add("PS1", "different-hash", "Game Title", "Game Title (Track 01).bin");

        var detector = new ConsoleDetector([
            new ConsoleInfo(
                Key: "PS1",
                DisplayName: "PlayStation",
                DiscBased: true,
                UniqueExts: ["cue"],
                AmbigExts: ["bin", "chd"],
                FolderAliases: ["PS1", "PlayStation"],
                Family: PlatformFamily.RedumpDisc,
                HashStrategy: "track-sha1")
        ]);

        var scan = new[] { new ScannedFileEntry(root, filePath, ".chd") };
        var options = new RunOptions
        {
            Roots = [root],
            Extensions = [".chd"],
            Mode = "DryRun",
            HashType = "SHA1"
        };

        var phase = new EnrichmentPipelinePhase();
        var result = phase.Execute(
            new EnrichmentPhaseInput(scan, detector, hashService, null, datIndex),
            CreateContext(options),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.True(candidate.DatMatch);
        Assert.Equal("PS1", candidate.ConsoleKey);
        Assert.Equal("Game Title", candidate.DatGameName);
        Assert.Equal(MatchKind.DatNameOnlyMatch, candidate.PrimaryMatchKind);
    }

    [Fact]
    public void Execute_ArcadeZipKnownConsole_NameOnlyFallback_MatchesByName()
    {
        var root = Path.Combine(_tempDir, "phase4_arcade_zip_name_only");
        var arcadeFolder = Path.Combine(root, "Arcade");
        Directory.CreateDirectory(arcadeFolder);
        var filePath = Path.Combine(arcadeFolder, "Metal Slug 5.zip");

        using (var archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("dummy.bin");
            using var stream = entry.Open();
            stream.Write([0x01, 0x02, 0x03, 0x04]);
        }

        var hashService = new FileHashService();
        var datIndex = new DatIndex();
        datIndex.Add("ARCADE", "different-hash", "Metal Slug 5", "mslug5.zip");

        var detector = new ConsoleDetector([
            new ConsoleInfo(
                Key: "ARCADE",
                DisplayName: "Arcade",
                DiscBased: false,
                UniqueExts: ["arc"],
                AmbigExts: ["zip"],
                FolderAliases: ["Arcade"],
                Family: PlatformFamily.Arcade,
                HashStrategy: "set-archive-sha1")
        ]);

        var scan = new[] { new ScannedFileEntry(root, filePath, ".zip") };
        var options = new RunOptions
        {
            Roots = [root],
            Extensions = [".zip"],
            Mode = "DryRun",
            HashType = "SHA1"
        };

        var phase = new EnrichmentPipelinePhase();
        var result = phase.Execute(
            new EnrichmentPhaseInput(scan, detector, hashService, new ArchiveHashService(), datIndex),
            CreateContext(options),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.True(candidate.DatMatch);
        Assert.Equal("ARCADE", candidate.ConsoleKey);
        Assert.Equal("Metal Slug 5", candidate.DatGameName);
        Assert.Equal(MatchKind.DatNameOnlyMatch, candidate.PrimaryMatchKind);
    }

    [Fact]
    public void Execute_CrossConsoleLookupDisabled_DoesNotSwitchConsoleOnDatHashMatch()
    {
        var root = Path.Combine(_tempDir, "phase4_cross_lookup_disabled");
        var snesFolder = Path.Combine(root, "SNES");
        Directory.CreateDirectory(snesFolder);
        var filePath = CreateFile(snesFolder, "mystery.bin", 96);

        var hashService = new FileHashService();
        var hash = hashService.GetHash(filePath, "SHA1");
        Assert.False(string.IsNullOrWhiteSpace(hash));

        var datIndex = new DatIndex();
        datIndex.Add("PS1", hash!, "Actual PS1 Game", "actual.bin", isBios: false);

        var detector = new ConsoleDetector([
            new ConsoleInfo(
                Key: "SNES",
                DisplayName: "Super Nintendo",
                DiscBased: false,
                UniqueExts: ["sfc"],
                AmbigExts: ["bin"],
                FolderAliases: ["SNES"],
                Family: PlatformFamily.NoIntroCartridge),
            new ConsoleInfo(
                Key: "PS1",
                DisplayName: "PlayStation",
                DiscBased: true,
                UniqueExts: ["cue"],
                AmbigExts: ["bin"],
                FolderAliases: ["PS1"],
                Family: PlatformFamily.RedumpDisc)
        ]);

        var phase = new EnrichmentPipelinePhase();
        var result = phase.Execute(
            new EnrichmentPhaseInput(
                [new ScannedFileEntry(root, filePath, ".bin")],
                detector,
                hashService,
                null,
                datIndex,
                FamilyDatStrategyResolver: new FixedPolicyResolver(new FamilyDatPolicy(
                    PreferArchiveInnerHash: true,
                    UseHeaderlessHash: false,
                    UseContainerHash: true,
                    AllowNameOnlyDatMatch: false,
                    RequireStrictNameForNameOnly: false,
                    EnableCrossConsoleLookup: false))),
            CreateContext(new RunOptions
            {
                Roots = [root],
                Extensions = [".bin"],
                Mode = "DryRun",
                HashType = "SHA1"
            }),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.False(candidate.DatMatch);
        Assert.Equal("SNES", candidate.ConsoleKey);
    }

    [Fact]
    public void Execute_Tier1HardEvidence_WithDatIndexForOtherConsole_RemainsSort()
    {
        var root = Path.Combine(_tempDir, "phase4_console_specific_dat_available");
        Directory.CreateDirectory(root);
        var filePath = CreateFile(root, "SLUS-00123 Game.bin", 120);

        var hashService = new FileHashService();
        var datIndex = new DatIndex();
        datIndex.Add("NES", "deadbeef", "Unrelated NES Game", "game.nes", isBios: false);

        var detector = new ConsoleDetector([]);

        var phase = new EnrichmentPipelinePhase();
        var result = phase.Execute(
            new EnrichmentPhaseInput(
                [new ScannedFileEntry(root, filePath, ".bin")],
                detector,
                hashService,
                null,
                datIndex),
            CreateContext(new RunOptions
            {
                Roots = [root],
                Extensions = [".bin"],
                Mode = "DryRun",
                HashType = "SHA1"
            }),
            CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.False(candidate.DatMatch);
        Assert.Equal("PS1", candidate.ConsoleKey);
        Assert.True(candidate.HasHardEvidence);
        Assert.Equal(SortDecision.Sort, candidate.SortDecision);
        Assert.Equal(DecisionClass.Sort, candidate.DecisionClass);
    }

    private PipelineContext CreateContext(RunOptions options)
    {
        var metrics = new PhaseMetricsCollector();
        metrics.Initialize();
        return new PipelineContext
        {
            Options = options,
            FileSystem = new FileSystemAdapter(),
            AuditStore = new AuditCsvStore(),
            Metrics = metrics
        };
    }

    private static string CreateFile(string root, string fileName, int sizeBytes)
    {
        var path = Path.Combine(root, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Enumerable.Range(1, sizeBytes).Select(static i => (byte)(i % 251)).ToArray());
        return path;
    }

    private sealed class FixedPolicyResolver(FamilyDatPolicy policy) : IFamilyDatStrategyResolver
    {
        public FamilyDatPolicy ResolvePolicy(PlatformFamily family, string extension, string? hashStrategy)
            => policy;
    }
}
