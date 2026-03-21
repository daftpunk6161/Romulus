using System.Net;
using System.Text;

namespace RomCleanup.Tests.Benchmark;

/// <summary>
/// Writes a compact HTML representation of benchmark results for CI artifacts.
/// </summary>
internal static class BenchmarkHtmlReportWriter
{
    public static void Write(BenchmarkReport report, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var html = BuildHtml(report);
        File.WriteAllText(path, html, Encoding.UTF8);
    }

    public static string BuildHtml(BenchmarkReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\" />");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("<title>RomCleanup Benchmark Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Segoe UI, sans-serif; margin: 24px; color: #1f2937; }");
        sb.AppendLine("h1, h2 { margin: 0 0 12px 0; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }");
        sb.AppendLine("th, td { border: 1px solid #d1d5db; padding: 8px; text-align: left; }");
        sb.AppendLine("th { background: #f3f4f6; }");
        sb.AppendLine(".muted { color: #6b7280; }");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<h1>RomCleanup Benchmark Report</h1>");
        sb.AppendLine($"<p class=\"muted\">Generated: {WebUtility.HtmlEncode(report.Timestamp.ToString("u"))} | GroundTruth: {WebUtility.HtmlEncode(report.GroundTruthVersion)}</p>");

        sb.AppendLine("<h2>Summary</h2>");
        sb.AppendLine("<table><tbody>");
        AppendRow("Total Samples", report.TotalSamples.ToString());
        AppendRow("Correct", report.Correct.ToString());
        AppendRow("Acceptable", report.Acceptable.ToString());
        AppendRow("Wrong", report.Wrong.ToString());
        AppendRow("Missed", report.Missed.ToString());
        AppendRow("True Negative", report.TrueNegative.ToString());
        AppendRow("Junk Classified", report.JunkClassified.ToString());
        AppendRow("False Positive", report.FalsePositive.ToString());
        AppendRow("Wrong Match Rate", report.WrongMatchRate.ToString("P3"));
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<h2>Per System</h2>");
        sb.AppendLine("<table><thead><tr><th>System</th><th>Correct</th><th>Acceptable</th><th>Wrong</th><th>Missed</th><th>TrueNegative</th><th>JunkClassified</th><th>FalsePositive</th></tr></thead><tbody>");
        foreach (var pair in report.PerSystem.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var system = WebUtility.HtmlEncode(pair.Key);
            var summary = pair.Value;
            sb.AppendLine($"<tr><td>{system}</td><td>{summary.Correct}</td><td>{summary.Acceptable}</td><td>{summary.Wrong}</td><td>{summary.Missed}</td><td>{summary.TrueNegative}</td><td>{summary.JunkClassified}</td><td>{summary.FalsePositive}</td></tr>");
        }

        sb.AppendLine("</tbody></table>");

        sb.AppendLine("</body></html>");
        return sb.ToString();

        void AppendRow(string label, string value)
        {
            sb.AppendLine($"<tr><th>{WebUtility.HtmlEncode(label)}</th><td>{WebUtility.HtmlEncode(value)}</td></tr>");
        }
    }
}
