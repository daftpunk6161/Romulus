using Romulus.Contracts;
using Romulus.Contracts.Models;
using Romulus.Infrastructure.Analysis;
using Romulus.Infrastructure.Reporting;
using Romulus.UI.Wpf.ViewModels;

namespace Romulus.UI.Wpf.Services;

/// <summary>
/// T-W5-REPORT-UNIFICATION: single canonical channel for HTML/CSV/JSON report
/// writing. Eliminates the prior <see cref="RunReportWriter"/> vs
/// <see cref="ReportGenerator"/> dual-truth fallback so GUI/CLI/API stay
/// byte-identical. When no live RunResult is present (Preview-only case),
/// the service synthesises one via
/// <see cref="CollectionExportService.BuildPreviewProjectionSource"/> and routes
/// through <see cref="RunReportWriter.WriteReport(string, RunResult, string)"/>.
/// </summary>
public interface IResultExportService
{
    HtmlReportWriteResult WriteHtmlReport(string targetPath, MainViewModel vm);
}

public readonly record struct HtmlReportWriteResult(bool Success, string Path, string ChannelUsed);

public sealed class ResultExportService : IResultExportService
{
    /// <summary>
    /// Single channel name surfaced by the unified writer. Kept on
    /// <see cref="HtmlReportWriteResult.ChannelUsed"/> for log breadcrumbs and
    /// downstream tests that want to drift-guard the unification.
    /// </summary>
    public const string CanonicalChannel = "RunReportWriter";

    public HtmlReportWriteResult WriteHtmlReport(string targetPath, MainViewModel vm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(vm);

        var mode = vm.CurrentRunState == Models.RunState.CompletedDryRun
            ? RunConstants.ModeDryRun
            : RunConstants.ModeMove;

        var source = vm.LastRunResult ?? CollectionExportService.BuildPreviewProjectionSource(
            vm.LastCandidates.ToArray(),
            vm.LastDedupeGroups.ToArray());

        RunReportWriter.WriteReport(targetPath, source, mode);
        return new HtmlReportWriteResult(true, targetPath, CanonicalChannel);
    }
}
