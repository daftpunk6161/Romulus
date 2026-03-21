using System.Text.Json;
using System.Text.Json.Serialization;
using RomCleanup.Tests.Benchmark.Models;

namespace RomCleanup.Tests.Benchmark.Infrastructure;

/// <summary>
/// Programmatic expansion of ground-truth JSONL datasets.
/// Generates entries for all 69 systems across all 20 Fallklassen to meet gate thresholds.
/// </summary>
internal sealed class DatasetExpander
{
    private readonly HashSet<string> _existingIds;
    private readonly Dictionary<string, int> _idCounters = new(StringComparer.Ordinal);

    private record SystemDef(
        string Key, bool DiscBased, string[] UniqueExts, string[] AmbigExts,
        string FolderAlias, string PrimaryDetection, string DatEcosystem,
        bool HasCartridgeHeader, string[] SampleGames, long TypicalSize);

    private static readonly SystemDef[] Systems = BuildSystemCatalog();

    public DatasetExpander(IEnumerable<GroundTruthEntry> existingEntries)
    {
        _existingIds = new HashSet<string>(
            existingEntries.Select(e => e.Id), StringComparer.Ordinal);
    }

    /// <summary>
    /// Generates all expanded entries grouped by target JSONL filename.
    /// Does NOT include existing entries — caller merges.
    /// </summary>
    public Dictionary<string, List<GroundTruthEntry>> GenerateExpansion()
    {
        var result = new Dictionary<string, List<GroundTruthEntry>>();

        GenerateCleanReferences(result);       // FC-01 → golden-core.jsonl
        GenerateWrongNameEntries(result);       // FC-02 → chaos-mixed.jsonl
        GenerateHeaderConflicts(result);        // FC-03 → chaos-mixed.jsonl
        GenerateWrongExtensions(result);        // FC-04 → edge-cases.jsonl
        GenerateFolderConflicts(result);        // FC-05 → edge-cases.jsonl
        GenerateDatExactEntries(result);        // FC-06 → dat-coverage.jsonl
        GenerateDatWeakEntries(result);         // FC-07 → dat-coverage.jsonl
        GenerateBiosEntries(result);            // FC-08 → golden-core.jsonl
        GenerateArcadeParentClone(result);      // FC-09 → golden-core.jsonl
        GenerateMultiDiscEntries(result);       // FC-10 → golden-realworld.jsonl
        GenerateMultiFileEntries(result);       // FC-11 → golden-realworld.jsonl
        GenerateArchiveInnerEntries(result);    // FC-12 → golden-realworld.jsonl
        GenerateDirectoryEntries(result);       // FC-13 → golden-realworld.jsonl
        GenerateUnknownExpected(result);        // FC-14 → negative-controls.jsonl
        GenerateAmbiguousEntries(result);       // FC-15 → edge-cases.jsonl
        GenerateNegativeControls(result);       // FC-16 → negative-controls.jsonl
        GenerateRepairBlocked(result);          // FC-17 → repair-safety.jsonl
        GenerateCrossSystem(result);            // FC-18 → edge-cases.jsonl
        GenerateJunkEntries(result);            // FC-19 → golden-realworld.jsonl
        GenerateBrokenEntries(result);          // FC-20 → chaos-mixed.jsonl
        GeneratePsDisambiguation(result);       // special: psDisambiguation
        GenerateHeaderlessEntries(result);      // special: headerless
        GenerateChdRawSha1Entries(result);      // special: chdRawSha1
        GenerateExtraArcadeEntries(result);     // boost: arcade family coverage
        GenerateExtraComputerEntries(result);   // boost: computer family coverage

        return result;
    }

    // ═══ GENERATION METHODS ═════════════════════════════════════════════

