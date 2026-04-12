namespace Romulus.UI.Wpf.Models;

public sealed class ReportPreviewResult
{
    private ReportPreviewResult()
    {
    }

    public string? ReportFilePath { get; init; }
    public string? InlineHtml { get; init; }

    public static ReportPreviewResult FromReportFile(string reportFilePath)
        => new() { ReportFilePath = reportFilePath };

    public static ReportPreviewResult FromInlineHtml(string inlineHtml)
        => new() { InlineHtml = inlineHtml };
}
