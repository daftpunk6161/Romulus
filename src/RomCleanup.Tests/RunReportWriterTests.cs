using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Orchestration;
using RomCleanup.Infrastructure.Reporting;
using Xunit;

namespace RomCleanup.Tests;

public class RunReportWriterTests
{
    [Fact]
    public void BuildSummary_PopulatesProjectionFields()
    {
        var winner = new RomCandidate { MainPath = @"C:\roms\Game (EU).chd", Category = FileCategory.Game, DatMatch = true };
        var loser = new RomCandidate { MainPath = @"C:\roms\Game (US).chd", Category = FileCategory.Game, DatMatch = false };
        var junk = new RomCandidate { MainPath = @"C:\roms\Game (Beta).zip", Category = FileCategory.Junk, DatMatch = false };

        var result = new RunResult
        {
            Status = "ok",
            TotalFilesScanned = 3,
            WinnerCount = 1,
            LoserCount = 1,
            GroupCount = 1,
            DedupeGroups = new[]
            {
                new DedupeResult
                {
                    GameKey = "game",
                    Winner = winner,
                    Losers = new[] { loser }
                }
            },
            AllCandidates = new[] { winner, loser, junk },
            DurationMs = 1500,
            MoveResult = new MovePhaseResult(MoveCount: 1, FailCount: 0, SavedBytes: 100, SkipCount: 0)
        };

        var summary = RunReportWriter.BuildSummary(result, "DryRun");

        Assert.Equal(3, summary.TotalFiles);
        Assert.Equal(3, summary.Candidates);
        Assert.Equal(1, summary.KeepCount);
        Assert.Equal(1, summary.DupesCount);
        Assert.Equal(1, summary.GamesCount);
        Assert.Equal(1, summary.JunkCount);
        Assert.Equal(1, summary.DatMatches);
        Assert.Equal(100, summary.SavedBytes);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), summary.Duration);
        Assert.InRange(summary.HealthScore, 0, 100);
    }

    [Fact]
    public void BuildSummary_WhenAccountedExceedsScanned_Throws()
    {
        var winner = new RomCandidate { MainPath = @"C:\roms\Game (EU).chd", Category = FileCategory.Game };
        var loser = new RomCandidate { MainPath = @"C:\roms\Game (US).chd", Category = FileCategory.Game };

        var result = new RunResult
        {
            TotalFilesScanned = 1,
            WinnerCount = 1,
            LoserCount = 1,
            DedupeGroups = new[]
            {
                new DedupeResult
                {
                    GameKey = "game",
                    Winner = winner,
                    Losers = new[] { loser }
                }
            },
            AllCandidates = new[] { winner, loser }
        };

        Assert.Throws<InvalidOperationException>(() => RunReportWriter.BuildSummary(result, "DryRun"));
    }
}