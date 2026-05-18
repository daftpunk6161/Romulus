using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Romulus.Api;
using Romulus.Contracts;
using Romulus.Contracts.Models;
using Romulus.Contracts.Ports;
using Romulus.Infrastructure.Review;
using Xunit;

namespace Romulus.Tests;

public sealed class ProgramReviewQueueAndBodyTests
{
    [Fact]
    public async Task BuildReviewQueueAsync_FiltersPagesAndCombinesRunAndPersistedApprovals()
    {
        var blockedPath = @"C:\roms\nes\blocked.nes";
        var reviewPath = @"C:\roms\snes\review.sfc";
        var unknownPath = @"C:\roms\snes\unknown.sfc";
        var sortedPath = @"C:\roms\snes\sorted.sfc";
        var run = new RunRecord
        {
            RunId = "review-run",
            CoreRunResult = new RunResult
            {
                Status = RunConstants.StatusOk,
                AllCandidates =
                [
                    Candidate(reviewPath, "SNES", SortDecision.Review),
                    Candidate(blockedPath, "NES", SortDecision.Blocked),
                    Candidate(unknownPath, "SNES", SortDecision.Unknown),
                    Candidate(sortedPath, "SNES", SortDecision.Sort)
                ]
            }
        };
        Assert.True(run.TryApproveReviewPath(blockedPath));
        using var reviewService = new PersistedReviewDecisionService(
            new InMemoryReviewDecisionStore(
            [
                new ReviewApprovalEntry
                {
                    Path = unknownPath,
                    ConsoleKey = "SNES",
                    SortDecision = SortDecision.Review,
                    MatchLevel = MatchLevel.Exact,
                    MatchReasoning = "persisted approval",
                    Source = "api",
                    ApprovedUtc = new DateTime(2026, 5, 18, 8, 0, 0, DateTimeKind.Utc)
                }
            ]));

        var all = await Program.BuildReviewQueueAsync(run, reviewService);
        var page = await Program.BuildReviewQueueAsync(run, reviewService, offset: 1, limit: 1);

        Assert.Equal(3, all.Total);
        Assert.Equal(3, all.Returned);
        Assert.False(all.HasMore);
        Assert.DoesNotContain(all.Items, item => item.MainPath == sortedPath);
        Assert.Equal([blockedPath, reviewPath, unknownPath], all.Items.Select(item => item.MainPath).ToArray());
        Assert.True(all.Items.Single(item => item.MainPath == blockedPath).Approved);
        Assert.False(all.Items.Single(item => item.MainPath == reviewPath).Approved);
        Assert.True(all.Items.Single(item => item.MainPath == unknownPath).Approved);

        Assert.Equal(1, page.Offset);
        Assert.Equal(1, page.Limit);
        Assert.Equal(1, page.Returned);
        Assert.True(page.HasMore);
        Assert.Equal(reviewPath, Assert.Single(page.Items).MainPath);
    }

    [Fact]
    public async Task BuildReviewQueueAsync_RunWithoutCoreResult_ReturnsStableEmptyQueue()
    {
        var queue = await Program.BuildReviewQueueAsync(
            new RunRecord { RunId = "empty-review-run" },
            reviewDecisionService: null,
            offset: 25,
            limit: 10);

        Assert.Equal("empty-review-run", queue.RunId);
        Assert.Equal(0, queue.Total);
        Assert.Equal(25, queue.Offset);
        Assert.Equal(10, queue.Limit);
        Assert.Equal(0, queue.Returned);
        Assert.False(queue.HasMore);
        Assert.Empty(queue.Items);
    }

    [Fact]
    public async Task ReadJsonBodyAsync_ContentLengthTooLarge_ReturnsStructuredBodyLimitError()
    {
        var context = NewJsonContext("");
        context.Request.ContentLength = 1_048_577;

        var (value, error) = await Program.ReadJsonBodyAsync<BodyDto>(context, "BODY", CancellationToken.None);

        Assert.Null(value);
        await AssertErrorCodeAsync(error, "BODY-BODY-TOO-LARGE");
    }

    [Fact]
    public async Task ReadJsonBodyAsync_ChunkedBodyTooLarge_ReturnsStructuredBodyLimitError()
    {
        var body = new string('x', 1_048_577);
        var context = NewJsonContext(body);

        var (value, error) = await Program.ReadJsonBodyAsync<BodyDto>(context, "BODY", CancellationToken.None);

        Assert.Null(value);
        await AssertErrorCodeAsync(error, "BODY-BODY-TOO-LARGE");
    }

    [Fact]
    public async Task ReadJsonBodyAsync_EmptyOrInvalidBody_ReturnsInvalidJson()
    {
        var empty = await Program.ReadJsonBodyAsync<BodyDto>(NewJsonContext("   "), "BODY", CancellationToken.None);
        var invalid = await Program.ReadJsonBodyAsync<BodyDto>(NewJsonContext("{not-json"), "BODY", CancellationToken.None);

        Assert.Null(empty.Value);
        Assert.Null(invalid.Value);
        await AssertErrorCodeAsync(empty.Error, "BODY-INVALID-JSON");
        await AssertErrorCodeAsync(invalid.Error, "BODY-INVALID-JSON");
    }

    [Fact]
    public async Task ReadJsonBodyAsync_ValidBody_DeserializesCaseInsensitivePayload()
    {
        var context = NewJsonContext("""{"NAME":"romulus","count":3}""");

        var (value, error) = await Program.ReadJsonBodyAsync<BodyDto>(context, "BODY", CancellationToken.None);

        Assert.Null(error);
        Assert.NotNull(value);
        Assert.Equal("romulus", value!.Name);
        Assert.Equal(3, value.Count);
    }

    private static RomCandidate Candidate(string path, string consoleKey, SortDecision decision)
        => new()
        {
            MainPath = path,
            ConsoleKey = consoleKey,
            GameKey = Path.GetFileNameWithoutExtension(path),
            Extension = Path.GetExtension(path),
            Category = FileCategory.Game,
            SortDecision = decision,
            DecisionClass = decision == SortDecision.Sort ? DecisionClass.Sort : DecisionClass.Unknown,
            EvidenceTier = EvidenceTier.Tier4_Unknown,
            PrimaryMatchKind = MatchKind.None,
            PlatformFamily = PlatformFamily.Unknown,
            DetectionConfidence = decision == SortDecision.Blocked ? 10 : 70,
            MatchEvidence = new MatchEvidence
            {
                Level = MatchLevel.None,
                Reasoning = $"{decision} candidate"
            }
        };

    private static DefaultHttpContext NewJsonContext(string body)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentType = "application/json";
        return context;
    }

    private static async Task AssertErrorCodeAsync(IResult? result, string expectedCode)
    {
        Assert.NotNull(result);
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await result!.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(body);
        Assert.Equal(expectedCode, doc.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    private sealed class InMemoryReviewDecisionStore(IReadOnlyList<ReviewApprovalEntry> approvals) : IReviewDecisionStore
    {
        public ValueTask UpsertApprovalsAsync(IReadOnlyList<ReviewApprovalEntry> approvals, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<ReviewApprovalEntry>> ListApprovalsAsync(
            IReadOnlyList<string> paths,
            CancellationToken ct = default)
        {
            var selected = approvals
                .Where(approval => paths.Any(path => string.Equals(path, approval.Path, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            return new ValueTask<IReadOnlyList<ReviewApprovalEntry>>(selected);
        }
    }

    private sealed class BodyDto
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}
