using System.Text.Json;
using Romulus.Api;
using Romulus.CLI;
using Romulus.Contracts.Models;
using Romulus.Contracts.Ports;
using Romulus.Infrastructure.Audit;
using Romulus.Infrastructure.FileSystem;
using Romulus.Infrastructure.Orchestration;
using Romulus.UI.Wpf.Services;
using Romulus.UI.Wpf.ViewModels;
using Romulus.Tests.TestFixtures;
using Xunit;
using CliProgram = Romulus.CLI.Program;

namespace Romulus.Tests;

/// <summary>
/// Block C2 - EntryPoint Paritaet erweitern.
///
/// Existing parity tests (ReportParityTests) verify aggregate counts and per-group
/// (GameKey, Winner, Losers) identity across CLI / API / WPF.
///
/// This suite extends parity to the per-DedupeGroup decision/classification fields
/// that downstream sorting and reports rely on:
///   - ConsoleKey
///   - PlatformFamily
///   - DecisionClass
///   - SortDecision
///   - ClassificationReasonCode
///   - DatMatch flag
///
/// Determinism rule: same input dataset MUST yield byte-identical per-group field
/// projections across all three entry points. Any divergence is a release blocker.
/// </summary>
public sealed class BlockC2_EntryPointFieldParityTests : IDisposable
{
    private readonly string _tempDir;

    public BlockC2_EntryPointFieldParityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Romulus_C2_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort */ }
    }

    [Fact]
    public async Task C2_01_PerDedupeGroup_DecisionFields_AreIdentical_AcrossCliApiWpf()
    {
        var root = Path.Combine(_tempDir, "roms");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Game (USA).zip"), "usa");
        File.WriteAllText(Path.Combine(root, "Game (Europe).zip"), "eu");
        File.WriteAllText(Path.Combine(root, "Other (Japan).zip"), "jp");

        // ── CLI ───────────────────────────────────────────────────────
        var cliOptions = new CliRunOptions
        {
            Roots = [root],
            Mode = "DryRun",
            PreferRegions = ["EU", "US", "JP", "WORLD"]
        };
        var (cliExitCode, cliStdout, cliStderr) = RunCliWithCapturedConsole(cliOptions);
        Assert.Equal(0, cliExitCode);
        using var cliJson = ParseCliSummaryJson(cliStdout, cliStderr);

        // ── WPF (Orchestrator) ────────────────────────────────────────
        var vm = CreateViewModel();
        vm.Roots.Add(root);
        vm.DryRun = true;
        vm.PreferEU = true; vm.PreferUS = true; vm.PreferJP = true; vm.PreferWORLD = true;

        var runService = new RunService();
        var (orchestrator, options, auditPath, reportPath) = await runService.BuildOrchestratorAsync(vm);
        var wpf = await runService.ExecuteRunAsync(orchestrator, options, auditPath, reportPath, CancellationToken.None);

        // ── API ───────────────────────────────────────────────────────
        var manager = new RunManager(new FileSystemAdapter(), new AuditCsvStore());
        var apiRun = manager.TryCreate(new RunRequest
        {
            Roots = [root],
            Mode = "DryRun",
            PreferRegions = ["EU", "US", "JP", "WORLD"]
        }, "DryRun");
        Assert.NotNull(apiRun);
        var wait = await manager.WaitForCompletion(apiRun!.RunId, timeout: TimeSpan.FromSeconds(20));
        Assert.Equal(RunWaitDisposition.Completed, wait.Disposition);
        var api = manager.Get(apiRun.RunId)!.Result!;

        // ── Field projections per group ───────────────────────────────
        var wpfProjection = ProjectFromCandidates(wpf.Result.DedupeGroups.Select(g => g.Winner));
        var apiProjection = ProjectFromCandidates(api.DedupeGroups.Select(g => g.Winner));

        Assert.Equal(wpfProjection, apiProjection);
        Assert.True(wpfProjection.Length > 0, "Need at least one group to assert parity.");

        // Sanity: counts match (CLI does not expose per-group decision fields in summary,
        // so we use group count + winners as an additional CLI parity gate).
        Assert.Equal(wpf.Result.GroupCount, cliJson.RootElement.GetProperty("Groups").GetInt32());
        Assert.Equal(wpf.Result.WinnerCount, cliJson.RootElement.GetProperty("Keep").GetInt32());
    }

    private static string[] ProjectFromCandidates(IEnumerable<RomCandidate> winners)
        => winners
            .Select(w => string.Join("|",
                w.GameKey.ToLowerInvariant(),
                w.ConsoleKey,
                w.PlatformFamily,
                w.DecisionClass,
                w.SortDecision,
                w.ClassificationReasonCode,
                w.DatMatch ? "DAT" : "NODAT"))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

    private static (int ExitCode, string Stdout, string Stderr) RunCliWithCapturedConsole(CliRunOptions options)
    {
        lock (SharedTestLocks.ConsoleLock)
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            try
            {
                CliProgram.SetConsoleOverrides(stdout, stderr);
                var exitCode = CliProgram.RunForTests(options);
                return (exitCode, stdout.ToString(), stderr.ToString());
            }
            finally
            {
                CliProgram.SetConsoleOverrides(null, null);
            }
        }
    }

    private static JsonDocument ParseCliSummaryJson(string stdout, string? stderr = null)
        => CliSummaryJsonParser.ParseSummary(stdout, stderr, "TotalFiles", "Groups", "Keep", "Dupes", "Results");

    private static MainViewModel CreateViewModel()
        => new(new StubThemeService(), new StubDialogService());

    private sealed class StubDialogService : IDialogService
    {
        public string? BrowseFolder(string title = "Ordner auswählen") => null;
        public string? BrowseFile(string title = "Datei auswählen", string filter = "Alle Dateien|*.*") => null;
        public string? SaveFile(string title = "Speichern unter", string filter = "Alle Dateien|*.*", string? defaultFileName = null) => null;
        public bool Confirm(string message, string title = "Bestätigung") => true;
        public void Info(string message, string title = "Information") { }
        public void Error(string message, string title = "Fehler") { }
        public ConfirmResult YesNoCancel(string message, string title = "Frage") => ConfirmResult.Yes;
        public string ShowInputBox(string prompt, string title = "Eingabe", string defaultValue = "") => defaultValue;
        public void ShowText(string title, string content) { }
        public bool DangerConfirm(string title, string message, string confirmText, string buttonLabel = "Bestätigen") => true;
        public bool ConfirmConversionReview(string title, string summary, IReadOnlyList<ConversionReviewEntry> entries) => true;
        public bool ConfirmDatRenamePreview(IReadOnlyList<DatAuditEntry> renameProposals) => true;
    }
}