    private void GenerateCleanReferences(Dictionary<string, List<GroundTruthEntry>> result)
    {
        foreach (var sys in Systems)
        {
            int count = GetTierCount(sys.Key, 5, 3, 2, 2);
            for (int i = 0; i < count; i++)
            {
                var ext = GetPrimaryExtension(sys);
                var id = NextId("gc", sys.Key, "ref");
                if (_existingIds.Contains(id)) continue;

                var gameName = sys.SampleGames[i % sys.SampleGames.Length];
                Add(result, "golden-core.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"{gameName} (USA){ext}",
                        Extension = ext,
                        SizeBytes = sys.TypicalSize + (i * 1024),
                        Directory = sys.FolderAlias,
                    },
                    Tags = BuildTags("clean-reference", sys),
                    Difficulty = "easy",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = sys.Key,
                        Category = "Game",
                        Confidence = 95,
                        HasConflict = false,
                        DatMatchLevel = "exact",
                        DatEcosystem = sys.DatEcosystem,
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = sys.PrimaryDetection,
                        AcceptableAlternatives = GetAlternatives(sys)
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    private void GenerateWrongNameEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        foreach (var sys in Systems.Where(s => s.UniqueExts.Length > 0))
        {
            var ext = sys.UniqueExts[0];
            var id = NextId("cm", sys.Key, "wrongname");
            if (_existingIds.Contains(id)) continue;

            Add(result, "chaos-mixed.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"unknown_game_2024{ext}",
                    Extension = ext,
                    SizeBytes = sys.TypicalSize,
                    Directory = sys.FolderAlias,
                },
                Tags = ["wrong-name", ..BuildTags(null, sys)],
                Difficulty = "medium",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sys.Key,
                    Category = "Game",
                    Confidence = 80,
                    HasConflict = false,
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = sys.PrimaryDetection,
                    AcceptableAlternatives = GetAlternatives(sys)
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateHeaderConflicts(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // Systems with cartridge headers where header could conflict with filename
        var headerSystems = Systems.Where(s => s.HasCartridgeHeader).ToArray();
        foreach (var sys in headerSystems)
        {
            // First variant: wrong folder placement
            var ext = GetPrimaryExtension(sys);
            var id = NextId("cm", sys.Key, "hdrconf");
            if (!_existingIds.Contains(id))
            {
                Add(result, "chaos-mixed.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"Wrong System Game{ext}",
                        Extension = ext,
                        SizeBytes = sys.TypicalSize,
                        Directory = "roms",
                    },
                    Tags = ["header-conflict", "wrong-name"],
                    Difficulty = "hard",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = sys.Key,
                        Category = "Game",
                        Confidence = 75,
                        HasConflict = true,
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = "CartridgeHeader",
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }

            // Second variant: misplaced in wrong console folder
            var id2 = NextId("cm", sys.Key, "hdrconf");
            if (!_existingIds.Contains(id2))
            {
                var wrongFolder = sys.Key == "NES" ? "snes" : "nes";
                Add(result, "chaos-mixed.jsonl", new GroundTruthEntry
                {
                    Id = id2,
                    Source = new SourceInfo
                    {
                        FileName = $"{sys.SampleGames[0]}{ext}",
                        Extension = ext,
                        SizeBytes = sys.TypicalSize,
                        Directory = wrongFolder,
                    },
                    Tags = ["header-conflict", "folder-header-conflict"],
                    Difficulty = "hard",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = sys.Key,
                        Category = "Game",
                        Confidence = 70,
                        HasConflict = true,
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = "CartridgeHeader",
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    private void GenerateWrongExtensions(Dictionary<string, List<GroundTruthEntry>> result)
    {
        foreach (var sys in Systems.Where(s => s.UniqueExts.Length > 0))
        {
            var id = NextId("ec", sys.Key, "wrongext");
            if (_existingIds.Contains(id)) continue;

            Add(result, "edge-cases.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{sys.SampleGames[0]} (USA).bin",
                    Extension = ".bin",
                    SizeBytes = sys.TypicalSize,
                    Directory = sys.FolderAlias,
                },
                Tags = ["wrong-extension"],
                Difficulty = "medium",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sys.Key,
                    Category = "Game",
                    Confidence = 70,
                    HasConflict = false,
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = sys.HasCartridgeHeader ? "CartridgeHeader" : "FolderName",
                    AcceptableAlternatives = sys.HasCartridgeHeader ? ["FolderName"] : []
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateFolderConflicts(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // File in WRONG folder, but with correct extension/header
        foreach (var sys in Systems.Where(s => s.UniqueExts.Length > 0).Take(25))
        {
            var ext = sys.UniqueExts[0];
            var id = NextId("ec", sys.Key, "foldconf");
            if (_existingIds.Contains(id)) continue;

            Add(result, "edge-cases.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{sys.SampleGames[0]} (USA){ext}",
                    Extension = ext,
                    SizeBytes = sys.TypicalSize,
                    Directory = "wrong_folder",
                },
                Tags = ["folder-header-conflict"],
                Difficulty = "hard",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sys.Key,
                    Category = "Game",
                    Confidence = 75,
                    HasConflict = true,
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = sys.HasCartridgeHeader ? "CartridgeHeader" : "UniqueExtension",
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateDatExactEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        foreach (var sys in Systems)
        {
            int count = GetTierCount(sys.Key, 3, 2, 1, 1);
            for (int i = 0; i < count; i++)
            {
                var ext = GetPrimaryExtension(sys);
                var id = NextId("dc", sys.Key, "exact");
                if (_existingIds.Contains(id)) continue;

                var gameName = sys.SampleGames[i % sys.SampleGames.Length];
                Add(result, "dat-coverage.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"{gameName} (USA){ext}",
                        Extension = ext,
                        SizeBytes = sys.TypicalSize,
                        Directory = sys.FolderAlias,
                    },
                    Tags = ["dat-exact-match", sys.DatEcosystem],
                    Difficulty = "easy",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = sys.Key,
                        Category = "Game",
                        Confidence = 98,
                        HasConflict = false,
                        DatMatchLevel = "exact",
                        DatEcosystem = sys.DatEcosystem,
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = "DatMatch",
                        AcceptableAlternatives = [sys.PrimaryDetection]
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    private void GenerateDatWeakEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        foreach (var sys in Systems)
        {
            var ext = GetPrimaryExtension(sys);
            var id = NextId("dc", sys.Key, "weak");
            if (_existingIds.Contains(id)) continue;

            Add(result, "dat-coverage.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"homebrew_game (PD){ext}",
                    Extension = ext,
                    SizeBytes = sys.TypicalSize / 4,
                    Directory = sys.FolderAlias,
                },
                Tags = ["dat-none"],
                Difficulty = "medium",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sys.Key,
                    Category = "Game",
                    Confidence = 60,
                    HasConflict = false,
                    DatMatchLevel = "none",
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = sys.PrimaryDetection,
                    AcceptableAlternatives = GetAlternatives(sys)
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateBiosEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // Systems that commonly have BIOS files
        string[] biosSystems = [
            "PS1", "PS2", "PS3", "SAT", "DC", "GBA", "NDS", "3DS",
            "NEOCD", "PCECD", "PCFX", "SCD", "CD32", "CDI", "JAGCD",
            "3DO", "GC", "WII", "WIIU", "FMTOWNS"
        ];

        foreach (var key in biosSystems)
        {
            var sys = Systems.FirstOrDefault(s => s.Key == key);
            if (sys is null) continue;

            var ext = GetPrimaryExtension(sys);
            var id = NextId("gc", key, "bios");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-core.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"[BIOS] {sys.Key} ({(sys.DiscBased ? "CD" : "System")}).bin",
                    Extension = ".bin",
                    SizeBytes = 524288,
                    Directory = sys.FolderAlias,
                },
                Tags = ["bios", "clean-reference", sys.DatEcosystem],
                Difficulty = "easy",
                Expected = new ExpectedResult
                {
                    ConsoleKey = key,
                    Category = "Bios",
                    Confidence = 95,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = sys.DatEcosystem,
                    SortDecision = "block"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "FolderName",
                    AcceptableAlternatives = ["DatLookup"]
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo
                {
                    BiosSystemKeys = [key]
                }
            });
        }

        // Regional BIOS variants for major systems
        foreach (var key in new[] { "PS1", "PS2", "SAT", "DC", "3DO", "GC", "WII", "NDS", "3DS", "PCECD", "PCFX", "SCD", "NEOCD", "CD32", "FMTOWNS" })
        {
            var sys2 = Systems.FirstOrDefault(s => s.Key == key);
            if (sys2 is null) continue;

            var id2 = NextId("gc", key, "biosrgn");
            if (_existingIds.Contains(id2)) continue;

            Add(result, "golden-core.jsonl", new GroundTruthEntry
            {
                Id = id2,
                Source = new SourceInfo
                {
                    FileName = $"[BIOS] {sys2.Key} (Japan).bin",
                    Extension = ".bin",
                    SizeBytes = 524288,
                    Directory = sys2.FolderAlias,
                },
                Tags = ["bios", "clean-reference", sys2.DatEcosystem],
                Difficulty = "easy",
                Expected = new ExpectedResult
                {
                    ConsoleKey = key,
                    Category = "Bios",
                    Confidence = 95,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = sys2.DatEcosystem,
                    SortDecision = "block"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "FolderName",
                    AcceptableAlternatives = ["DatLookup"]
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo
                {
                    BiosSystemKeys = [key]
                }
            });
        }

        // Arcade BIOS entries
        foreach (var biosName in new[] { "pgm", "cps2", "cps3", "decocass", "isgsm", "skns", "stvbios" })
        {
            var id = NextId("gc", "ARCADE", $"bios{biosName}");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-core.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{biosName}.zip",
                    Extension = ".zip",
                    SizeBytes = 131072,
                    Directory = "arcade",
                },
                Tags = ["bios", "arcade-bios", "dat-mame"],
                Difficulty = "easy",
                Expected = new ExpectedResult
                {
                    ConsoleKey = "ARCADE",
                    Category = "Bios",
                    Confidence = 92,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = "mame",
                    SortDecision = "block"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "FolderName",
                    AcceptableAlternatives = ["DatLookup"]
                },
                FileModel = new FileModelInfo { Type = "archive" },
                Relationships = new RelationshipInfo
                {
                    BiosSystemKeys = ["ARCADE"]
                }
            });
        }
    }

    private void GenerateArcadeParentClone(Dictionary<string, List<GroundTruthEntry>> result)
    {
        var arcadeGames = new[]
        {
            ("pacman", "Pac-Man", 49152L),
            ("dkong", "Donkey Kong", 32768L),
            ("galaga", "Galaga", 32768L),
            ("1942", "1942", 65536L),
            ("bublbobl", "Bubble Bobble", 131072L),
            ("tmnt", "TMNT", 524288L),
            ("xmen", "X-Men", 2097152L),
            ("simpsons", "The Simpsons", 2097152L),
            ("mslug2", "Metal Slug 2", 33554432L),
            ("garou", "Garou MOTW", 67108864L),
            ("sf2", "Street Fighter II", 3145728L),
            ("mslug", "Metal Slug", 16777216L),
            ("ddonpach", "DoDonPachi", 8388608L),
            ("mvsc", "Marvel vs Capcom", 33554432L),
            ("kof2002", "KOF 2002", 67108864L),
            ("twinbee", "TwinBee", 131072L),
            ("gradius", "Gradius", 131072L),
            ("outrun", "Out Run", 262144L),
            ("parodius", "Parodius", 524288L),
            ("raiden", "Raiden", 1048576L),
            ("darius", "Darius", 2097152L),
            ("turtles", "Turtles in Time", 2097152L),
            ("punkshot", "Punk Shot", 1048576L),
            ("ssf2t", "Super SF2 Turbo", 4194304L),
            ("msh", "Marvel Super Heroes", 33554432L),
        };

        // Parent sets
        foreach (var (rom, name, size) in arcadeGames)
        {
            var id = NextId("gc", "ARCADE", "parent");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-core.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{rom}.zip",
                    Extension = ".zip",
                    SizeBytes = size,
                    Directory = "arcade",
                },
                Tags = ["clean-reference", "parent", "dat-mame"],
                Difficulty = "easy",
                Expected = new ExpectedResult
                {
                    ConsoleKey = "ARCADE",
                    Category = "Game",
                    Confidence = 90,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = "mame",
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "FolderName",
                    AcceptableAlternatives = ["ArchiveContent", "DatLookup"]
                },
                FileModel = new FileModelInfo { Type = "archive" },
                Relationships = new RelationshipInfo()
            });
        }

        // Clone sets
        var clones = new[] {
            ("sf2ce", "sf2"), ("sf2hf", "sf2"), ("mslug2t", "mslug2"),
            ("pacmanf", "pacman"), ("dkongj", "dkong"),
            ("galagao", "galaga"), ("1942a", "1942"), ("1942b", "1942"),
            ("bublbob1", "bublbobl"), ("tmnt2", "tmnt"),
            ("xmen2p", "xmen"), ("simpsonj", "simpsons"),
            ("garoubl", "garou"), ("mslug2x", "mslug2"),
            ("mslugx", "mslug"), ("ddonpchj", "ddonpach"),
            ("mvscj", "mvsc"), ("kof2002m", "kof2002"),
            ("gradius2", "gradius"), ("outrundx", "outrun"),
            ("raiden2", "raiden"), ("dariusg", "darius"),
            ("mshj", "msh"), ("ssf2ta", "ssf2t"),
            ("sf2rb", "sf2"),
        };

        foreach (var (clone, parent) in clones)
        {
            var id = NextId("gc", "ARCADE", "clone");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-core.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{clone}.zip",
                    Extension = ".zip",
                    SizeBytes = 65536,
                    Directory = "arcade",
                },
                Tags = ["clone", "dat-mame"],
                Difficulty = "easy",
                Expected = new ExpectedResult
                {
                    ConsoleKey = "ARCADE",
                    Category = "Game",
                    Confidence = 88,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = "mame",
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "FolderName",
                    AcceptableAlternatives = ["DatLookup"]
                },
                FileModel = new FileModelInfo { Type = "archive" },
                Relationships = new RelationshipInfo { CloneOf = parent }
            });
        }

        // Neo Geo parent sets
        var neogeoGames = new[] {
            ("kof97", 20971520L), ("kof99", 33554432L), ("samsho2", 16777216L),
            ("mslug3", 67108864L), ("mslug4", 67108864L), ("mslug5", 67108864L),
            ("fatfury2", 8388608L), ("rbff1", 16777216L), ("aof3", 33554432L),
            ("matrim", 67108864L), ("lastblad", 33554432L), ("blazstar", 16777216L),
        };

        foreach (var (rom, size) in neogeoGames)
        {
            var id = NextId("gc", "NEOGEO", "parent");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-core.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{rom}.zip",
                    Extension = ".zip",
                    SizeBytes = size,
                    Directory = "neogeo",
                },
                Tags = ["clean-reference", "parent", "dat-mame"],
                Difficulty = "easy",
                Expected = new ExpectedResult
                {
                    ConsoleKey = "NEOGEO",
                    Category = "Game",
                    Confidence = 90,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = "mame",
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "FolderName",
                    AcceptableAlternatives = ["ArchiveContent", "DatLookup"],
                    AcceptableConsoleKeys = ["ARCADE"]
                },
                FileModel = new FileModelInfo { Type = "archive" },
                Relationships = new RelationshipInfo()
            });
        }

        // Neo Geo clone sets
        var neogeoClones = new[]
        {
            ("kof97a", "kof97"), ("kof99e", "kof99"), ("samsho2k", "samsho2"),
            ("mslug3h", "mslug3"), ("mslug4h", "mslug4"), ("mslug5h", "mslug5"),
            ("fatfury2a", "fatfury2"), ("rbff1a", "rbff1"), ("aof3k", "aof3"),
            ("matrimbl", "matrim"),
        };

        foreach (var (clone, parent) in neogeoClones)
        {
            var id = NextId("gc", "NEOGEO", "clone");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-core.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{clone}.zip",
                    Extension = ".zip",
                    SizeBytes = 33554432,
                    Directory = "neogeo",
                },
                Tags = ["clone", "dat-mame"],
                Difficulty = "easy",
                Expected = new ExpectedResult
                {
                    ConsoleKey = "NEOGEO",
                    Category = "Game",
                    Confidence = 88,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = "mame",
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "FolderName",
                    AcceptableAlternatives = ["DatLookup"],
                    AcceptableConsoleKeys = ["ARCADE"]
                },
                FileModel = new FileModelInfo { Type = "archive" },
                Relationships = new RelationshipInfo { CloneOf = parent }
            });
        }

