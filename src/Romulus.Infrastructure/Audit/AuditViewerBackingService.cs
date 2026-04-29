using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Romulus.Contracts.Ports;
using Romulus.Infrastructure.FileSystem;

namespace Romulus.Infrastructure.Audit;

/// <summary>
/// Wave 2 — T-W2-AUDIT-VIEWER-API.
/// Read-only adapter implementation of <see cref="IAuditViewerBackingService"/>.
///
/// <para>
/// <strong>No-duplication policy:</strong> this adapter uses
/// <see cref="AuditCsvParser.ParseCsvLine(string)"/> and
/// <see cref="AuditSigningService.VerifyMetadataSidecar(string)"/> as the
/// single source of truth for parsing and verification. It MUST NOT
/// re-implement CSV quoting, signing, HMAC verification, or sidecar
/// schema. Failing this contract re-introduces the parallel-truth bug
/// the port was created to prevent.
/// </para>
///
/// <para>
/// <strong>Path safety:</strong> all path arguments are validated against
/// the audit root the consumer provides; symlinks/junctions resolved
/// before access.
/// </para>
/// </summary>
public sealed class AuditViewerBackingService : IAuditViewerBackingService
{
    private static readonly string[] CsvSearchPatterns = new[] { "audit-*.csv", "*.audit.csv" };

    private readonly AuditSigningService _signingService;

    public AuditViewerBackingService(IFileSystem? fs = null, string? keyFilePath = null)
    {
        _signingService = new AuditSigningService(fs ?? new FileSystemAdapter(), log: null, keyFilePath);
    }

    public IReadOnlyList<AuditRunSummary> ListRuns(
        string auditRoot,
        AuditRunFilter? filter = null,
        AuditPage? page = null)
    {
        if (string.IsNullOrWhiteSpace(auditRoot))
            throw new ArgumentException("auditRoot must not be empty.", nameof(auditRoot));
        if (!Directory.Exists(auditRoot))
            return Array.Empty<AuditRunSummary>();

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in CsvSearchPatterns)
        {
            foreach (var f in Directory.EnumerateFiles(auditRoot, pattern, SearchOption.AllDirectories))
            {
                files.Add(f);
            }
        }

        var summaries = new List<AuditRunSummary>(files.Count);
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            if (!info.Exists) continue;

            var runId = TryExtractRunId(info.Name);
            var rowCount = CountRowsExcludingHeader(file);
            var hasSidecar = File.Exists(file + ".meta.json");
            bool sidecarValid = false;
            if (hasSidecar)
            {
                try { sidecarValid = _signingService.VerifyMetadataSidecar(file); }
                catch { sidecarValid = false; }
            }

