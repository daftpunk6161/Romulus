using System.Net;
using System.Text;
using System.Text.Json;
using Romulus.Api;
using Romulus.Contracts;
using Romulus.Contracts.Errors;
using Romulus.Contracts.Models;
using Romulus.Contracts.Ports;
using Romulus.Core.Classification;
using Romulus.Infrastructure.FileSystem;
using Romulus.Infrastructure.Orchestration;
using Romulus.Infrastructure.Sorting;
using Xunit;

namespace Romulus.Tests;

/// <summary>
/// RED tests for audit tracker priority block 1-6:
/// 1) EP-01/02/03 (code-behind business logic)
/// 2) SORT-01 (dry-run/execute parity for set-path resolution)
/// 3) ORC-10/API-08 (disposed cancellation token fallback)
/// 4) EP-08 (profile PUT route/body id mismatch)
/// 5) API-02 (exception-message leakage)
/// 6) DI-06/07/08/09 (explicit singleton disposal at shutdown)
/// </summary>
public sealed class TrackerBlock1To6RedTests : IDisposable
{
    private const string ApiKey = "tracker-block-1-6-key";
    private readonly string _tempDir;

    public TrackerBlock1To6RedTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Romulus_TrackerBlock1To6_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public void EP_01_03_LibrarySafetyView_CodeBehind_MustNotContainBusinessLogicOrWatchers()
    {
        var source = File.ReadAllText(FindRepoFile("src", "Romulus.UI.Wpf", "Views", "LibrarySafetyView.xaml.cs"));

        Assert.DoesNotContain("RefreshLists(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SortDecision.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DataContextChanged +=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyChanged +=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EP_02_LibraryReportView_CodeBehind_MustNotBuildErrorSummaryOrMapDomainErrors()
    {
        var source = File.ReadAllText(FindRepoFile("src", "Romulus.UI.Wpf", "Views", "LibraryReportView.xaml.cs"));

        Assert.DoesNotContain("PopulateErrorSummary", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Models.UiError", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GUI-REPORTERR", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GUI-NOREPORT", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SORT_01_DryRunAndExecute_MustMatch_WhenSetMemberDestinationCannotBeResolved()
    {
        var dryRoot = Path.Combine(_tempDir, "dry");
        var moveRoot = Path.Combine(_tempDir, "move");
        Directory.CreateDirectory(dryRoot);
        Directory.CreateDirectory(moveRoot);

        var dryCue = WriteCueSet(dryRoot, "Game");
        var moveCue = WriteCueSet(moveRoot, "Game");

        var fs = new SetMemberBlockingFileSystem();
        var detector = BuildDetector();
        var sorter = new ConsoleSorter(fs, detector);

        var dry = sorter.Sort(
            new[] { dryRoot },
            new[] { ".cue" },
            dryRun: true,
            enrichedConsoleKeys: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [dryCue] = "PS1"
            },
            candidatePaths: new[] { dryCue });

        var execute = sorter.Sort(
            new[] { moveRoot },
            new[] { ".cue" },
            dryRun: false,
            enrichedConsoleKeys: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [moveCue] = "PS1"
            },
            candidatePaths: new[] { moveCue });

        Assert.Equal(execute.Moved, dry.Moved);
        Assert.Equal(execute.SetMembersMoved, dry.SetMembersMoved);
        Assert.Equal(execute.Failed, dry.Failed);
    }

    [Fact]
    public void ORC_10_API_08_DisposedCancellationSource_MustStillExposeCancelledToken()
    {
        var run = new RunRecord
        {
            RunId = Guid.NewGuid().ToString("N"),
            RequestFingerprint = "fp",
            StartedUtc = DateTime.UtcNow
        };

        run.CancellationRequested = true;
        run.CancellationSource.Dispose();
        _ = run.TryCancelExecution();

        var token = run.GetCancellationToken();

        Assert.True(token.CanBeCanceled);
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public async Task EP_08_ProfilePut_RouteAndBodyIdMismatch_MustReturn400()
    {
        using var factory = ApiTestFactory.Create(new Dictionary<string, string?>
        {
            ["ApiKey"] = ApiKey
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);

        using var content = new StringContent(
            JsonSerializer.Serialize(new
            {
                id = "body-id",
                version = 1,
                name = "Mismatch Profile",
                description = "route/body mismatch",
                settings = new
                {
                    mode = "DryRun"
                }
            }),
            Encoding.UTF8,
            "application/json");

        var response = await client.PutAsync("/profiles/route-id", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ApiErrorCodes.ProfileInvalid, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void API_02_ApiEndpoints_MustNotReturnRawExceptionMessages()
    {
        var profileEndpoints = File.ReadAllText(FindRepoFile("src", "Romulus.Api", "Program.ProfileWorkflowEndpoints.cs"));
        var program = File.ReadAllText(FindRepoFile("src", "Romulus.Api", "Program.cs"));
        var helpers = File.ReadAllText(FindRepoFile("src", "Romulus.Api", "ProgramHelpers.cs"));

        Assert.DoesNotContain("ApiError(400, ApiErrorCodes.ProfileInvalid, ex.Message)", profileEndpoints, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiError(400, ApiErrorCodes.ProfileDeleteBlocked, ex.Message)", profileEndpoints, StringComparison.Ordinal);
        Assert.DoesNotContain("{ex.Message}", profileEndpoints, StringComparison.Ordinal);

        Assert.DoesNotContain("ApiError(409, ApiErrorCodes.ExportNotReady, ex.Message)", program, StringComparison.Ordinal);

        Assert.DoesNotContain("Invalid outputPath: {ex.Message}", helpers, StringComparison.Ordinal);
    }

    [Fact]
    public void DI_06_07_08_09_ProgramShutdown_MustDisposeCriticalSingletonsExplicitly()
    {
        var source = File.ReadAllText(FindRepoFile("src", "Romulus.Api", "Program.cs"));

        Assert.Contains("GetService<ApiAutomationService>()", source, StringComparison.Ordinal);
        Assert.Contains("GetService<PersistedReviewDecisionService>()", source, StringComparison.Ordinal);
        Assert.Contains("GetService<ICollectionIndex>()", source, StringComparison.Ordinal);
        Assert.Contains("GetService<IReviewDecisionStore>()", source, StringComparison.Ordinal);
        Assert.Contains("Dispose()", source, StringComparison.Ordinal);
    }

    private static ConsoleDetector BuildDetector()
        => new(
        [
            new ConsoleInfo("PS1", "PlayStation", false, [".cue", ".bin"], Array.Empty<string>(), ["PS1"])
        ]);

    private static string WriteCueSet(string root, string stem)
    {
        var cue = Path.Combine(root, stem + ".cue");
        var bin = Path.Combine(root, stem + ".bin");
        File.WriteAllText(cue, "FILE \"" + stem + ".bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00");
        File.WriteAllBytes(bin, new byte[] { 1, 2, 3, 4 });
        return cue;
    }

    private static string FindRepoFile(params string[] parts)
    {
        var dataDir = RunEnvironmentBuilder.ResolveDataDir();
        var repoRoot = Directory.GetParent(dataDir)?.FullName
            ?? throw new InvalidOperationException("Repository root could not be resolved from data directory.");
        return Path.Combine(new[] { repoRoot }.Concat(parts).ToArray());
    }

    private sealed class SetMemberBlockingFileSystem : IFileSystem
    {
        private readonly FileSystemAdapter _inner = new();

        public bool TestPath(string literalPath, string pathType = "Any")
            => _inner.TestPath(literalPath, pathType);

        public string EnsureDirectory(string path)
            => _inner.EnsureDirectory(path);

        public IReadOnlyList<string> GetFilesSafe(string root, IEnumerable<string>? allowedExtensions = null)
            => _inner.GetFilesSafe(root, allowedExtensions);

        public string? MoveItemSafely(string sourcePath, string destinationPath)
            => _inner.MoveItemSafely(sourcePath, destinationPath);

        public string? MoveItemSafely(string sourcePath, string destinationPath, bool overwrite)
            => _inner.MoveItemSafely(sourcePath, destinationPath, overwrite);

        public string? ResolveChildPathWithinRoot(string rootPath, string relativePath)
        {
            if (relativePath.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                return null;

            return _inner.ResolveChildPathWithinRoot(rootPath, relativePath);
        }

        public bool IsReparsePoint(string path)
            => _inner.IsReparsePoint(path);

        public void DeleteFile(string path)
            => _inner.DeleteFile(path);

        public void CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
            => _inner.CopyFile(sourcePath, destinationPath, overwrite);
    }
}