        // Arcade split/merged/nonmerged variants
        foreach (var variant in new[] { "arcade-split", "arcade-merged", "arcade-non-merged" })
        {
            for (int i = 0; i < 15; i++)
            {
                var id = NextId("gc", "ARCADE", variant.Replace("arcade-", ""));
                if (_existingIds.Contains(id)) continue;

                Add(result, "golden-core.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"game_{variant}_{i:D2}.zip",
                        Extension = ".zip",
                        SizeBytes = 1048576 * (i + 1),
                        Directory = "arcade",
                    },
                    Tags = ["parent", variant, "dat-mame"],
                    Difficulty = "medium",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = "ARCADE",
                        Category = "Game",
                        Confidence = 88,
                        HasConflict = false,
                        DatMatchLevel = "exact",
                        DatEcosystem = "mame",
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = "FolderName",
                        AcceptableAlternatives = ["DatLookup"]
                    },
                    FileModel = new FileModelInfo { Type = "archive" },
                    Relationships = new RelationshipInfo()
                });
            }
        }

        // Arcade CHD supplement
        foreach (var chdGame in new[] { "area51", "kinst", "kinst2", "sfiii3", "mvsc2", "capvssnk", "sfex", "tekken3" })
        {
            var id = NextId("gc", "ARCADE", "chd");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-core.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{chdGame}.chd",
                    Extension = ".chd",
                    SizeBytes = 734003200,
                    Directory = $"arcade/{chdGame}",
                },
                Tags = ["arcade-chd", "dat-mame"],
                Difficulty = "medium",
                Expected = new ExpectedResult
                {
                    ConsoleKey = "ARCADE",
                    Category = "Game",
                    Confidence = 88,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = "mame",
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "FolderName",
                    AcceptableAlternatives = ["DatLookup"]
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateMultiDiscEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        var discSystems = Systems.Where(s => s.DiscBased).ToArray();
        foreach (var sys in discSystems)
        {
            var id = NextId("gr", sys.Key, "mdisc");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-realworld.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{sys.SampleGames[0]} (USA) (Disc 1).chd",
                    Extension = ".chd",
                    SizeBytes = 734003200,
                    Directory = sys.FolderAlias,
                    InnerFiles = null,
                },
                Tags = ["multi-disc", "clean-reference", sys.DatEcosystem],
                Difficulty = "medium",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sys.Key,
                    Category = "Game",
                    Confidence = 92,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = sys.DatEcosystem,
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = sys.PrimaryDetection,
                    AcceptableAlternatives = ["FolderName"]
                },
                FileModel = new FileModelInfo { Type = "multi-disc", DiscCount = 2 },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateMultiFileEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        var discSystems = Systems.Where(s => s.DiscBased).ToArray();
        foreach (var sys in discSystems)
        {
            // CUE+BIN variant
            var id = NextId("gr", sys.Key, "mfile");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-realworld.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{sys.SampleGames[0]} (USA).cue",
                    Extension = ".cue",
                    SizeBytes = 256,
                    Directory = sys.FolderAlias,
                    InnerFiles =
                    [
                        new InnerFileInfo { Name = $"{sys.SampleGames[0]} (USA) (Track 1).bin", SizeBytes = 734003200 },
                        new InnerFileInfo { Name = $"{sys.SampleGames[0]} (USA) (Track 2).bin", SizeBytes = 10240000 },
                    ]
                },
                Tags = ["multi-file", "clean-reference"],
                Difficulty = "medium",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sys.Key,
                    Category = "Game",
                    Confidence = 92,
                    HasConflict = false,
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "FolderName",
                },
                FileModel = new FileModelInfo
                {
                    Type = "multi-file-set",
                    SetFiles = [$"{sys.SampleGames[0]} (USA).cue", $"{sys.SampleGames[0]} (USA) (Track 1).bin"]
                },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateArchiveInnerEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        foreach (var sys in Systems.Where(s => !s.DiscBased && s.UniqueExts.Length > 0).Take(20))
        {
            var ext = sys.UniqueExts[0];
            var id = NextId("gr", sys.Key, "arcinner");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-realworld.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{sys.SampleGames[0]} (USA).zip",
                    Extension = ".zip",
                    SizeBytes = sys.TypicalSize / 2,
                    Directory = sys.FolderAlias,
                    InnerFiles =
                    [
                        new InnerFileInfo { Name = $"{sys.SampleGames[0]} (USA){ext}", SizeBytes = sys.TypicalSize }
                    ]
                },
                Tags = ["archive-inner", "clean-reference"],
                Difficulty = "easy",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sys.Key,
                    Category = "Game",
                    Confidence = 95,
                    HasConflict = false,
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "ArchiveContent",
                    AcceptableAlternatives = ["FolderName"]
                },
                FileModel = new FileModelInfo { Type = "archive" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateDirectoryEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        var dirSystems = new[] { "WIIU", "3DS", "DOS", "SWITCH", "VITA" };
        foreach (var key in dirSystems)
        {
            var sys = Systems.FirstOrDefault(s => s.Key == key);
            if (sys is null) continue;

            for (int i = 0; i < 3; i++)
            {
                var id = NextId("gr", key, "dir");
                if (_existingIds.Contains(id)) continue;

                Add(result, "golden-realworld.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"game_{key.ToLowerInvariant()}_{i:D2}",
                        Extension = key switch
                        {
                            "WIIU" => ".rpx",
                            "3DS" => ".3ds",
                            "SWITCH" => ".nsp",
                            "VITA" => ".vpk",
                            _ => ".exe"
                        },
                        SizeBytes = 0,
                        Directory = $"{sys.FolderAlias}/game_{i:D2}",
                    },
                    Tags = ["directory-based", "clean-reference"],
                    Difficulty = "medium",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = key,
                        Category = "Game",
                        Confidence = 80,
                        HasConflict = false,
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = "FolderName",
                    },
                    FileModel = new FileModelInfo { Type = "directory" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    private void GenerateUnknownExpected(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // Files that look like ROMs but can't be identified
        var unknownExts = new[] { ".rom", ".dat", ".img", ".raw", ".dmp", ".dump",
            ".unknown", ".bak", ".old", ".tmp", ".bin", ".data" };

        for (int i = 0; i < unknownExts.Length; i++)
        {
            var id = NextId("nc", "UNK", $"exp{i:D2}");
            if (_existingIds.Contains(id)) continue;

            Add(result, "negative-controls.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"mystery_file_{i:D3}{unknownExts[i]}",
                    Extension = unknownExts[i],
                    SizeBytes = 65536 * (i + 1),
                    Directory = "unsorted",
                },
                Tags = ["expected-unknown"],
                Difficulty = "medium",
                Expected = new ExpectedResult
                {
                    ConsoleKey = null,
                    Category = "Unknown",
                    Confidence = 0,
                    HasConflict = false,
                    SortDecision = "skip"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "Heuristic",
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }

        // ROM-like files in non-system folders
        foreach (var sys in Systems.Where(s => s.UniqueExts.Length > 0).Take(18))
        {
            var ext = sys.UniqueExts[0];
            var id = NextId("nc", sys.Key, "expunk");
            if (_existingIds.Contains(id)) continue;

            Add(result, "negative-controls.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"empty_file{ext}",
                    Extension = ext,
                    SizeBytes = 0,
                },
                Tags = ["expected-unknown"],
                Difficulty = "hard",
                Expected = new ExpectedResult
                {
                    ConsoleKey = null,
                    Category = "Unknown",
                    Confidence = 0,
                    HasConflict = false,
                    SortDecision = "skip"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "Heuristic",
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo(),
                Notes = "Empty file with valid ROM extension — should be rejected"
            });
        }
    }

    private void GenerateAmbiguousEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // Disc systems that share .iso/.bin/.chd extensions
        var discSystems = Systems.Where(s => s.DiscBased && s.AmbigExts.Length > 0).ToArray();
        foreach (var sys in discSystems)
        {
            var id = NextId("ec", sys.Key, "ambig");
            if (_existingIds.Contains(id)) continue;

            Add(result, "edge-cases.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"Game (USA).iso",
                    Extension = ".iso",
                    SizeBytes = 734003200,
                    Directory = "roms", // Neutral folder
                },
                Tags = ["ambiguous"],
                Difficulty = "hard",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sys.Key,
                    Category = "Game",
                    Confidence = 50,
                    HasConflict = true,
                    SortDecision = "block"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "DiscHeader",
                    AcceptableAlternatives = ["Heuristic"]
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateNegativeControls(Dictionary<string, List<GroundTruthEntry>> result)
    {
        var negatives = new[]
        {
            (".doc",   "document.doc",      102400L,   "D0CF11E0"),
            (".mp3",   "music.mp3",         5242880L,  "494433"),
            (".avi",   "video.avi",          10485760L, "52494646"),
            (".png",   "image.png",         65536L,    "89504E47"),
            (".gif",   "animation.gif",     32768L,    "47494638"),
            (".bmp",   "picture.bmp",       786432L,   "424D"),
            (".html",  "readme.html",       8192L,     null as string),
            (".xml",   "config.xml",        4096L,     null),
            (".csv",   "database.csv",      16384L,    null),
            (".log",   "debug.log",         32768L,    null),
            (".ini",   "settings.ini",      1024L,     null),
            (".bat",   "autorun.bat",       512L,      null),
            (".ps1",   "script.ps1",        2048L,     null),
            (".json",  "metadata.json",     4096L,     null),
            (".nfo",   "release.nfo",       8192L,     null),
            (".sfv",   "verify.sfv",        1024L,     null),
            (".torrent","download.torrent",  16384L,    null),
            (".url",   "website.url",        256L,      null),
            (".lnk",   "shortcut.lnk",      1024L,     null),
            (".dll",   "library.dll",        65536L,    "4D5A"),
            (".sys",   "driver.sys",         32768L,    "4D5A"),
            (".msi",   "installer.msi",      2097152L,  "D0CF11E0"),
            (".dmg",   "macos.dmg",          4194304L,  null),
            (".apk",   "android.apk",        8388608L,  "504B0304"),
            (".ipa",   "ios.ipa",            16777216L, "504B0304"),
            (".pdf",   "manual.pdf",         1048576L,  "25504446"),
            (".xlsx",  "spreadsheet.xlsx",   524288L,   "504B0304"),
            (".pptx",  "slides.pptx",        2097152L,  "504B0304"),
            (".ogg",   "soundtrack.ogg",     3145728L,  "4F676753"),
            (".flac",  "lossless.flac",      10485760L, "664C6143"),
            (".wav",   "sample.wav",         4194304L,  "52494646"),
            (".ttf",   "font.ttf",           131072L,   "00010000"),
        };

        for (int i = 0; i < negatives.Length; i++)
        {
            var (ext, name, size, _) = negatives[i];
            var id = NextId("nc", "NONE", $"neg{i:D2}");
            if (_existingIds.Contains(id)) continue;

            Add(result, "negative-controls.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = name,
                    Extension = ext,
                    SizeBytes = size,
                },
                Tags = ["negative-control"],
                Difficulty = "easy",
                Expected = new ExpectedResult
                {
                    ConsoleKey = null,
                    Category = "Unknown",
                    Confidence = 0,
                    HasConflict = false,
                    SortDecision = "skip"
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateRepairBlocked(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // For common systems, generate a sort-blocked entry
        foreach (var sys in Systems)
        {
            var ext = GetPrimaryExtension(sys);

            // Low confidence → block
            var id = NextId("rs", sys.Key, "lowconf");
            if (!_existingIds.Contains(id))
            {
                Add(result, "repair-safety.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"maybe_{sys.Key.ToLowerInvariant()}_game{ext}",
                        Extension = ext,
                        SizeBytes = sys.TypicalSize / 2,
                        Directory = "unsorted",
                    },
                    Tags = ["sort-blocked", "repair-safety"],
                    Difficulty = "medium",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = sys.Key,
                        Category = "Game",
                        Confidence = 40,
                        HasConflict = false,
                        SortDecision = "block"
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo(),
                    Notes = "Low confidence — system should block automatic sorting"
                });
            }
        }
    }

    private void GenerateCrossSystem(Dictionary<string, List<GroundTruthEntry>> result)
    {
        var pairs = new[]
        {
            ("PS1", "PS2", "disc"), ("PS2", "PS3", "disc"),
            ("GB", "GBC", "cart"), ("MD", "32X", "cart"),
            ("ARCADE", "NEOGEO", "arcade"), ("NEOCD", "NEOGEO", "disc-arcade"),
            ("PCE", "PCECD", "cart-disc"), ("GC", "WII", "disc"),
            ("NDS", "3DS", "cart"), ("SMS", "GG", "cart"),
            ("SG1000", "SMS", "cart"), ("GB", "GBA", "cart"),
            ("WII", "WIIU", "disc"),
        };

        foreach (var (sysA, sysB, type) in pairs)
        {
            var defA = Systems.First(s => s.Key == sysA);

            // File that is system A but could be confused for system B
            var id = NextId("ec", sysA, $"xs{sysB.ToLowerInvariant()}");
            if (_existingIds.Contains(id)) continue;

            var ext = GetPrimaryExtension(defA);
            Add(result, "edge-cases.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"CrossTest ({sysA} vs {sysB}){ext}",
                    Extension = ext,
                    SizeBytes = defA.TypicalSize,
                    Directory = "roms",
                },
                Tags = ["cross-system"],
                Difficulty = "hard",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sysA,
                    Category = "Game",
                    Confidence = 75,
                    HasConflict = true,
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = defA.PrimaryDetection,
                    AcceptableConsoleKeys = [sysB]
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });

            // Reverse direction
            var defB = Systems.First(s => s.Key == sysB);
            var id2 = NextId("ec", sysB, $"xs{sysA.ToLowerInvariant()}");
            if (_existingIds.Contains(id2)) continue;

            var ext2 = GetPrimaryExtension(defB);
            Add(result, "edge-cases.jsonl", new GroundTruthEntry
            {
                Id = id2,
                Source = new SourceInfo
                {
                    FileName = $"CrossTest ({sysB} vs {sysA}){ext2}",
                    Extension = ext2,
                    SizeBytes = defB.TypicalSize,
                    Directory = "roms",
                },
                Tags = ["cross-system"],
                Difficulty = "hard",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sysB,
                    Category = "Game",
                    Confidence = 75,
                    HasConflict = true,
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = defB.PrimaryDetection,
                    AcceptableConsoleKeys = [sysA]
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateJunkEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        var junkTags = new[] {
            ("Demo", "demo"), ("Beta", "beta"), ("Proto", "prototype"),
            ("Sample", "sample"), ("Hack", "hack"),
        };

        foreach (var sys in Systems)
        {
            var ext = GetPrimaryExtension(sys);

            foreach (var (label, tag) in junkTags.Take(GetTierCount(sys.Key, 5, 2, 1, 1)))
            {
                var id = NextId("gr", sys.Key, $"junk{tag}");
                if (_existingIds.Contains(id)) continue;

                Add(result, "golden-realworld.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"Game ({label}) (USA){ext}",
                        Extension = ext,
                        SizeBytes = sys.TypicalSize / 4,
                        Directory = sys.FolderAlias,
                    },
                    Tags = [tag == "hack" ? "non-game" : "junk"],
                    Difficulty = "easy",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = sys.Key,
                        Category = tag == "hack" ? "NonGame" : "Junk",
                        Confidence = 90,
                        HasConflict = false,
                        SortDecision = "sort"
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    private void GenerateBrokenEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        foreach (var sys in Systems.Where(s => s.UniqueExts.Length > 0).Take(20))
        {
            var ext = sys.UniqueExts[0];

            // Truncated
            var id = NextId("cm", sys.Key, "trunc");
            if (!_existingIds.Contains(id))
            {
                Add(result, "chaos-mixed.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"broken_rom{ext}",
                        Extension = ext,
                        SizeBytes = 16,
                    },
                    Tags = ["truncated"],
                    Difficulty = "adversarial",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = null,
                        Category = "Unknown",
                        Confidence = 0,
                        HasConflict = false,
                        SortDecision = "skip"
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }

            // Corrupt (valid header but garbage body)
            var id2 = NextId("cm", sys.Key, "corrupt");
            if (!_existingIds.Contains(id2))
            {
                Add(result, "chaos-mixed.jsonl", new GroundTruthEntry
                {
                    Id = id2,
                    Source = new SourceInfo
                    {
                        FileName = $"corrupt_game{ext}",
                        Extension = ext,
                        SizeBytes = sys.TypicalSize,
                    },
                    Tags = ["corrupt"],
                    Difficulty = "adversarial",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = sys.Key,
                        Category = "Game",
                        Confidence = 50,
                        HasConflict = false,
                        SortDecision = "block"
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    private void GeneratePsDisambiguation(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // PS1 ↔ PS2 ↔ PS3 disambiguation entries
        string[] psSystems = ["PS1", "PS2", "PS3"];
        var psGames = new Dictionary<string, string[]>
        {
            ["PS1"] = ["Crash Bandicoot", "Spyro", "Tekken 3", "Resident Evil", "Tomb Raider",
                        "Silent Hill", "Metal Gear Solid", "Parasite Eve", "Vagrant Story", "Dino Crisis"],
            ["PS2"] = ["God of War", "Shadow of Colossus", "Okami", "Persona 4", "DMC 3",
                        "Ratchet Clank", "Jak and Daxter", "Ico", "Gran Turismo 4", "Metal Gear 3"],
            ["PS3"] = ["Uncharted 2", "Last of Us", "Demon Souls", "LBP", "MGS4",
                        "Infamous", "Heavy Rain", "GT5", "Killzone 2", "Resistance"],
        };

        foreach (var sys in psSystems)
        {
            var games = psGames[sys];
            for (int i = 0; i < games.Length; i++)
            {
                var id = NextId("ec", sys, "psdis");
                if (_existingIds.Contains(id)) continue;

                var otherSys = sys == "PS1" ? "PS2" : sys == "PS2" ? "PS3" : "PS1";
                Add(result, "edge-cases.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"{games[i]} (USA).iso",
                        Extension = ".iso",
                        SizeBytes = sys == "PS1" ? 734003200 : sys == "PS2" ? 4700000000 : 25769803776,
                        Directory = "roms",
                    },
                    Tags = ["cross-system", "ps-disambiguation"],
                    Difficulty = "hard",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = sys,
                        Category = "Game",
                        Confidence = 80,
                        HasConflict = true,
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = "DiscHeader",
                        AcceptableConsoleKeys = [otherSys]
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    private void GenerateHeaderlessEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // Cartridge systems with headers — generate headerless variants
        var headerSystems = Systems.Where(s => s.HasCartridgeHeader).ToArray();
        foreach (var sys in headerSystems)
        {
            var ext = GetPrimaryExtension(sys);
            for (int i = 0; i < 3; i++)
            {
                var id = NextId("gr", sys.Key, "hless");
                if (_existingIds.Contains(id)) continue;

                var gameName = sys.SampleGames[i % sys.SampleGames.Length];
                Add(result, "golden-realworld.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"{gameName} (USA){ext}",
                        Extension = ext,
                        SizeBytes = sys.TypicalSize - 16,
                        Directory = sys.FolderAlias,
                        Stub = new StubInfo { Generator = "generic-headerless", Variant = "no-header" }
                    },
                    Tags = ["headerless", "clean-reference"],
                    Difficulty = "medium",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = sys.Key,
                        Category = "Game",
                        Confidence = 80,
                        HasConflict = false,
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = "UniqueExtension",
                        AcceptableAlternatives = ["FolderName", "DatLookup"]
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    private void GenerateChdRawSha1Entries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // Disc-based systems with CHD files using raw SHA1
        var chdSystems = Systems.Where(s => s.DiscBased).Take(10).ToArray();
        foreach (var sys in chdSystems)
        {
            var id = NextId("gr", sys.Key, "chdsha");
            if (_existingIds.Contains(id)) continue;

            Add(result, "golden-realworld.jsonl", new GroundTruthEntry
            {
                Id = id,
                Source = new SourceInfo
                {
                    FileName = $"{sys.SampleGames[0]} (USA).chd",
                    Extension = ".chd",
                    SizeBytes = 734003200,
                    Directory = sys.FolderAlias,
                },
                Tags = ["chd-raw-sha1", "clean-reference", sys.DatEcosystem],
                Difficulty = "medium",
                Expected = new ExpectedResult
                {
                    ConsoleKey = sys.Key,
                    Category = "Game",
                    Confidence = 92,
                    HasConflict = false,
                    DatMatchLevel = "exact",
                    DatEcosystem = sys.DatEcosystem,
                    SortDecision = "sort"
                },
                DetectionExpectations = new DetectionExpectations
                {
                    PrimaryMethod = "DatMatch",
                    AcceptableAlternatives = ["DiscHeader"]
                },
                FileModel = new FileModelInfo { Type = "single-file" },
                Relationships = new RelationshipInfo()
            });
        }
    }

    private void GenerateExtraArcadeEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // Additional arcade entries: dat-exact, wrong-name, junk for ARCADE/NEOGEO
        var arcadeKeys = new[] { "ARCADE", "NEOGEO" };
        var extraGames = new[]
        {
            "qbert", "frogger", "asteroids", "centipede", "defender",
            "robotron", "joust", "tempest", "digdug", "mappy",
            "rallyx", "bosconian", "xevious", "starforce", "gunsmoke",
        };

        foreach (var key in arcadeKeys)
        {
            var sys = Systems.First(s => s.Key == key);
            // Extra dat-exact entries
            for (int i = 0; i < 8; i++)
            {
                var game = extraGames[i % extraGames.Length];
                var id = NextId("dc", key, "datex");
                if (_existingIds.Contains(id)) continue;

                Add(result, "dat-coverage.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"{game}_{key.ToLowerInvariant()}_{i}.zip",
                        Extension = ".zip",
                        SizeBytes = 65536 * (i + 1),
                        Directory = sys.FolderAlias,
                    },
                    Tags = ["dat-exact-match", "clean-reference", sys.DatEcosystem],
                    Difficulty = "easy",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = key,
                        Category = "Game",
                        Confidence = 95,
                        HasConflict = false,
                        DatMatchLevel = "exact",
                        DatEcosystem = sys.DatEcosystem,
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = "DatLookup",
                        AcceptableAlternatives = ["FolderName"]
                    },
                    FileModel = new FileModelInfo { Type = "archive" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    private void GenerateExtraComputerEntries(Dictionary<string, List<GroundTruthEntry>> result)
    {
        // Computer systems need more entries to hit the 120 hardFail gate
        var computerGames = new Dictionary<string, string[]>
        {
            ["A800"] = ["Star Raiders", "MULE", "Archon", "Rescue on Fractalus"],
            ["AMIGA"] = ["Turrican II", "Lemmings", "Speedball 2", "Sensible Soccer"],
            ["ATARIST"] = ["Dungeon Master", "Starglider", "Stunt Car Racer", "Captive"],
            ["C64"] = ["Impossible Mission", "Maniac Mansion", "Ghosts n Goblins", "Uridium"],
            ["CPC"] = ["Gryzor", "Rick Dangerous", "Renegade", "R-Type CPC"],
            ["DOS"] = ["DOOM", "Commander Keen", "Prince of Persia", "Wolfenstein 3D"],
            ["MSX"] = ["Nemesis", "Metal Gear", "Vampire Killer", "Penguin Adventure"],
            ["PC98"] = ["Ys IV", "Policenauts", "Snatcher", "Eve Burst Error"],
            ["X68K"] = ["Akumajou Dracula", "Gradius", "Star Wars X68K", "Parodius Da"],
            ["ZX"] = ["Manic Miner", "Jet Set Willy", "Dizzy", "Atic Atac"],
        };

        foreach (var (key, games) in computerGames)
        {
            var sys = Systems.FirstOrDefault(s => s.Key == key);
            if (sys is null) continue;
            var ext = GetPrimaryExtension(sys);

            // Extra clean-reference entries
            for (int i = 0; i < 3; i++)
            {
                var id = NextId("gc", key, "compref");
                if (_existingIds.Contains(id)) continue;

                Add(result, "golden-core.jsonl", new GroundTruthEntry
                {
                    Id = id,
                    Source = new SourceInfo
                    {
                        FileName = $"{games[i % games.Length]} (Europe){ext}",
                        Extension = ext,
                        SizeBytes = sys.TypicalSize + (i * 2048),
                        Directory = sys.FolderAlias,
                    },
                    Tags = BuildTags("clean-reference", sys),
                    Difficulty = "easy",
                    Expected = new ExpectedResult
                    {
                        ConsoleKey = key,
                        Category = "Game",
                        Confidence = 95,
                        HasConflict = false,
                        DatMatchLevel = "exact",
                        DatEcosystem = sys.DatEcosystem,
                        SortDecision = "sort"
                    },
                    DetectionExpectations = new DetectionExpectations
                    {
                        PrimaryMethod = sys.PrimaryDetection,
                        AcceptableAlternatives = GetAlternatives(sys)
                    },
                    FileModel = new FileModelInfo { Type = "single-file" },
                    Relationships = new RelationshipInfo()
                });
            }
        }
    }

    // ═══ HELPERS ═════════════════════════════════════════════════════════

    private string NextId(string prefix, string system, string subclass)
    {
        var key = $"{prefix}-{system}-{subclass}";
        var counter = _idCounters.GetValueOrDefault(key, 0) + 1;
        _idCounters[key] = counter;
        return $"{prefix}-{system}-{subclass}-{counter:D3}";
    }

    private static void Add(Dictionary<string, List<GroundTruthEntry>> result,
        string file, GroundTruthEntry entry)
    {
        if (!result.TryGetValue(file, out var list))
        {
            list = [];
            result[file] = list;
        }
        list.Add(entry);
    }

    private static string GetPrimaryExtension(SystemDef sys)
    {
        if (sys.UniqueExts.Length > 0) return sys.UniqueExts[0];
        if (sys.AmbigExts.Length > 0) return sys.AmbigExts[0];
        return ".bin";
    }

    private static string[] BuildTags(string? primaryTag, SystemDef sys)
    {
        var tags = new List<string>();
        if (primaryTag is not null) tags.Add(primaryTag);

        if (sys.HasCartridgeHeader) tags.Add("cartridge-header");
        else if (sys.DiscBased) tags.Add("disc-header");
        if (sys.UniqueExts.Length > 0) tags.Add("unique-extension");

        tags.Add(sys.DatEcosystem);
        return tags.ToArray();
    }

    private static string[] GetAlternatives(SystemDef sys)
    {
        var alts = new List<string>();
        if (sys.UniqueExts.Length > 0 && sys.PrimaryDetection != "UniqueExtension")
            alts.Add("UniqueExtension");
        if (sys.PrimaryDetection != "FolderName")
            alts.Add("FolderName");
        return alts.ToArray();
    }

    private static int GetTierCount(string key, int t1, int t2, int t3, int t4)
    {
        if (Tier1.Contains(key)) return t1;
        if (Tier2.Contains(key)) return t2;
        if (Tier3.Contains(key)) return t3;
        return t4;
    }

    private static bool IsTier1Or2(string key) => Tier1.Contains(key) || Tier2.Contains(key);

    private static readonly HashSet<string> Tier1 = new(StringComparer.Ordinal)
    { "NES", "SNES", "N64", "GBA", "GB", "GBC", "MD", "PS1", "PS2" };

    private static readonly HashSet<string> Tier2 = new(StringComparer.Ordinal)
    { "32X", "PSP", "SAT", "DC", "GC", "WII", "SMS", "GG", "PCE", "LYNX",
      "A78", "A26", "NDS", "3DS", "SWITCH", "AMIGA" };

    private static readonly HashSet<string> Tier3 = new(StringComparer.Ordinal)
    { "PCECD", "PCFX", "SCD", "NEOCD", "CD32", "CDI", "JAGCD", "FMTOWNS",
      "3DO", "ATARIST", "C64", "MSX", "ZX", "COLECO", "INTV", "VB",
      "VECTREX", "A52", "NGP", "WS", "WSC", "ODYSSEY2" };

    private static SystemDef[] BuildSystemCatalog()
    {
        return
        [
            S("3DO",   true, [],                    [".iso",".bin",".chd"],  "3do",        "DiscHeader",       "redump",  false, ["Road Rash","Need for Speed"],                      734003200),
            S("3DS",   false,[".3ds",".cia"],        [],                     "3ds",        "UniqueExtension",  "no-intro",false, ["Pokemon X","Animal Crossing New Leaf"],             2147483648),
            S("32X",   false,[".32x"],               [],                     "32x",        "UniqueExtension",  "no-intro",true,  ["Doom","Knuckles Chaotix","Star Wars Arcade"],       3145728),
            S("A26",   false,[".a26"],               [],                     "a26",        "UniqueExtension",  "no-intro",false, ["Combat","Pitfall","Adventure"],                     4096),
            S("A52",   false,[".a52"],               [],                     "a52",        "UniqueExtension",  "no-intro",false, ["Pac-Man","Moon Patrol"],                            16384),
            S("A78",   false,[".a78"],               [],                     "a78",        "UniqueExtension",  "no-intro",true,  ["Asteroids","Dig Dug","Food Fight"],                 65536),
            S("A800",  false,[".atr",".xex",".xfd"], [],                     "a800",       "UniqueExtension",  "tosec",   false, ["Star Raiders","MULE","Archon"],                     131072),
            S("AMIGA", false,[".adf"],               [],                     "amiga",      "UniqueExtension",  "tosec",   false, ["Turrican II","Lemmings","Speedball 2"],             901120),
            S("ARCADE",false,[],                     [],                     "arcade",     "FolderName",       "mame",    false, ["pacman","dkong","galaga","sf2","tmnt"],              3145728),
            S("ATARIST",false,[".st",".stx"],        [],                     "atarist",    "UniqueExtension",  "tosec",   false, ["Dungeon Master","Starglider"],                     737280),
            S("C64",   false,[".d64",".t64"],        [],                     "c64",        "UniqueExtension",  "tosec",   false, ["Impossible Mission","Maniac Mansion"],              174848),
            S("CD32",  true, [],                     [".iso",".bin",".chd"], "cd32",       "DiscHeader",       "tosec",   false, ["Microcosm","Super Stardust"],                       734003200),
            S("CDI",   true, [".cdi"],               [".iso",".bin",".chd"], "cdi",        "UniqueExtension",  "redump",  false, ["Hotel Mario","Zelda Wand of Gamelon"],              734003200),
            S("CHANNELF",false,[],                   [],                     "channelf",   "FolderName",       "no-intro",false, ["Hockey","Tennis"],                                  2048),
            S("COLECO",false,[".col"],               [],                     "coleco",     "UniqueExtension",  "no-intro",false, ["Donkey Kong","Zaxxon"],                             16384),
            S("CPC",   false,[],                     [],                     "cpc",        "FolderName",       "tosec",   false, ["Gryzor","Rick Dangerous"],                          65536),
            S("DC",    true, [".gdi"],               [".iso",".bin",".chd"], "dc",         "UniqueExtension",  "redump",  false, ["Sonic Adventure","Shenmue","Jet Grind Radio"],      1073741824),
            S("DOS",   false,[],                     [],                     "dos",        "FolderName",       "tosec",   false, ["DOOM","Commander Keen","Prince of Persia"],          1048576),
            S("FMTOWNS",true,[],                    [".iso",".bin",".chd"], "fmtowns",    "DiscHeader",       "tosec",   false, ["Zak McKracken FM","After Burner"],                  734003200),
            S("GB",    false,[".gb"],                [],                     "gb",         "CartridgeHeader",  "no-intro",true,  ["Tetris","Super Mario Land","Pokemon Red"],           32768),
            S("GBA",   false,[".gba"],               [],                     "gba",        "CartridgeHeader",  "no-intro",true,  ["Pokemon FireRed","Metroid Fusion","Minish Cap"],    16777216),
            S("GBC",   false,[".gbc"],               [],                     "gbc",        "CartridgeHeader",  "no-intro",true,  ["Pokemon Crystal","Links Awakening DX"],             2097152),
            S("GC",    true, [],                     [".iso",".gcz",".rvz"], "gc",         "DiscHeader",       "redump",  false, ["Melee","Wind Waker","Metroid Prime"],               1459617792),
            S("GG",    false,[".gg"],                [],                     "gg",         "UniqueExtension",  "no-intro",false, ["Sonic Chaos","Columns","Shinobi"],                  262144),
            S("INTV",  false,[".int"],               [],                     "intv",       "UniqueExtension",  "no-intro",false, ["Astrosmash","Utopia"],                              16384),
            S("JAG",   false,[".j64"],               [],                     "jag",        "UniqueExtension",  "no-intro",false, ["Tempest 2000","Rayman"],                            4194304),
            S("JAGCD", true, [],                     [".iso",".bin",".chd"], "jaguarcd",   "DiscHeader",       "redump",  false, ["Myst","Battlemorph"],                               734003200),
            S("LYNX",  false,[".lnx"],               [],                     "lynx",       "CartridgeHeader",  "no-intro",true,  ["California Games","Todd-s Adventures","Chips Challenge"], 262144),
            S("MD",    false,[".md",".gen"],          [],                     "md",         "CartridgeHeader",  "no-intro",true,  ["Sonic The Hedgehog","Streets of Rage 2","Gunstar Heroes"], 1048576),
            S("MSX",   false,[".mx1",".mx2"],        [],                     "msx",        "UniqueExtension",  "tosec",   false, ["Nemesis","Metal Gear"],                             131072),
            S("N64",   false,[".n64",".z64",".v64"], [],                     "n64",        "CartridgeHeader",  "no-intro",true,  ["Super Mario 64","Ocarina of Time","GoldenEye 007"], 8388608),
            S("NDS",   false,[".nds"],               [],                     "nds",        "UniqueExtension",  "no-intro",false, ["New Super Mario Bros","Pokemon Diamond"],            134217728),
            S("NEOCD", true, [],                     [".iso",".bin",".chd"], "neocd",      "DiscHeader",       "redump",  false, ["Viewpoint","Samurai Shodown RPG"],                  734003200),
            S("NEOGEO",false,[],                     [],                     "neogeo",     "FolderName",       "mame",    false, ["kof98","mslug","samsho2","garou"],                   33554432),
            S("NES",   false,[".nes"],               [],                     "nes",        "CartridgeHeader",  "no-intro",true,  ["Super Mario Bros","Zelda","Mega Man 2","Castlevania","Metroid"], 40976),
            S("NGP",   false,[".ngp"],               [],                     "ngp",        "UniqueExtension",  "no-intro",false, ["SNK vs Capcom Match","Neo Turf Masters"],           1048576),
            S("NGPC",  false,[],                     [],                     "ngpc",       "FolderName",       "no-intro",false, ["SNK vs Capcom Card","Metal Slug 1st"],               2097152),
            S("ODYSSEY2",false,[".o2"],              [],                     "odyssey2",   "UniqueExtension",  "no-intro",false, ["Quest for Rings","KC Munchkin"],                    4096),
            S("PC98",  false,[],                     [],                     "pc98",       "FolderName",       "tosec",   false, ["Ys IV","Policenauts"],                               1048576),
            S("PCE",   false,[".pce"],               [],                     "pce",        "UniqueExtension",  "no-intro",false, ["R-Type","Bonk-s Adventure"],                         524288),
            S("PCECD", true, [],                     [".iso",".bin",".chd"], "pcecd",      "DiscHeader",       "redump",  false, ["Ys Book I II","Rondo of Blood"],                    734003200),
            S("PCFX",  true, [".pcfx"],              [".iso",".bin",".chd"], "pcfx",       "UniqueExtension",  "redump",  false, ["Battle Heat","Zenki"],                              734003200),
            S("POKEMINI",false,[".min"],             [],                     "pokemini",   "UniqueExtension",  "no-intro",false, ["Pokemon Party Mini","Pokemon Zany Cards"],           65536),
            S("PS1",   true, [],                     [".iso",".bin",".chd"], "ps1",        "DiscHeader",       "redump",  false, ["Crash Bandicoot","FF VII","MGS","Castlevania SOTN","Resident Evil 2"], 734003200),
            S("PS2",   true, [],                     [".iso",".bin",".chd"], "ps2",        "DiscHeader",       "redump",  false, ["GTA San Andreas","FF X","Kingdom Hearts","Shadow of the Colossus","MGS 3"], 4700000000),
            S("PS3",   true, [],                     [".iso",".bin",".chd"], "ps3",        "DiscHeader",       "redump",  false, ["Uncharted 2","The Last of Us"],                     25769803776),
            S("PSP",   true, [],                     [".iso",".cso"],        "psp",        "DiscHeader",       "redump",  false, ["God of War Chains of Olympus","Lumines"],            1800000000),
            S("SAT",   true, [],                     [".iso",".bin",".chd"], "sat",        "DiscHeader",       "redump",  false, ["Nights into Dreams","Panzer Dragoon Saga"],          734003200),
            S("SCD",   true, [],                     [".iso",".bin",".chd"], "scd",        "DiscHeader",       "redump",  false, ["Sonic CD","Lunar"],                                 734003200),
            S("SG1000",false,[".sg",".sc"],          [],                     "sg1000",     "UniqueExtension",  "no-intro",false, ["Congo Bongo","Gulkave"],                            16384),
            S("SMS",   false,[".sms"],               [],                     "sms",        "UniqueExtension",  "no-intro",false, ["Alex Kidd","Phantasy Star","Sonic the Hedgehog MS"], 262144),
            S("SNES",  false,[".sfc",".smc"],        [],                     "snes",       "CartridgeHeader",  "no-intro",true,  ["Super Mario World","Link to the Past","Super Metroid","Chrono Trigger","FF VI"], 524288),
            S("SUPERVISION",false,[],                [],                     "supervision","FolderName",       "no-intro",false, ["Crystball","Alien"],                                32768),
            S("SWITCH",false,[".nsp",".xci"],        [],                     "switch",     "UniqueExtension",  "no-intro",false, ["BotW","Mario Odyssey","Animal Crossing NH"],         16106127360),
            S("VB",    false,[".vb"],                [],                     "vb",         "UniqueExtension",  "no-intro",false, ["Virtual Boy Wario Land","Mario Clash"],              1048576),
            S("VECTREX",false,[".vec"],              [],                     "vectrex",    "UniqueExtension",  "no-intro",false, ["Mine Storm","Scramble"],                             8192),
            S("VITA",  false,[".vpk"],               [],                     "vita",       "UniqueExtension",  "no-intro",false, ["Persona 4 Golden","Tearaway"],                      4294967296),
            S("WII",   true, [".wbfs",".wad"],       [".iso",".gcz",".rvz"], "wii",        "UniqueExtension",  "redump",  false, ["Mario Galaxy","Twilight Princess Wii","Smash Brawl"], 4699979776),
            S("WIIU",  true, [".wux",".rpx"],        [".iso",".bin",".chd"], "wiiu",       "UniqueExtension",  "redump",  false, ["BotW WiiU","Mario Kart 8"],                         25769803776),
            S("WS",    false,[".ws"],                [],                     "ws",         "UniqueExtension",  "no-intro",false, ["Gunpey","Final Fantasy WS"],                        4194304),
            S("WSC",   false,[".wsc"],               [],                     "wsc",        "UniqueExtension",  "no-intro",false, ["Final Fantasy I WSC","SD Gundam"],                  4194304),
            S("X360",  true, [],                     [".iso",".bin",".chd"], "x360",       "DiscHeader",       "redump",  false, ["Halo 3","Gears of War"],                            7835492352),
            S("X68K",  false,[],                     [],                     "x68k",       "FolderName",       "tosec",   false, ["Akumajou Dracula","Gradius"],                        1048576),
            S("XBOX",  true, [],                     [".iso",".bin",".chd"], "xbox",       "DiscHeader",       "redump",  false, ["Halo CE","Fable"],                                  4700000000),
            S("ZX",    false,[".tzx"],               [],                     "zx",         "UniqueExtension",  "tosec",   false, ["Manic Miner","Jet Set Willy"],                      32768),
        ];
    }

    private static SystemDef S(string key, bool disc, string[] uExts, string[] aExts,
        string folder, string detection, string datEco, bool hasHeader,
        string[] games, long size)
        => new(key, disc, uExts, aExts, folder, detection, datEco, hasHeader, games, size);

    /// <summary>
    /// Writes expanded entries to JSONL files, merging with existing entries.
    /// </summary>
    public static void WriteToFiles(
        Dictionary<string, List<GroundTruthEntry>> existingByFile,
        Dictionary<string, List<GroundTruthEntry>> generated)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };

        var allFiles = existingByFile.Keys.Union(generated.Keys).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var file in allFiles)
        {
            var entries = new List<GroundTruthEntry>();
            if (existingByFile.TryGetValue(file, out var existing))
                entries.AddRange(existing);
            if (generated.TryGetValue(file, out var gen))
                entries.AddRange(gen);

            var path = Path.Combine(BenchmarkPaths.GroundTruthDir, file);
            var lines = entries.Select(e => JsonSerializer.Serialize(e, options));
            File.WriteAllLines(path, lines);
        }
    }
}