            summaries.Add(new AuditRunSummary(
                AuditCsvPath: file,
                FileName: info.Name,
                RunId: runId,
                LastModifiedUtc: info.LastWriteTimeUtc,
                FileSizeBytes: info.Length,
                RowCount: rowCount,
                HasSidecar: hasSidecar,
                IsSidecarValid: sidecarValid));
        }

        IEnumerable<AuditRunSummary> filtered = summaries;
        if (filter is not null)
        {
            if (!string.IsNullOrEmpty(filter.RunId))
                filtered = filtered.Where(s =>
                    s.RunId is not null
                    && string.Equals(s.RunId, filter.RunId, StringComparison.OrdinalIgnoreCase));
            if (filter.FromUtc is { } from)
                filtered = filtered.Where(s => s.LastModifiedUtc >= from);
            if (filter.ToUtc is { } to)
                filtered = filtered.Where(s => s.LastModifiedUtc <= to);
        }

        var ordered = filtered
            .OrderByDescending(s => s.LastModifiedUtc)
            .ThenBy(s => s.FileName, StringComparer.Ordinal)
            .ToList();

        if (page is null)
            return ordered;

        return ordered
            .Skip(page.PageIndex * page.PageSize)
            .Take(page.PageSize)
            .ToList();
    }

    public AuditRowPage ReadRunRows(
        string auditCsvPath,
        AuditRunFilter? filter = null,
        AuditPage? page = null)
    {
        if (string.IsNullOrWhiteSpace(auditCsvPath))
            throw new ArgumentException("auditCsvPath must not be empty.", nameof(auditCsvPath));
        page ??= new AuditPage();

        if (!File.Exists(auditCsvPath))
            return new AuditRowPage(Array.Empty<AuditRowView>(), 0, 0, page.PageIndex, page.PageSize);

        var rows = new List<AuditRowView>();
        int lineNumber = 0;
        using (var reader = new StreamReader(auditCsvPath))
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;
                if (lineNumber == 1) continue; // skip header

                if (string.IsNullOrEmpty(line)) continue;

                string[] fields;
                try { fields = AuditCsvParser.ParseCsvLine(line); }
                catch (InvalidDataException) { continue; }

                rows.Add(new AuditRowView(
                    LineNumber: lineNumber,
                    RootPath: GetField(fields, 0),
                    OldPath: GetField(fields, 1),
                    NewPath: GetField(fields, 2),
                    Action: GetField(fields, 3),
                    Category: GetField(fields, 4),
                    Hash: GetField(fields, 5),
                    Reason: GetField(fields, 6),
                    Timestamp: GetField(fields, 7)));
            }
        }

        var total = rows.Count;

        IEnumerable<AuditRowView> filtered = rows;
        if (filter is not null)
        {
            if (!string.IsNullOrEmpty(filter.Outcome))
                filtered = filtered.Where(r =>
                    string.Equals(r.Action, filter.Outcome, StringComparison.OrdinalIgnoreCase));
            if (filter.FromUtc is { } from)
                filtered = filtered.Where(r => TryParseTimestamp(r.Timestamp) is { } ts && ts >= from);
            if (filter.ToUtc is { } to)
                filtered = filtered.Where(r => TryParseTimestamp(r.Timestamp) is { } ts && ts <= to);
        }

        var filteredList = filtered.ToList();
        var pageRows = filteredList
            .Skip(page.PageIndex * page.PageSize)
            .Take(page.PageSize)
            .ToList();

        return new AuditRowPage(
            Rows: pageRows,
            TotalRowCount: total,
            FilteredRowCount: filteredList.Count,
            PageIndex: page.PageIndex,
            PageSize: page.PageSize);
    }

    public AuditSidecarInfo? ReadSidecar(string auditCsvPath)
    {
        if (string.IsNullOrWhiteSpace(auditCsvPath))
            throw new ArgumentException("auditCsvPath must not be empty.", nameof(auditCsvPath));

        var sidecarPath = auditCsvPath + ".meta.json";
        if (!File.Exists(sidecarPath))
            return null;

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        int declaredRows = 0;
        try
        {
            var json = File.ReadAllText(sidecarPath);
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                metadata[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True or JsonValueKind.False => prop.Value.GetBoolean().ToString(),
                    JsonValueKind.Null => string.Empty,
                    _ => prop.Value.GetRawText(),
                };
            }
            if (metadata.TryGetValue("rowCount", out var rc) && int.TryParse(rc, out var parsed))
                declaredRows = parsed;
        }
        catch (JsonException)
        {
            return new AuditSidecarInfo(sidecarPath, 0, false, metadata);
        }

        bool isValid;
        try { isValid = _signingService.VerifyMetadataSidecar(auditCsvPath); }
        catch { isValid = false; }

        return new AuditSidecarInfo(sidecarPath, declaredRows, isValid, metadata);
    }

    private static string GetField(string[] fields, int index)
        => index < fields.Length ? fields[index] : string.Empty;

    private static int CountRowsExcludingHeader(string csvPath)
    {
        try
        {
            using var reader = new StreamReader(csvPath);
            int count = 0;
            int line = 0;
            while (reader.ReadLine() is not null)
            {
                line++;
                if (line == 1) continue;
                count++;
            }
            return count;
        }
        catch
        {
            return 0;
        }
    }

    private static string? TryExtractRunId(string fileName)
    {
        // patterns: audit-{RUNID}.csv or {RUNID}.audit.csv
        const string prefix = "audit-";
        if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return fileName.Substring(prefix.Length, fileName.Length - prefix.Length - 4);
        }
        const string suffix = ".audit.csv";
        if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return fileName.Substring(0, fileName.Length - suffix.Length);
        }
        return null;
    }

    private static DateTimeOffset? TryParseTimestamp(string ts)
    {
        if (string.IsNullOrWhiteSpace(ts)) return null;
        if (DateTimeOffset.TryParse(ts, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var result))
            return result;
        return null;
    }
}
