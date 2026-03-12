using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Orchestration;
using RomCleanup.Infrastructure.Tools;
using RomCleanup.Infrastructure.Reporting;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>
/// Backend logic for all WPF feature buttons.
/// Port of PowerShell modules: ConversionEstimate, JunkReport, DuplicateHeatmap,
/// CompletenessTracker, HeaderAnalysis, CollectionCsvExport, FilterBuilder, etc.
/// </summary>
public static class FeatureService
{
    /// <summary>
    /// Safely load an XDocument with XXE/DTD processing disabled.
    /// </summary>
    private static XDocument SafeLoadXDocument(string path)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader);
    }

    // ═══ CONVERSION ESTIMATE ════════════════════════════════════════════
    // Port of ConversionEstimate.ps1

    private static readonly Dictionary<string, double> CompressionRatios = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bin_chd"] = 0.50, ["cue_chd"] = 0.50, ["iso_chd"] = 0.60,
        ["iso_rvz"] = 0.40, ["gcz_rvz"] = 0.70, ["zip_7z"] = 0.90,
        ["rar_7z"] = 0.95, ["cso_chd"] = 0.80, ["pbp_chd"] = 0.70,
        ["iso_cso"] = 0.65, ["wbfs_rvz"] = 0.45, ["nkit_rvz"] = 0.50
    };

    public static ConversionEstimateResult GetConversionEstimate(IReadOnlyList<RomCandidate> candidates)
    {
        long totalSource = 0, totalEstimated = 0;
        var details = new List<ConversionDetail>();

        foreach (var c in candidates)
        {
            var ext = c.Extension.TrimStart('.').ToLowerInvariant();
            var target = GetTargetFormat(ext);
            if (target is null) continue;

            var key = $"{ext}_{target}";
            var ratio = CompressionRatios.GetValueOrDefault(key, 0.75);
            var estimated = (long)(c.SizeBytes * ratio);
            totalSource += c.SizeBytes;
            totalEstimated += estimated;
            details.Add(new ConversionDetail(Path.GetFileName(c.MainPath), ext, target, c.SizeBytes, estimated));
        }

        return new ConversionEstimateResult(totalSource, totalEstimated, totalSource - totalEstimated,
            totalSource > 0 ? (double)totalEstimated / totalSource : 1.0, details);
    }

    private static string? GetTargetFormat(string ext) => ext switch
    {
        "bin" or "cue" or "iso" or "cso" or "pbp" => "chd",
        "gcz" or "wbfs" or "nkit" => "rvz",
        "zip" => "7z",
        "rar" => "7z",
        _ => null
    };

    // ═══ HEALTH SCORE ═══════════════════════════════════════════════════
    // Port of WpfSlice.ReportPreview.ps1

    public static int CalculateHealthScore(int totalFiles, int dupes, int junk, int verified)
    {
        if (totalFiles <= 0) return 0;
        var dupePct = 100.0 * dupes / totalFiles;
        var junkPct = 100.0 * junk / totalFiles;
        var verifiedBonus = verified > 0 ? 10.0 * verified / totalFiles : 0;
        return (int)Math.Clamp(100 - Math.Min(60, dupePct) - Math.Min(30, junkPct) + verifiedBonus, 0, 100);
    }

    // ═══ DUPLICATE HEATMAP ══════════════════════════════════════════════
    // Port of DuplicateHeatmap.ps1

    public static List<HeatmapEntry> GetDuplicateHeatmap(IReadOnlyList<DedupeResult> groups)
    {
        var consoleMap = new Dictionary<string, (int total, int dupes)>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
        {
            var console = DetectConsoleFromPath(g.Winner.MainPath);
            if (!consoleMap.TryGetValue(console, out var val))
                val = (0, 0);
            val.total += 1 + g.Losers.Count;
            val.dupes += g.Losers.Count;
            consoleMap[console] = val;
        }

        return consoleMap
            .Select(kv => new HeatmapEntry(kv.Key, kv.Value.total, kv.Value.dupes,
                kv.Value.total > 0 ? 100.0 * kv.Value.dupes / kv.Value.total : 0))
            .OrderByDescending(h => h.Duplicates)
            .ToList();
    }

    private static string DetectConsoleFromPath(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        // Typically: root/ConsoleName/game.ext → take second-to-last dir
        return parts.Length >= 2 ? parts[^2] : "Unbekannt";
    }

    // ═══ DUPLICATE INSPECTOR ════════════════════════════════════════════
    // Port of WpfSlice.ReportPreview.ps1 - btnDuplicateInspector

    public static List<DuplicateSourceEntry> GetDuplicateInspector(string? auditPath)
    {
        if (string.IsNullOrEmpty(auditPath) || !File.Exists(auditPath))
            return [];

        var dirCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(auditPath, Encoding.UTF8).Skip(1)) // skip header
        {
            var fields = ParseCsvLine(line);
            if (fields.Length < 5) continue;
            var action = fields[3];
            if (!action.Equals("MOVE", StringComparison.OrdinalIgnoreCase) &&
                !action.Equals("SKIP_DRYRUN", StringComparison.OrdinalIgnoreCase))
                continue;
            var dir = Path.GetDirectoryName(fields[1]) ?? "";
            dirCounts[dir] = dirCounts.GetValueOrDefault(dir) + 1;
        }

        return dirCounts
            .OrderByDescending(kv => kv.Value)
            .Take(8)
            .Select(kv => new DuplicateSourceEntry(kv.Key, kv.Value))
            .ToList();
    }

    // ═══ JUNK REPORT ════════════════════════════════════════════════════
    // Port of JunkReport.ps1

    private static readonly (string pattern, string tag, string reason)[] JunkPatterns =
    [
        (@"\(Beta[^)]*\)", "Beta", "Beta-Version"),
        (@"\(Proto[^)]*\)", "Proto", "Prototyp"),
        (@"\(Demo[^)]*\)", "Demo", "Demo-Version"),
        (@"\(Sample\)", "Sample", "Sample"),
        (@"\(Homebrew\)", "Homebrew", "Homebrew"),
        (@"\(Hack\)", "Hack", "ROM-Hack"),
        (@"\(Unl\)", "Unlicensed", "Unlizenziert"),
        (@"\(Aftermarket\)", "Aftermarket", "Aftermarket"),
        (@"\(Pirate\)", "Pirate", "Pirate"),
        (@"\(Program\)", "Program", "Programm/Utility"),
        (@"\[b\d*\]", "[b]", "Bad Dump"),
        (@"\[h\d*\]", "[h]", "Hack-Tag"),
        (@"\[o\d*\]", "[o]", "Overdump"),
        (@"\[t\d*\]", "[t]", "Trainer"),
        (@"\[f\d*\]", "[f]", "Fixed"),
        (@"\[T[\+\-]", "[T]", "Translation")
    ];

    private static readonly (string pattern, string tag, string reason)[] AggressivePatterns =
    [
        (@"\(Alt[^)]*\)", "Alt", "Alternative Version"),
        (@"\(Bonus Disc\)", "Bonus", "Bonus Disc"),
        (@"\(Reprint\)", "Reprint", "Nachdruck"),
        (@"\(Virtual Console\)", "VC", "Virtual Console")
    ];

    public static JunkReportEntry? GetJunkReason(string baseName, bool aggressive)
    {
        foreach (var (pattern, tag, reason) in JunkPatterns)
        {
            if (Regex.IsMatch(baseName, pattern, RegexOptions.IgnoreCase))
                return new JunkReportEntry(tag, reason, "standard");
        }

        if (aggressive)
        {
            foreach (var (pattern, tag, reason) in AggressivePatterns)
            {
                if (Regex.IsMatch(baseName, pattern, RegexOptions.IgnoreCase))
                    return new JunkReportEntry(tag, reason, "aggressive");
            }
        }

        return null;
    }

    public static string BuildJunkReport(IReadOnlyList<RomCandidate> candidates, bool aggressive)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Junk-Klassifizierungsbericht");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine();

        var junkItems = new List<(string file, JunkReportEntry reason)>();
        foreach (var c in candidates.Where(c => c.Category == "JUNK"))
        {
            var name = Path.GetFileNameWithoutExtension(c.MainPath);
            var reason = GetJunkReason(name, aggressive) ?? new JunkReportEntry("JUNK", "Klassifiziert als Junk", "core");
            junkItems.Add((Path.GetFileName(c.MainPath), reason));
        }

        var byTag = junkItems.GroupBy(j => j.reason.Tag).OrderByDescending(g => g.Count());
        foreach (var group in byTag)
        {
            sb.AppendLine($"── {group.Key} ({group.Count()} Dateien) ──");
            sb.AppendLine($"   Grund: {group.First().reason.Reason} [{group.First().reason.Level}]");
            foreach (var item in group.Take(10))
                sb.AppendLine($"   • {item.file}");
            if (group.Count() > 10)
                sb.AppendLine($"   … und {group.Count() - 10} weitere");
            sb.AppendLine();
        }

        sb.AppendLine($"Gesamt: {junkItems.Count} Junk-Dateien");
        return sb.ToString();
    }

    // ═══ ROM FILTER ═════════════════════════════════════════════════════
    // Port of RomFilter.ps1

    public static List<RomCandidate> SearchRomCollection(IReadOnlyList<RomCandidate> candidates, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText)) return candidates.ToList();
        return candidates.Where(c =>
            c.MainPath.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            c.GameKey.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            c.Region.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            c.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            c.Extension.Contains(searchText, StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    // ═══ COLLECTION CSV EXPORT ══════════════════════════════════════════
    // Port of CollectionCsvExport.ps1

    public static string ExportCollectionCsv(IReadOnlyList<RomCandidate> candidates, char delimiter = ';')
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Dateiname{delimiter}Konsole{delimiter}Region{delimiter}Format{delimiter}Groesse_MB{delimiter}Kategorie{delimiter}DAT_Status{delimiter}Pfad");
        foreach (var c in candidates)
        {
            sb.Append(SanitizeCsvField(Path.GetFileName(c.MainPath)));
            sb.Append(delimiter);
            sb.Append(SanitizeCsvField(DetectConsoleFromPath(c.MainPath)));
            sb.Append(delimiter);
            sb.Append(SanitizeCsvField(c.Region));
            sb.Append(delimiter);
            sb.Append(SanitizeCsvField(c.Extension));
            sb.Append(delimiter);
            sb.Append((c.SizeBytes / 1048576.0).ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(delimiter);
            sb.Append(SanitizeCsvField(c.Category));
            sb.Append(delimiter);
            sb.Append(c.DatMatch ? "Verified" : "Unverified");
            sb.Append(delimiter);
            sb.AppendLine(SanitizeCsvField(c.MainPath));
        }
        return sb.ToString();
    }

    private static string SanitizeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        // CSV injection protection (OWASP)
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        if (value.Contains('"') || value.Contains(';') || value.Contains(','))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    // ═══ EXCEL XML EXPORT ═══════════════════════════════════════════════
    // Port of WpfSlice.ReportPreview.ps1 - Export-WpfSummaryData -Format ExcelXml

    public static string ExportExcelXml(IReadOnlyList<RomCandidate> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine("<Worksheet ss:Name=\"ROMs\"><Table>");

        // Header
        sb.AppendLine("<Row>");
        foreach (var h in new[] { "Dateiname", "Konsole", "Region", "Format", "Groesse_MB", "Kategorie", "DAT", "Pfad" })
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{SecurityElement.Escape(h)}</Data></Cell>");
        sb.AppendLine("</Row>");

        // Data
        foreach (var c in candidates)
        {
            sb.AppendLine("<Row>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{SecurityElement.Escape(Path.GetFileName(c.MainPath))}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{SecurityElement.Escape(DetectConsoleFromPath(c.MainPath))}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{SecurityElement.Escape(c.Region)}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{SecurityElement.Escape(c.Extension)}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"Number\">{(c.SizeBytes / 1048576.0).ToString("F2", CultureInfo.InvariantCulture)}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{SecurityElement.Escape(c.Category)}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{(c.DatMatch ? "Verified" : "Unverified")}</Data></Cell>");
            sb.AppendLine($"<Cell><Data ss:Type=\"String\">{SecurityElement.Escape(c.MainPath)}</Data></Cell>");
            sb.AppendLine("</Row>");
        }

        sb.AppendLine("</Table></Worksheet></Workbook>");
        return sb.ToString();
    }

    // ═══ DRY RUN COMPARE ════════════════════════════════════════════════
    // Port of DryRunCompare.ps1

    public static DryRunCompareResult CompareDryRuns(IReadOnlyList<ReportEntry> a, IReadOnlyList<ReportEntry> b)
    {
        // Use first-wins to avoid ArgumentException on duplicate FilePath entries
        var indexA = new Dictionary<string, ReportEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in a)
            indexA.TryAdd(e.FilePath, e);
        var indexB = new Dictionary<string, ReportEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in b)
            indexB.TryAdd(e.FilePath, e);

        var onlyInA = a.Where(e => !indexB.ContainsKey(e.FilePath)).ToList();
        var onlyInB = b.Where(e => !indexA.ContainsKey(e.FilePath)).ToList();
        var different = new List<(ReportEntry left, ReportEntry right)>();
        var identical = 0;

        foreach (var entry in a)
        {
            if (indexB.TryGetValue(entry.FilePath, out var other))
            {
                if (entry.Action != other.Action || entry.Category != other.Category)
                    different.Add((entry, other));
                else
                    identical++;
            }
        }

        return new DryRunCompareResult(onlyInA, onlyInB, different, identical);
    }

    // ═══ CONVERSION VERIFY ══════════════════════════════════════════════
    // Port of ConversionVerify.ps1

    public static (int passed, int failed, int missing) VerifyConversions(IReadOnlyList<string> targetPaths, long minSize = 1)
    {
        int passed = 0, failed = 0, missing = 0;
        foreach (var path in targetPaths)
        {
            if (!File.Exists(path)) { missing++; continue; }
            var fi = new FileInfo(path);
            if (fi.Length >= minSize) passed++;
            else failed++;
        }
        return (passed, failed, missing);
    }

    // ═══ HEADER ANALYSIS ════════════════════════════════════════════════
    // Port of HeaderAnalysis.ps1

    public static RomHeaderInfo? AnalyzeHeader(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            using var fs = File.OpenRead(filePath);
            var header = new byte[Math.Min(65536, fs.Length)];
            _ = fs.Read(header, 0, header.Length);

            // NES (iNES): 4E 45 53 1A
            if (header.Length >= 16 && header[0] == 0x4E && header[1] == 0x45 &&
                header[2] == 0x53 && header[3] == 0x1A)
            {
                var isNes2 = (header[7] & 0x0C) == 0x08;
                return new RomHeaderInfo("NES", isNes2 ? "NES 2.0" : "iNES",
                    $"PRG={header[4] * 16}KB, CHR={header[5] * 8}KB, Mapper={(header[6] >> 4) | (header[7] & 0xF0)}");
            }

            // N64 Big-Endian: 80 37
            if (header.Length >= 0x40 && header[0] == 0x80 && header[1] == 0x37)
            {
                var title = Encoding.ASCII.GetString(header, 0x20, 20).TrimEnd('\0', ' ');
                return new RomHeaderInfo("N64", "Big-Endian (.z64)", $"Title={title}");
            }

            // N64 Byte-Swap: 37 80
            if (header.Length >= 0x40 && header[0] == 0x37 && header[1] == 0x80)
                return new RomHeaderInfo("N64", "Byte-Swapped (.v64)", "");

            // N64 Little-Endian: 40 12
            if (header.Length >= 0x40 && header[0] == 0x40 && header[1] == 0x12)
                return new RomHeaderInfo("N64", "Little-Endian (.n64)", "");

            // GBA: 0x96 at offset 0xB2
            if (header.Length >= 0xBE && header[0xB2] == 0x96)
            {
                var title = Encoding.ASCII.GetString(header, 0xA0, 12).TrimEnd('\0', ' ');
                var code = Encoding.ASCII.GetString(header, 0xAC, 4).TrimEnd('\0');
                return new RomHeaderInfo("GBA", "GBA ROM", $"Title={title}, Code={code}");
            }

            // SNES LoROM (header at 0x7FC0)
            if (header.Length >= 0x8000)
            {
                var snesTitle = Encoding.ASCII.GetString(header, 0x7FC0, 21).TrimEnd('\0', ' ');
                // Validate SNES checksum complement: checksum + complement must equal 0xFFFF
                int checksum = header[0x7FDE] | (header[0x7FDF] << 8);
                int complement = header[0x7FDC] | (header[0x7FDD] << 8);
                if (snesTitle.Length > 0 && snesTitle.All(c => c >= 0x20 && c <= 0x7E) &&
                    (checksum + complement) == 0xFFFF)
                    return new RomHeaderInfo("SNES", "LoROM", $"Title={snesTitle}");
            }

            // SNES HiROM (header at 0xFFC0)
            if (header.Length >= 0x10000)
            {
                var snesTitle = Encoding.ASCII.GetString(header, 0xFFC0, 21).TrimEnd('\0', ' ');
                int checksum = header[0xFFDE] | (header[0xFFDF] << 8);
                int complement = header[0xFFDC] | (header[0xFFDD] << 8);
                if (snesTitle.Length > 0 && snesTitle.All(c => c >= 0x20 && c <= 0x7E) &&
                    (checksum + complement) == 0xFFFF)
                    return new RomHeaderInfo("SNES", "HiROM", $"Title={snesTitle}");
            }

            return new RomHeaderInfo("Unbekannt", "Unbekanntes Format", $"Magic: {header[0]:X2} {header[1]:X2} {header[2]:X2} {header[3]:X2}");
        }
        catch
        {
            return null;
        }
    }

    // ═══ TREND ANALYSIS ═════════════════════════════════════════════════
    // Port of TrendAnalysis.ps1

    private static readonly string TrendFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RomCleanupRegionDedupe", "trend-history.json");

    public static void SaveTrendSnapshot(int totalFiles, long sizeBytes, int verified, int dupes, int junk)
    {
        var history = LoadTrendHistory();
        history.Add(new TrendSnapshot(DateTime.Now, totalFiles, sizeBytes, verified, dupes, junk,
            totalFiles > 0 ? (int)(100.0 * verified / totalFiles) : 0));
        if (history.Count > 365) history.RemoveRange(0, history.Count - 365);
        Directory.CreateDirectory(Path.GetDirectoryName(TrendFile)!);
        File.WriteAllText(TrendFile, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static List<TrendSnapshot> LoadTrendHistory()
    {
        if (!File.Exists(TrendFile)) return [];
        try { return JsonSerializer.Deserialize<List<TrendSnapshot>>(File.ReadAllText(TrendFile)) ?? []; }
        catch { return []; }
    }

    public static string FormatTrendReport(List<TrendSnapshot> history)
    {
        if (history.Count == 0) return "Keine Trend-Daten vorhanden.";
        var sb = new StringBuilder();
        sb.AppendLine("Trend-Analyse");
        sb.AppendLine(new string('═', 50));
        var latest = history[^1];
        sb.AppendLine($"Aktuell: {latest.TotalFiles} Dateien, {FormatSize(latest.SizeBytes)}, Quality={latest.QualityScore}%");

        if (history.Count >= 2)
        {
            var prev = history[^2];
            var fileDelta = latest.TotalFiles - prev.TotalFiles;
            var dupeDelta = latest.Dupes - prev.Dupes;
            sb.AppendLine($"Δ Dateien: {fileDelta:+#;-#;0}, Δ Duplikate: {dupeDelta:+#;-#;0}");
        }

        sb.AppendLine();
        sb.AppendLine("Verlauf (letzte 10):");
        foreach (var s in history.TakeLast(10))
            sb.AppendLine($"  {s.Timestamp:yyyy-MM-dd HH:mm} | {s.TotalFiles} Dateien | Q={s.QualityScore}%");
        return sb.ToString();
    }

    // ═══ INTEGRITY MONITOR ══════════════════════════════════════════════
    // Port of IntegrityMonitor.ps1
    // NOTE: Baseline uses absolute paths. The baseline is not portable across machines.
    //       A future version could store paths relative to ROM roots.

    private static readonly string BaselinePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RomCleanupRegionDedupe", "integrity-baseline.json");

    public static async Task<Dictionary<string, IntegrityEntry>> CreateBaseline(
        IReadOnlyList<string> filePaths, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var baseline = new System.Collections.Concurrent.ConcurrentDictionary<string, IntegrityEntry>(StringComparer.OrdinalIgnoreCase);
        int completed = 0;
        var total = filePaths.Count;

        await Parallel.ForEachAsync(filePaths, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
            async (path, token) =>
            {
                if (!File.Exists(path)) return;
                var fi = new FileInfo(path);
                var count = Interlocked.Increment(ref completed);
                progress?.Report($"Baseline: {count}/{total} – {Path.GetFileName(path)}");
                var hash = await Task.Run(() => ComputeSha256(path), token);
                baseline[path] = new IntegrityEntry(hash, fi.Length, fi.LastWriteTimeUtc);
            });

        var result = new Dictionary<string, IntegrityEntry>(baseline, StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
        File.WriteAllText(BaselinePath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return result;
    }

    public static async Task<IntegrityCheckResult> CheckIntegrity(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(BaselinePath))
            return new IntegrityCheckResult([], [], [], false, "Keine Baseline vorhanden. Erstellen Sie zuerst eine Baseline über 'Integrity-Baseline speichern'.");

        var baseline = JsonSerializer.Deserialize<Dictionary<string, IntegrityEntry>>(File.ReadAllText(BaselinePath))
            ?? [];

        var changed = new List<string>();
        var missing = new List<string>();
        var intact = new List<string>();
        int i = 0;

        foreach (var (path, entry) in baseline)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Prüfe: {++i}/{baseline.Count} – {Path.GetFileName(path)}");
            if (!File.Exists(path)) { missing.Add(path); continue; }
            var hash = await Task.Run(() => ComputeSha256(path), ct);
            if (hash != entry.Hash) changed.Add(path);
            else intact.Add(path);
        }

        return new IntegrityCheckResult(changed, missing, intact, changed.Count > 0);
    }

    private static string ComputeSha256(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(fs));
    }

    // ═══ BACKUP MANAGER ═════════════════════════════════════════════════
    // Port of BackupManager.ps1

    public static string CreateBackup(IReadOnlyList<string> filePaths, string backupRoot, string label)
    {
        var sessionDir = Path.Combine(backupRoot, $"{DateTime.Now:yyyyMMdd-HHmmss}_{label}");
        Directory.CreateDirectory(sessionDir);

        // Find common root to preserve relative directory structure
        var commonRoot = FindCommonRoot(filePaths);

        foreach (var path in filePaths)
        {
            if (!File.Exists(path)) continue;
            var relativePath = commonRoot is not null
                ? Path.GetRelativePath(commonRoot, path)
                : Path.GetFileName(path);
            var dest = Path.Combine(sessionDir, relativePath);
            var destDir = Path.GetDirectoryName(dest);
            if (destDir is not null) Directory.CreateDirectory(destDir);
            File.Copy(path, dest, overwrite: false);
        }
        return sessionDir;
    }

    private static string? FindCommonRoot(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return null;
        var dirs = paths.Select(p => Path.GetDirectoryName(Path.GetFullPath(p)) ?? "").ToList();
        if (dirs.Count == 0) return null;
        var common = dirs[0];
        foreach (var dir in dirs.Skip(1))
        {
            while (!dir.StartsWith(common + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(dir, common, StringComparison.OrdinalIgnoreCase))
            {
                common = Path.GetDirectoryName(common) ?? "";
                if (common.Length == 0) return null;
            }
        }
        return common;
    }

    public static int CleanupOldBackups(string backupRoot, int retentionDays, Func<int, bool>? confirmDelete = null)
    {
        if (!Directory.Exists(backupRoot)) return 0;
        var cutoff = DateTime.Now.AddDays(-retentionDays);
        var expired = Directory.GetDirectories(backupRoot)
            .Where(dir => Directory.GetCreationTime(dir) < cutoff)
            .ToList();
        if (expired.Count == 0) return 0;
        if (confirmDelete is not null && !confirmDelete(expired.Count)) return 0;
        int removed = 0;
        foreach (var dir in expired)
        {
            Directory.Delete(dir, recursive: true);
            removed++;
        }
        return removed;
    }

    // ═══ FORMAT PRIORITY ════════════════════════════════════════════════
    // Port of FormatPriority.ps1

    private static readonly Dictionary<string, string[]> ConsoleFormatPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ps1"] = ["chd", "bin/cue", "pbp", "cso", "iso"],
        ["ps2"] = ["chd", "iso"],
        ["psp"] = ["chd", "pbp", "cso", "iso"],
        ["dreamcast"] = ["chd", "gdi", "cdi"],
        ["saturn"] = ["chd", "bin/cue", "iso"],
        ["gc"] = ["rvz", "iso", "nkit", "gcz"],
        ["wii"] = ["rvz", "iso", "nkit", "wbfs"],
        ["nes"] = ["zip", "7z", "nes"],
        ["snes"] = ["zip", "7z", "sfc", "smc"],
        ["gba"] = ["zip", "7z", "gba"],
        ["n64"] = ["zip", "7z", "z64", "v64", "n64"],
        ["gb"] = ["zip", "7z", "gb"],
        ["gbc"] = ["zip", "7z", "gbc"],
        ["nds"] = ["zip", "7z", "nds"],
        ["3ds"] = ["zip", "7z", "3ds"],
        ["genesis"] = ["zip", "7z", "md", "gen"],
        ["arcade"] = ["zip", "7z"]
    };

    public static string FormatFormatPriority()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Format-Prioritäten nach Konsole");
        sb.AppendLine(new string('═', 50));
        foreach (var (console, formats) in ConsoleFormatPriority.OrderBy(kv => kv.Key))
        {
            sb.AppendLine($"  {console,-15} → {string.Join(" > ", formats)}");
        }
        return sb.ToString();
    }

    // ═══ SORT TEMPLATES ═════════════════════════════════════════════════
    // Port of SortTemplates.ps1

    public static Dictionary<string, string> GetSortTemplates() => new()
    {
        ["RetroArch"] = "{console}/{filename}",
        ["EmulationStation"] = "roms/{console_lower}/{filename}",
        ["LaunchBox"] = "Games/{console}/{filename}",
        ["Batocera"] = "share/roms/{console_lower}/{filename}",
        ["Flat"] = "{filename}"
    };

    // ═══ EMULATOR COMPAT ════════════════════════════════════════════════
    // Port of EmulatorCompatReport.ps1

    private static readonly Dictionary<string, Dictionary<string, string>> EmulatorMatrix = new()
    {
        ["nes"] = new() { ["Mesen"] = "Perfect", ["FCEUX"] = "Great", ["Nestopia"] = "Great" },
        ["snes"] = new() { ["bsnes"] = "Perfect", ["Snes9x"] = "Great", ["ZSNES"] = "Good" },
        ["n64"] = new() { ["Mupen64Plus"] = "Great", ["Project64"] = "Great", ["Ares"] = "Good" },
        ["gba"] = new() { ["mGBA"] = "Perfect", ["VBA-M"] = "Great" },
        ["gb"] = new() { ["SameBoy"] = "Perfect", ["Gambatte"] = "Perfect", ["mGBA"] = "Great" },
        ["ps1"] = new() { ["DuckStation"] = "Perfect", ["Mednafen"] = "Perfect", ["PCSX-R"] = "Great" },
        ["ps2"] = new() { ["PCSX2"] = "Great", ["AetherSX2"] = "Good" },
        ["psp"] = new() { ["PPSSPP"] = "Great" },
        ["gc"] = new() { ["Dolphin"] = "Great" },
        ["wii"] = new() { ["Dolphin"] = "Great" },
        ["dreamcast"] = new() { ["Flycast"] = "Great", ["Redream"] = "Great" },
        ["saturn"] = new() { ["Mednafen"] = "Great", ["Kronos"] = "Good" },
        ["genesis"] = new() { ["BlastEm"] = "Perfect", ["Genesis Plus GX"] = "Perfect" },
        ["arcade"] = new() { ["MAME"] = "Great", ["FBNeo"] = "Great" }
    };

    public static string FormatEmulatorCompat()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Emulator-Kompatibilitätsmatrix");
        sb.AppendLine(new string('═', 60));
        foreach (var (console, emus) in EmulatorMatrix.OrderBy(kv => kv.Key))
        {
            sb.AppendLine($"\n  {console.ToUpperInvariant()}:");
            foreach (var (emu, compat) in emus)
                sb.AppendLine($"    {emu,-20} {compat}");
        }
        return sb.ToString();
    }

    // ═══ CONFIG DIFF ════════════════════════════════════════════════════
    // Port of ConfigMerge.ps1

    public static List<ConfigDiffEntry> GetConfigDiff(Dictionary<string, string> current, Dictionary<string, string> saved)
    {
        var result = new List<ConfigDiffEntry>();
        var allKeys = current.Keys.Union(saved.Keys).Distinct();
        foreach (var key in allKeys)
        {
            current.TryGetValue(key, out var curVal);
            saved.TryGetValue(key, out var savedVal);
            if (curVal != savedVal)
                result.Add(new ConfigDiffEntry(key, savedVal ?? "(fehlt)", curVal ?? "(fehlt)"));
        }
        return result;
    }

    // ═══ LOCALIZATION ═══════════════════════════════════════════════════
    // Port of Localization.ps1

    public static Dictionary<string, string> LoadLocale(string locale)
    {
        var dataDir = ResolveDataDirectory("i18n");
        if (dataDir is null)
            return new Dictionary<string, string>();

        var path = Path.Combine(dataDir, $"{locale}.json");
        if (!File.Exists(path))
            path = Path.Combine(dataDir, "de.json");
        if (!File.Exists(path))
            return new Dictionary<string, string>();

        try
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path));
            if (json is null) return new Dictionary<string, string>();
            return json.Where(kv => kv.Value.ValueKind == JsonValueKind.String)
                       .ToDictionary(kv => kv.Key, kv => kv.Value.GetString() ?? "");
        }
        catch { return new Dictionary<string, string>(); }
    }

    /// <summary>Resolve the data/ subdirectory, probing from BaseDirectory upward (max 5 levels).</summary>
    internal static string? ResolveDataDirectory(string? subFolder = null)
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "data"),
            Path.Combine(AppContext.BaseDirectory, "data"),
        };

        // Also probe upward from BaseDirectory (for dev layouts)
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 5 && dir is not null; i++)
        {
            var probe = Path.Combine(dir, "data");
            if (Directory.Exists(probe))
                return subFolder is not null ? Path.Combine(probe, subFolder) : probe;
            dir = Path.GetDirectoryName(dir);
        }

        foreach (var c in candidates)
            if (Directory.Exists(c))
                return subFolder is not null ? Path.Combine(c, subFolder) : c;

        return null;
    }

    // ═══ PORTABLE MODE CHECK ════════════════════════════════════════════
    // Port of PortableMode.ps1
    // NOTE: The marker file ".portable" is checked relative to AppContext.BaseDirectory,
    // which is the directory containing the executable (e.g. bin/Debug/net10.0-windows/).
    // Place ".portable" next to the .exe, NOT the workspace root.

    public static bool IsPortableMode()
    {
        var marker = Path.Combine(AppContext.BaseDirectory, ".portable");
        return File.Exists(marker);
    }

    // ═══ STORAGE TIERING ════════════════════════════════════════════════
    // Port of StorageTiering.ps1

    public static string AnalyzeStorageTiers(IReadOnlyList<RomCandidate> candidates, int hotThresholdDays = 30)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Storage-Tiering-Analyse");
        sb.AppendLine(new string('═', 50));

        long hotSize = 0, coldSize = 0;
        int hotCount = 0, coldCount = 0;
        var now = DateTime.Now;

        foreach (var c in candidates)
        {
            var fi = new FileInfo(c.MainPath);
            if (!fi.Exists) continue;
            var daysSince = (now - fi.LastAccessTime).TotalDays;
            if (daysSince <= hotThresholdDays)
            { hotSize += c.SizeBytes; hotCount++; }
            else
            { coldSize += c.SizeBytes; coldCount++; }
        }

        sb.AppendLine($"  Hot (≤{hotThresholdDays}d): {hotCount} Dateien, {FormatSize(hotSize)}");
        sb.AppendLine($"  Cold (>{hotThresholdDays}d): {coldCount} Dateien, {FormatSize(coldSize)}");
        sb.AppendLine($"\n  Empfehlung: Cold-Dateien auf HDD/NAS verschieben → {FormatSize(coldSize)} SSD-Platz frei");
        return sb.ToString();
    }

    // ═══ HARDLINK ESTIMATE ══════════════════════════════════════════════
    // Port of HardlinkMode.ps1

    public static string GetHardlinkEstimate(IReadOnlyList<DedupeResult> groups)
    {
        long savedBytes = 0;
        int linkCount = 0;
        foreach (var g in groups)
        {
            foreach (var l in g.Losers)
            {
                savedBytes += l.SizeBytes;
                linkCount++;
            }
        }
        return $"Hardlink-Modus: {linkCount} Links möglich, {FormatSize(savedBytes)} Speicher gespart (100% Effizienz bei NTFS)";
    }

    // ═══ NAS OPTIMIZATION ═══════════════════════════════════════════════
    // Port of NasOptimization.ps1

    public static string GetNasInfo(IReadOnlyList<string> roots)
    {
        var sb = new StringBuilder();
        sb.AppendLine("NAS-Optimierung");
        sb.AppendLine(new string('═', 50));
        foreach (var root in roots)
        {
            var isUncPath = root.StartsWith(@"\\") || root.StartsWith("//");
            var isMappedNetworkDrive = false;
            string? uncResolved = null;

            // Detect mapped network drives (e.g. W:\ → \\server\share)
            if (!isUncPath && root.Length >= 2 && root[1] == ':')
            {
                try
                {
                    var driveInfo = new DriveInfo(root[..1]);
                    if (driveInfo.DriveType == DriveType.Network)
                    {
                        isMappedNetworkDrive = true;
                        // Try to resolve UNC path from mapped drive
                        var fullPath = Path.GetFullPath(root);
                        uncResolved = fullPath.StartsWith(@"\\") ? fullPath : null;
                    }
                }
                catch { /* DriveInfo may fail on disconnected drives */ }
            }

            var isNetwork = isUncPath || isMappedNetworkDrive;
            sb.AppendLine($"\n  {root}");
            if (isMappedNetworkDrive)
                sb.AppendLine($"    Typ: Zugeordnetes Netzlaufwerk{(uncResolved != null ? $" → {uncResolved}" : "")}");
            else if (isUncPath)
                sb.AppendLine($"    Typ: UNC-Netzwerkpfad");
            else
                sb.AppendLine($"    Typ: Lokales Laufwerk");
            sb.AppendLine($"    Netzwerk-Pfad: {(isNetwork ? "Ja" : "Nein")}");
            if (isNetwork)
            {
                sb.AppendLine("    Empfehlungen:");
                sb.AppendLine("      • Batch-Größe reduzieren (max 500 Dateien/Batch)");
                sb.AppendLine("      • Hashing-Threads begrenzen (max 2 für SMB)");
                sb.AppendLine("      • Audit/Reports lokal speichern (nicht auf NAS)");
                sb.AppendLine("      • Throttling: Medium (200ms Verzögerung)");
                sb.AppendLine("      • UNC-Pfad statt Laufwerksbuchstabe empfohlen (stabiler)");
            }
            else
            {
                sb.AppendLine("    Empfehlung: Maximale Parallelität möglich");
            }

            // Check accessibility
            try
            {
                if (!Directory.Exists(root))
                    sb.AppendLine("    ⚠ WARNUNG: Pfad nicht erreichbar!");
            }
            catch
            {
                sb.AppendLine("    ⚠ WARNUNG: Zugriffsprüfung fehlgeschlagen!");
            }
        }
        return sb.ToString();
    }

    // ═══ CLONE LIST VIEWER ══════════════════════════════════════════════
    // Port of CloneListViewer.ps1

    public static string BuildCloneTree(IReadOnlyList<DedupeResult> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Parent/Clone-Baum");
        sb.AppendLine(new string('═', 50));
        foreach (var g in groups.Take(50))
        {
            sb.AppendLine($"\n  ► {g.GameKey} (Winner)");
            sb.AppendLine($"    {Path.GetFileName(g.Winner.MainPath)} [{g.Winner.Region}] {g.Winner.Extension}");
            foreach (var l in g.Losers)
                sb.AppendLine($"    └─ {Path.GetFileName(l.MainPath)} [{l.Region}] {l.Extension}");
        }
        if (groups.Count > 50)
            sb.AppendLine($"\n  … und {groups.Count - 50} weitere Gruppen");
        return sb.ToString();
    }

    // ═══ VIRTUAL FOLDER PREVIEW ═════════════════════════════════════════
    // Port of VirtualFolderPreview.ps1

    public static string BuildVirtualFolderPreview(IReadOnlyList<RomCandidate> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Virtuelle Ordner-Vorschau");
        sb.AppendLine(new string('═', 50));

        var byConsole = candidates.GroupBy(c => DetectConsoleFromPath(c.MainPath))
            .OrderBy(g => g.Key);

        foreach (var group in byConsole)
        {
            var total = group.Sum(c => c.SizeBytes);
            sb.AppendLine($"\n  📁 {group.Key} ({group.Count()} Dateien, {FormatSize(total)})");
            var byRegion = group.GroupBy(c => c.Region).OrderByDescending(g => g.Count());
            foreach (var rg in byRegion.Take(5))
                sb.AppendLine($"      [{rg.Key}] {rg.Count()} Dateien");
        }
        return sb.ToString();
    }

    // ═══ SPLIT PANEL PREVIEW ════════════════════════════════════════════
    // Port of SplitPanelPreview.ps1

    public static string BuildSplitPanelPreview(IReadOnlyList<DedupeResult> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Split-Panel (Norton Commander Style)");
        sb.AppendLine(new string('═', 60));
        sb.AppendLine($"{"KEEP (Quelle)",-30} │ {"MOVE (Ziel)",-30}");
        sb.AppendLine(new string('─', 30) + "─┼─" + new string('─', 30));

        foreach (var g in groups.Take(30))
        {
            var winner = Path.GetFileName(g.Winner.MainPath);
            foreach (var l in g.Losers)
            {
                var loser = Path.GetFileName(l.MainPath);
                sb.AppendLine($"{Truncate(winner, 30),-30} │ {Truncate(loser, 30),-30}");
            }
        }
        if (groups.Count > 30)
            sb.AppendLine($"\n  … und {groups.Count - 30} weitere Gruppen");
        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : string.Concat(s.AsSpan(0, max - 3), "...");

    // ═══ COMMAND PALETTE ════════════════════════════════════════════════
    // Port of CommandPalette.ps1

    public static readonly (string key, string name, string shortcut)[] PaletteCommands =
    [
        ("dryrun", "DryRun starten", "Ctrl+D"),
        ("move", "Move ausführen", "Ctrl+M"),
        ("convert", "Konvertierung starten", "Ctrl+K"),
        ("settings", "Einstellungen öffnen", "Ctrl+,"),
        ("dat-update", "DAT aktualisieren", "Ctrl+U"),
        ("export-csv", "CSV exportieren", "Ctrl+E"),
        ("export-report", "Report öffnen", "Ctrl+R"),
        ("history", "Verlauf anzeigen", "Ctrl+H"),
        ("cancel", "Lauf abbrechen", "Escape"),
        ("help", "Hilfe anzeigen", "F1"),
        ("rollback", "Rollback ausführen", "Ctrl+Z"),
        ("filter", "ROM-Filter", "Ctrl+F"),
        ("theme", "Theme wechseln", "Ctrl+T"),
        ("clear-log", "Log leeren", "Ctrl+L"),
        ("gamekey", "GameKey-Vorschau", "Ctrl+G")
    ];

    public static List<(string key, string name, string shortcut, int score)> SearchCommands(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return PaletteCommands.Select(c => (c.key, c.name, c.shortcut, 0)).ToList();

        // Limit query length to prevent excessive Levenshtein matrix allocation
        var safeQuery = query.Length > 50 ? query[..50] : query;

        var results = new List<(string key, string name, string shortcut, int score)>();
        foreach (var cmd in PaletteCommands)
        {
            // Substring match = best score
            if (cmd.name.Contains(safeQuery, StringComparison.OrdinalIgnoreCase) ||
                cmd.key.Contains(safeQuery, StringComparison.OrdinalIgnoreCase))
            {
                results.Add((cmd.key, cmd.name, cmd.shortcut, 0));
                continue;
            }
            // Levenshtein fuzzy match
            var dist = LevenshteinDistance(safeQuery.ToLowerInvariant(), cmd.key.ToLowerInvariant());
            if (dist <= 3)
                results.Add((cmd.key, cmd.name, cmd.shortcut, dist + 2));
        }
        return results.OrderBy(r => r.score).ToList();
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;
        for (var i = 1; i <= n; i++)
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[n, m];
    }

    // ═══ SCHEDULER ══════════════════════════════════════════════════════
    // Port of SchedulerAdvanced.ps1

    public static bool TestCronMatch(string cronExpression, DateTime dt)
    {
        var fields = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5) return false;

        return CronFieldMatch(fields[0], dt.Minute) &&
               CronFieldMatch(fields[1], dt.Hour) &&
               CronFieldMatch(fields[2], dt.Day) &&
               CronFieldMatch(fields[3], dt.Month) &&
               CronFieldMatch(fields[4], (int)dt.DayOfWeek);
    }

    private static bool CronFieldMatch(string field, int value)
    {
        if (field == "*") return true;
        foreach (var part in field.Split(','))
        {
            if (part.Contains('/'))
            {
                var segments = part.Split('/');
                if (segments.Length == 2 && int.TryParse(segments[1], out var step) && step > 0)
                {
                    // Support range/step syntax like "10-30/5"
                    int lo = 0;
                    if (segments[0].Contains('-'))
                    {
                        var range = segments[0].Split('-');
                        if (int.TryParse(range[0], out var rLo) && int.TryParse(range[1], out var rHi))
                        {
                            if (value >= rLo && value <= rHi && (value - rLo) % step == 0)
                                return true;
                        }
                    }
                    else if (segments[0] == "*" || int.TryParse(segments[0], out lo))
                    {
                        if ((value - lo) % step == 0 && value >= lo)
                            return true;
                    }
                }
            }
            else if (part.Contains('-'))
            {
                var range = part.Split('-');
                if (int.TryParse(range[0], out var lo) && int.TryParse(range[1], out var hi) && value >= lo && value <= hi)
                    return true;
            }
            else if (int.TryParse(part, out var exact) && exact == value)
                return true;
        }
        return false;
    }

    // ═══ LAUNCHER INTEGRATION ═══════════════════════════════════════════
    // Port of LauncherIntegration.ps1

    private static readonly Dictionary<string, string> CoreMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nes"] = "mesen_libretro", ["snes"] = "snes9x_libretro", ["n64"] = "mupen64plus_next_libretro",
        ["gb"] = "gambatte_libretro", ["gbc"] = "gambatte_libretro", ["gba"] = "mgba_libretro",
        ["nds"] = "melonds_libretro", ["ps1"] = "mednafen_psx_hw_libretro", ["ps2"] = "pcsx2_libretro",
        ["psp"] = "ppsspp_libretro", ["gc"] = "dolphin_libretro", ["wii"] = "dolphin_libretro",
        ["genesis"] = "genesis_plus_gx_libretro", ["arcade"] = "fbneo_libretro",
        ["dreamcast"] = "flycast_libretro", ["saturn"] = "mednafen_saturn_libretro"
    };

    public static string ExportRetroArchPlaylist(IReadOnlyList<RomCandidate> winners, string playlistName)
    {
        var entries = new List<object>();
        foreach (var w in winners)
        {
            var console = DetectConsoleFromPath(w.MainPath).ToLowerInvariant();
            var core = CoreMapping.GetValueOrDefault(console, "");
            entries.Add(new
            {
                path = w.MainPath.Replace('\\', '/'),
                label = Path.GetFileNameWithoutExtension(w.MainPath),
                core_path = core,
                core_name = core.Replace("_libretro", ""),
                db_name = playlistName + ".lpl"
            });
        }
        return JsonSerializer.Serialize(new
        {
            version = "1.5",
            default_core_path = "",
            default_core_name = "",
            items = entries
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // ═══ GENRE CLASSIFICATION ═══════════════════════════════════════════
    // Port of GenreClassification.ps1

    private static readonly (string keyword, string genre)[] GenreKeywords =
    [
        ("rpg", "RPG"), ("quest", "RPG"), ("dragon", "RPG"), ("fantasy", "RPG"),
        ("race", "Racing"), ("rally", "Racing"), ("kart", "Racing"), ("speed", "Racing"),
        ("soccer", "Sports"), ("fifa", "Sports"), ("nba", "Sports"), ("tennis", "Sports"),
        ("fight", "Fighting"), ("tekken", "Fighting"), ("mortal", "Fighting"), ("street fighter", "Fighting"),
        ("puzzle", "Puzzle"), ("tetris", "Puzzle"),
        ("mario", "Platformer"), ("sonic", "Platformer"), ("jump", "Platformer"),
        ("shoot", "Shooter"), ("gun", "Shooter"), ("doom", "Shooter"),
        ("strategy", "Strategy"), ("chess", "Strategy"), ("war", "Strategy"),
        ("adventure", "Adventure"), ("zelda", "Adventure"),
        ("simulation", "Simulation"), ("sim", "Simulation"),
        ("pinball", "Arcade"), ("pong", "Arcade")
    ];

    public static string ClassifyGenre(string gameName)
    {
        var lower = gameName.ToLowerInvariant();
        foreach (var (keyword, genre) in GenreKeywords)
        {
            // Use word boundary matching to avoid false positives (e.g. "gun" matching "Gundam")
            if (System.Text.RegularExpressions.Regex.IsMatch(lower, $@"\b{System.Text.RegularExpressions.Regex.Escape(keyword)}\b"))
                return genre;
        }
        return "Other";
    }

    // ═══ PATCH ENGINE ═══════════════════════════════════════════════════
    // Port of PatchEngine.ps1

    public static string? DetectPatchFormat(string patchPath)
    {
        if (!File.Exists(patchPath)) return null;
        using var fs = File.OpenRead(patchPath);
        var magic = new byte[5];
        if (fs.Read(magic, 0, 5) < 5) return null;
        if (magic[0] == 'P' && magic[1] == 'A' && magic[2] == 'T' && magic[3] == 'C' && magic[4] == 'H') return "IPS";
        if (magic[0] == 'B' && magic[1] == 'P' && magic[2] == 'S' && magic[3] == '1') return "BPS";
        if (magic[0] == 'U' && magic[1] == 'P' && magic[2] == 'S' && magic[3] == '1') return "UPS";
        return null;
    }

    // ═══ HELPER ═════════════════════════════════════════════════════════

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):F2} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):F2} KB",
        _ => $"{bytes} B"
    };

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
                { current.Append('"'); i++; }
                else if (ch == '"') inQuotes = false;
                else current.Append(ch);
            }
            else
            {
                if (ch == '"') inQuotes = true;
                else if (ch == ',') { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(ch);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }

    // ═══ DOCKER CONFIG ══════════════════════════════════════════════════
    // Port of DockerContainer.ps1

    public static string GenerateDockerfile()
    {
        return """
            FROM mcr.microsoft.com/dotnet/aspnet:10.0
            LABEL maintainer="RomCleanup" description="ROM Cleanup REST API"
            WORKDIR /app
            COPY publish/ .
            VOLUME ["/data/roms", "/data/config"]
            EXPOSE 5000 5001
            ENV ASPNETCORE_URLS=http://+:5000;https://+:5001
            ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certs/cert.pfx
            ENTRYPOINT ["dotnet", "RomCleanup.Api.dll"]
            """;
    }

    public static string GenerateDockerCompose()
    {
        return """
            # HINWEIS: ROM_CLEANUP_API_KEY NICHT in docker-compose.yml hartcodieren!
            # Verwende eine .env-Datei oder Docker Secrets.
            services:
              romcleanup:
                build: .
                ports:
                  - "5000:5000"
                volumes:
                  - ./roms:/data/roms
                  - ./config:/data/config
                environment:
                  - ROM_CLEANUP_API_KEY=${ROM_CLEANUP_API_KEY}
                restart: unless-stopped
                healthcheck:
                  test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
                  interval: 30s
                  retries: 3
            """;
    }

    // ═══ WINDOWS CONTEXT MENU ═══════════════════════════════════════════
    // Port of WindowsContextMenu.ps1

    public static string GetContextMenuRegistryScript()
    {
        var exePath = Environment.ProcessPath ?? "RomCleanup.CLI.exe";
        // .reg format requires backslashes doubled and paths with spaces quoted
        var escapedPath = "\\\"" + exePath.Replace("\\", "\\\\") + "\\\"";
        return $"""
            Windows Registry Editor Version 5.00

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_DryRun]
            @="ROM Cleanup – DryRun Scan"

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_DryRun\command]
            @="{escapedPath} --roots \\\"%V\\\" --mode DryRun"

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_Move]
            @="ROM Cleanup – Move Sort"

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_Move\command]
            @="{escapedPath} --roots \\\"%V\\\" --mode Move"
            """;
    }

    // ═══ ACCESSIBILITY ══════════════════════════════════════════════════
    // Port of Accessibility.ps1

    public static bool IsHighContrastActive()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Control Panel\Accessibility\HighContrast");
            var flags = key?.GetValue("Flags") as string;
            return flags is not null && int.TryParse(flags, out var f) && (f & 1) != 0;
        }
        catch { return false; }
    }

    // ═══ CSV REPORT PARSER ═════════════════════════════════════════════
    // Parse a CSV report file (as exported by ExportCollectionCsv) back into RomCandidate objects.

    public static IReadOnlyList<RomCandidate> ParseCsvReport(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return [];

        var results = new List<RomCandidate>();
        var lines = File.ReadAllLines(filePath);
        if (lines.Length < 2) return [];

        // Parse header to determine column indices
        var headers = ParseCsvLine(lines[0]);
        var colIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
            colIndex[headers[i].Trim()] = i;

        for (int row = 1; row < lines.Length; row++)
        {
            var line = lines[row];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = ParseCsvLine(line);

            string GetField(string name) =>
                colIndex.TryGetValue(name, out var idx) && idx < fields.Length ? fields[idx].Trim() : "";

            var sizeStr = GetField("SizeBytes");
            long.TryParse(sizeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var sizeBytes);

            var datStr = GetField("DatMatch");
            var datMatch = datStr.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                           datStr.Equals("Verified", StringComparison.OrdinalIgnoreCase);

            results.Add(new RomCandidate
            {
                MainPath = GetField("MainPath"),
                GameKey = GetField("GameKey"),
                Extension = GetField("Extension"),
                Region = GetField("Region") is { Length: > 0 } r ? r : "UNKNOWN",
                Category = GetField("Category") is { Length: > 0 } cat ? cat : "GAME",
                SizeBytes = sizeBytes,
                DatMatch = datMatch
            });
        }

        return results;
    }

    // ═══ APPLY FILTER ══════════════════════════════════════════════════
    // Filter candidates by field/operator/value.

    public static IReadOnlyList<RomCandidate> ApplyFilter(
        IReadOnlyList<RomCandidate> candidates, string field, string op, string value)
    {
        if (candidates is null || candidates.Count == 0)
            return [];

        return candidates.Where(c => EvaluateFilter(c, field, op, value)).ToList();
    }

    private static string ResolveField(RomCandidate c, string field) => field.ToLowerInvariant() switch
    {
        "console" => DetectConsoleFromPath(c.MainPath),
        "region" => c.Region,
        "format" => c.Extension,
        "category" => c.Category,
        "sizemb" => (c.SizeBytes / 1048576.0).ToString("F2", CultureInfo.InvariantCulture),
        "datstatus" => c.DatMatch ? "Verified" : "Unverified",
        "filename" => Path.GetFileName(c.MainPath),
        "gamekey" => c.GameKey,
        _ => ""
    };

    private static bool EvaluateFilter(RomCandidate c, string field, string op, string value)
    {
        var fieldValue = ResolveField(c, field);
        return op.ToLowerInvariant() switch
        {
            "eq" => fieldValue.Equals(value, StringComparison.OrdinalIgnoreCase),
            "neq" => !fieldValue.Equals(value, StringComparison.OrdinalIgnoreCase),
            "contains" => fieldValue.Contains(value, StringComparison.OrdinalIgnoreCase),
            "gt" => double.TryParse(fieldValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var fv) &&
                    double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var tv) && fv > tv,
            "lt" => double.TryParse(fieldValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var fv2) &&
                    double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var tv2) && fv2 < tv2,
            "regex" => TryRegexMatch(fieldValue, value),
            _ => false
        };
    }

    private static bool TryRegexMatch(string input, string pattern)
    {
        try { return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase); }
        catch { return false; }
    }

    // ═══ DAT FILE COMPARE ══════════════════════════════════════════════
    // Compare two Logiqx XML DAT files and return a diff summary.

    public static DatDiffResult CompareDatFiles(string pathA, string pathB)
    {
        var gamesA = LoadDatGameNames(pathA);
        var gamesB = LoadDatGameNames(pathB);

        var setA = new HashSet<string>(gamesA, StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(gamesB, StringComparer.OrdinalIgnoreCase);

        var added = gamesB.Where(g => !setA.Contains(g)).ToList();
        var removed = gamesA.Where(g => !setB.Contains(g)).ToList();
        var common = gamesA.Where(g => setB.Contains(g)).ToList();

        // Modified detection: compare <rom> child elements for games in both
        int modified = 0;
        int unchanged = 0;
        if (File.Exists(pathA) && File.Exists(pathB))
        {
            var docA = SafeLoadXDocument(pathA);
            var docB = SafeLoadXDocument(pathB);
            var gameMapA = BuildGameElementMap(docA);
            var gameMapB = BuildGameElementMap(docB);

            foreach (var name in common)
            {
                var xmlA = gameMapA.GetValueOrDefault(name);
                var xmlB = gameMapB.GetValueOrDefault(name);
                if (xmlA is not null && xmlB is not null &&
                    !string.Equals(xmlA, xmlB, StringComparison.Ordinal))
                    modified++;
                else
                    unchanged++;
            }
        }
        else
        {
            unchanged = common.Count;
        }

        return new DatDiffResult(added, removed, modified, unchanged);
    }

    private static List<string> LoadDatGameNames(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return [];
        try
        {
            var doc = SafeLoadXDocument(path);
            return doc.Descendants("game")
                .Select(e => e.Attribute("name")?.Value ?? "")
                .Where(n => n.Length > 0)
                .ToList();
        }
        catch { return []; }
    }

    private static Dictionary<string, string> BuildGameElementMap(XDocument doc)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in doc.Descendants("game"))
        {
            var name = game.Attribute("name")?.Value;
            if (name is not null)
                map.TryAdd(name, game.ToString());
        }
        return map;
    }

    // ═══ FORMAT RULES FROM JSON ════════════════════════════════════════
    // Load data/rules.json, format rules as a readable string.

    public static string FormatRulesFromJson(string rulesPath, IReadOnlyList<RomCandidate>? candidates = null)
    {
        if (string.IsNullOrEmpty(rulesPath) || !File.Exists(rulesPath))
            return "Keine Regeldatei gefunden.";

        List<ClassificationRule>? rules;
        try
        {
            var json = File.ReadAllText(rulesPath);
            rules = JsonSerializer.Deserialize<List<ClassificationRule>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            return $"Fehler beim Laden der Regeln: {ex.Message}";
        }

        if (rules is null || rules.Count == 0)
            return "Keine Regeln definiert.";

        var sb = new StringBuilder();
        sb.AppendLine("Regel-Übersicht");
        sb.AppendLine(new string('═', 50));

        foreach (var rule in rules.OrderBy(r => r.Priority))
        {
            var status = rule.Enabled ? "aktiv" : "inaktiv";
            sb.AppendLine($"\n  [{rule.Priority}] {rule.Name} ({status})");
            sb.AppendLine($"    Aktion: {rule.Action}");
            if (!string.IsNullOrEmpty(rule.Reason))
                sb.AppendLine($"    Grund: {rule.Reason}");

            if (rule.Conditions.Count > 0)
            {
                sb.AppendLine("    Bedingungen:");
                foreach (var cond in rule.Conditions)
                    sb.AppendLine($"      • {cond.Field} {cond.Op} \"{cond.Value}\"");
            }

            if (candidates is { Count: > 0 } && rule.Enabled)
            {
                var matchCount = candidates.Count(c =>
                    rule.Conditions.All(cond => EvaluateFilter(c, cond.Field, cond.Op, cond.Value)));
                sb.AppendLine($"    Treffer: {matchCount}/{candidates.Count} Kandidaten");
            }
        }

        sb.AppendLine($"\nGesamt: {rules.Count} Regeln ({rules.Count(r => r.Enabled)} aktiv)");
        return sb.ToString();
    }

    // ═══ NES HEADER REPAIR ═════════════════════════════════════════════
    // Check if NES ROM has dirty bytes at offset 12-15. If so, zero them.

    public static bool RepairNesHeader(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

            if (fs.Length < 16)
                return false;

            var header = new byte[16];
            var read = fs.Read(header, 0, header.Length);
            if (read < 16)
                return false;

            // Verify iNES magic: 4E 45 53 1A
            if (header[0] != 0x4E || header[1] != 0x45 || header[2] != 0x53 || header[3] != 0x1A)
                return false;

            // Check if bytes 12-15 are dirty (non-zero)
            bool dirty = false;
            for (int i = 12; i <= 15; i++)
            {
                if (header[i] != 0x00)
                { dirty = true; break; }
            }

            if (!dirty) return false;

            // Create backup
            File.Copy(path, path + ".bak", overwrite: true);

            // Zero bytes 12-15 in place (streaming-safe for large files).
            fs.Seek(12, SeekOrigin.Begin);
            var zeroBytes = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            fs.Write(zeroBytes, 0, zeroBytes.Length);
            fs.Flush();
            return true;
        }
        catch { return false; }
    }

    // ═══ COPIER HEADER REMOVAL ═════════════════════════════════════════
    // Check if SNES ROM has a 512-byte copier header and remove it.

    public static bool RemoveCopierHeader(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        try
        {
            var fi = new FileInfo(path);
            if (fi.Length < 512 || fi.Length % 1024 != 512)
                return false;

            // Create backup
            File.Copy(path, path + ".bak", overwrite: true);

            // Read file, skip first 512 bytes, write back
            byte[] data = File.ReadAllBytes(path);
            byte[] stripped = new byte[data.Length - 512];
            Array.Copy(data, 512, stripped, 0, stripped.Length);
            File.WriteAllBytes(path, stripped);
            return true;
        }
        catch { return false; }
    }

    // ═══ LOGIQX XML GENERATOR ══════════════════════════════════════════
    // Generate Logiqx XML string for a single game entry.

    public static string GenerateLogiqxEntry(string gameName, string romName, string crc, string sha1, long size)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("datafile",
                new XElement("game",
                    new XAttribute("name", gameName),
                    new XElement("description", gameName),
                    new XElement("rom",
                        new XAttribute("name", romName),
                        new XAttribute("size", size),
                        new XAttribute("crc", crc),
                        new XAttribute("sha1", sha1)))));

        return doc.Declaration + Environment.NewLine + doc.Root;
    }

    /// <summary>
    /// Append a custom DAT entry (Logiqx XML fragment) to <paramref name="datRoot"/>/custom.dat.
    /// Creates the file if it doesn't exist. Atomic write via temp+move.
    /// </summary>
    public static void AppendCustomDatEntry(string datRoot, string xmlEntry)
    {
        var customDatPath = Path.Combine(datRoot, "custom.dat");
        if (File.Exists(customDatPath))
        {
            var content = File.ReadAllText(customDatPath);
            var closeTag = "</datafile>";
            var idx = content.LastIndexOf(closeTag, StringComparison.OrdinalIgnoreCase);
            content = idx >= 0
                ? content[..idx] + xmlEntry + "\n" + closeTag
                : content + "\n" + xmlEntry;
            var tempPath = customDatPath + ".tmp";
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, customDatPath, overwrite: true);
        }
        else
        {
            var fullXml = "<?xml version=\"1.0\"?>\n" +
                          "<!DOCTYPE datafile SYSTEM \"http://www.logiqx.com/Dats/datafile.dtd\">\n" +
                          "<datafile>\n" +
                          "  <header>\n" +
                          "    <name>Custom DAT</name>\n" +
                          "    <description>Benutzerdefinierte DAT-Einträge</description>\n" +
                          "  </header>\n" +
                          xmlEntry + "\n" +
                          "</datafile>";
            File.WriteAllText(customDatPath, fullXml);
        }
    }

    // ═══ CSV DIFF ═══════════════════════════════════════════════════════

    /// <summary>
    /// Compare two CSV report files and build a diff report string.
    /// Returns null if comparison is not possible (non-CSV files).
    /// </summary>
    public static string? BuildCsvDiff(string fileA, string fileB, string title)
    {
        if (!fileA.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
            !fileB.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return null;

        var setA = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(fileA).Skip(1))
        {
            var mainPath = line.Split(';')[0].Trim('"');
            if (!string.IsNullOrWhiteSpace(mainPath))
                setA.Add(mainPath);
        }
        foreach (var line in File.ReadLines(fileB).Skip(1))
        {
            var mainPath = line.Split(';')[0].Trim('"');
            if (!string.IsNullOrWhiteSpace(mainPath))
                setB.Add(mainPath);
        }

        var added = setB.Except(setA).ToList();
        var removed = setA.Except(setB).ToList();
        var same = setA.Intersect(setB).Count();

        var sb = new StringBuilder();
        sb.AppendLine($"{title} (CSV)");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  A: {Path.GetFileName(fileA)} ({setA.Count} Einträge)");
        sb.AppendLine($"  B: {Path.GetFileName(fileB)} ({setB.Count} Einträge)");
        sb.AppendLine($"\n  Gleich:      {same}");
        sb.AppendLine($"  Hinzugefügt: {added.Count}");
        sb.AppendLine($"  Entfernt:    {removed.Count}");

        if (added.Count > 0)
        {
            sb.AppendLine($"\n  --- Hinzugefügt (erste {Math.Min(30, added.Count)}) ---");
            foreach (var entry in added.Take(30))
                sb.AppendLine($"    + {Path.GetFileName(entry)}");
            if (added.Count > 30)
                sb.AppendLine($"    … und {added.Count - 30} weitere");
        }

        if (removed.Count > 0)
        {
            sb.AppendLine($"\n  --- Entfernt (erste {Math.Min(30, removed.Count)}) ---");
            foreach (var entry in removed.Take(30))
                sb.AppendLine($"    - {Path.GetFileName(entry)}");
            if (removed.Count > 30)
                sb.AppendLine($"    … und {removed.Count - 30} weitere");
        }

        return sb.ToString();
    }

    // ═══ MISSING ROM REPORT ════════════════════════════════════════════

    /// <summary>
    /// Build a "missing ROMs" (not DAT-verified) report grouped by subdirectory.
    /// Returns null if no unverified candidates exist.
    /// </summary>
    public static string? BuildMissingRomReport(IReadOnlyList<RomCandidate> candidates, IReadOnlyList<string> roots)
    {
        var unverified = candidates.Where(c => !c.DatMatch).ToList();
        if (unverified.Count == 0) return null;

        var normalizedRoots = roots
            .Select(r => Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToList();

        string GetSubDir(string filePath)
        {
            var full = Path.GetFullPath(filePath);
            foreach (var root in normalizedRoots)
            {
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = full[(root.Length + 1)..];
                    var sep = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                    return sep > 0 ? relative[..sep] : "(Root)";
                }
            }
            return Path.GetDirectoryName(filePath) ?? "(Unbekannt)";
        }

        var byDir = unverified.GroupBy(c => GetSubDir(c.MainPath))
            .OrderByDescending(g => g.Count())
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Fehlende ROMs (ohne DAT-Match)");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Gesamt ohne DAT-Match: {unverified.Count} / {candidates.Count}");
        sb.AppendLine($"\n  Nach Verzeichnis:\n");
        foreach (var g in byDir)
            sb.AppendLine($"    {g.Count(),5}  {g.Key}");

        return sb.ToString();
    }

    // ═══ CROSS-ROOT DUPLICATE REPORT ════════════════════════════════════

    /// <summary>
    /// Build a cross-root duplicate report showing groups spanning multiple roots.
    /// </summary>
    public static string BuildCrossRootReport(IReadOnlyList<DedupeResult> dedupeGroups, IReadOnlyList<string> roots)
    {
        var normalizedRoots = roots
            .Select(r => Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToList();

        string? GetRoot(string filePath)
        {
            var full = Path.GetFullPath(filePath);
            return normalizedRoots.FirstOrDefault(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase));
        }

        var crossRootGroups = new List<DedupeResult>();
        foreach (var g in dedupeGroups)
        {
            var allPaths = new[] { g.Winner }.Concat(g.Losers);
            var distinctRoots = allPaths.Select(c => GetRoot(c.MainPath)).Where(r => r is not null).Distinct().Count();
            if (distinctRoots > 1)
                crossRootGroups.Add(g);
        }

        var sb = new StringBuilder();
        sb.AppendLine("Cross-Root-Duplikate");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Roots: {roots.Count}");
        sb.AppendLine($"  Dedupe-Gruppen gesamt: {dedupeGroups.Count}");
        sb.AppendLine($"  Cross-Root-Gruppen: {crossRootGroups.Count}\n");

        foreach (var g in crossRootGroups.Take(30))
        {
            sb.AppendLine($"  [{g.GameKey}]");
            sb.AppendLine($"    Winner: {g.Winner.MainPath}");
            foreach (var l in g.Losers)
                sb.AppendLine($"    Loser:  {l.MainPath}");
        }
        if (crossRootGroups.Count > 30)
            sb.AppendLine($"\n  … und {crossRootGroups.Count - 30} weitere Gruppen");
        if (crossRootGroups.Count == 0)
            sb.AppendLine("  Keine Cross-Root-Duplikate gefunden.");

        return sb.ToString();
    }

    // ═══ COVER SCRAPER ═════════════════════════════════════════════════

    /// <summary>
    /// Format a DatDiffResult into a human-readable report.
    /// </summary>
    public static string FormatDatDiffReport(string fileA, string fileB, DatDiffResult diff)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DAT-Diff-Viewer (Logiqx XML)");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  A: {Path.GetFileName(fileA)}");
        sb.AppendLine($"  B: {Path.GetFileName(fileB)}");
        sb.AppendLine($"\n  Gleich:       {diff.UnchangedCount}");
        sb.AppendLine($"  Geändert:     {diff.ModifiedCount}");
        sb.AppendLine($"  Hinzugefügt:  {diff.Added.Count}");
        sb.AppendLine($"  Entfernt:     {diff.Removed.Count}");

        if (diff.Added.Count > 0)
        {
            sb.AppendLine($"\n  --- Hinzugefügt (erste {Math.Min(30, diff.Added.Count)}) ---");
            foreach (var name in diff.Added.Take(30))
                sb.AppendLine($"    + {name}");
            if (diff.Added.Count > 30)
                sb.AppendLine($"    … und {diff.Added.Count - 30} weitere");
        }

        if (diff.Removed.Count > 0)
        {
            sb.AppendLine($"\n  --- Entfernt (erste {Math.Min(30, diff.Removed.Count)}) ---");
            foreach (var name in diff.Removed.Take(30))
                sb.AppendLine($"    - {name}");
            if (diff.Removed.Count > 30)
                sb.AppendLine($"    … und {diff.Removed.Count - 30} weitere");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Build a DAT auto-update status report showing local DAT files, ages, and catalog info.
    /// </summary>
    public static (string Report, int LocalCount, int OldCount) BuildDatAutoUpdateReport(string datRoot)
    {
        var dataDir = ResolveDataDirectory() ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var catalogPath = Path.Combine(dataDir, "dat-catalog.json");

        var sb = new StringBuilder();
        sb.AppendLine("DAT Auto-Update");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  DAT-Root: {datRoot}");

        var localDats = Directory.GetFiles(datRoot, "*.dat", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(datRoot, "*.xml", SearchOption.AllDirectories))
            .ToList();

        sb.AppendLine($"  Lokale DAT-Dateien: {localDats.Count}\n");

        if (localDats.Count > 0)
        {
            sb.AppendLine("  Lokale Dateien (nach Alter sortiert):");
            foreach (var dat in localDats.OrderBy(d => File.GetLastWriteTime(d)).Take(20))
            {
                var age = DateTime.Now - File.GetLastWriteTime(dat);
                var ageStr = age.TotalDays > 365 ? $"{age.TotalDays / 365:0.0} Jahre"
                    : age.TotalDays > 30 ? $"{age.TotalDays / 30:0.0} Monate"
                    : $"{age.TotalDays:0} Tage";
                sb.AppendLine($"    {Path.GetFileName(dat),-40} {ageStr} alt");
            }
            if (localDats.Count > 20)
                sb.AppendLine($"    … und {localDats.Count - 20} weitere");
        }

        if (File.Exists(catalogPath))
        {
            try
            {
                var catalogJson = File.ReadAllText(catalogPath);
                using var doc = JsonDocument.Parse(catalogJson);
                var entries = doc.RootElement.EnumerateArray().ToList();
                var withUrl = entries.Count(e => e.TryGetProperty("Url", out var u) && u.GetString()?.Length > 0);
                var groups = entries.GroupBy(e => e.TryGetProperty("Group", out var g) ? g.GetString() : "?");

                sb.AppendLine($"\n  Katalog: {entries.Count} Einträge ({withUrl} mit Download-URL)");
                foreach (var g in groups)
                    sb.AppendLine($"    {g.Key}: {g.Count()} Systeme");
            }
            catch (Exception ex)
            { sb.AppendLine($"\n  Katalog-Fehler: {ex.Message}"); }
        }
        else
            sb.AppendLine($"\n  Katalog nicht gefunden: {catalogPath}");

        var oldDats = localDats.Where(d => (DateTime.Now - File.GetLastWriteTime(d)).TotalDays > 180).ToList();
        if (oldDats.Count > 0)
            sb.AppendLine($"\n  ⚠ {oldDats.Count} DATs sind älter als 6 Monate!");

        return (sb.ToString(), localDats.Count, oldDats.Count);
    }

    /// <summary>
    /// Match cover images against ROM candidates and build a report.
    /// Returns (report, matchedCount, unmatchedCount).
    /// </summary>
    public static (string Report, int Matched, int Unmatched) BuildCoverReport(
        string coverDir, IReadOnlyList<RomCandidate> candidates)
    {
        var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
        var coverFiles = Directory.GetFiles(coverDir, "*.*", SearchOption.AllDirectories)
            .Where(f => imageExts.Contains(Path.GetExtension(f)))
            .ToList();

        if (coverFiles.Count == 0)
            return ($"Keine Cover-Bilder gefunden in:\n{coverDir}", 0, 0);

        var gameKeys = candidates
            .Select(c => RomCleanup.Core.GameKeys.GameKeyNormalizer.Normalize(
                Path.GetFileNameWithoutExtension(c.MainPath)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matched = new List<string>();
        var unmatched = new List<string>();
        foreach (var cover in coverFiles)
        {
            var coverName = Path.GetFileNameWithoutExtension(cover);
            var normalizedCover = RomCleanup.Core.GameKeys.GameKeyNormalizer.Normalize(coverName);
            if (gameKeys.Contains(normalizedCover))
                matched.Add(coverName);
            else
                unmatched.Add(coverName);
        }

        var sb = new StringBuilder();
        sb.AppendLine("Cover-Scraper Ergebnis\n");
        sb.AppendLine($"  Cover-Ordner: {coverDir}");
        sb.AppendLine($"  Gefundene Bilder: {coverFiles.Count}");
        sb.AppendLine($"  ROMs in Sammlung: {gameKeys.Count}");
        sb.AppendLine($"\n  Zugeordnet:    {matched.Count}");
        sb.AppendLine($"  Nicht zugeordnet: {unmatched.Count}");
        sb.AppendLine($"  Ohne Cover:    {gameKeys.Count - matched.Count}");

        if (matched.Count > 0)
        {
            sb.AppendLine($"\n  --- Zugeordnet (erste {Math.Min(15, matched.Count)}) ---");
            foreach (var m in matched.Take(15))
                sb.AppendLine($"    \u2713 {m}");
        }
        if (unmatched.Count > 0)
        {
            sb.AppendLine($"\n  --- Nicht zugeordnet (erste {Math.Min(15, unmatched.Count)}) ---");
            foreach (var u in unmatched.Take(15))
                sb.AppendLine($"    ? {u}");
        }

        return (sb.ToString(), matched.Count, unmatched.Count);
    }

    // ═══ RULE ENGINE REPORT ═════════════════════════════════════════════

    /// <summary>
    /// Build a report of all rules from rules.json, or a default help text if not found.
    /// </summary>
    public static string BuildRuleEngineReport()
    {
        var dataDir = ResolveDataDirectory() ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        var rulesPath = Path.Combine(dataDir, "rules.json");

        if (!File.Exists(rulesPath))
        {
            return "Benutzerdefinierte Regeln\n\n" +
                "Erstelle Regeln mit Bedingungen und Aktionen:\n\n" +
                "Bedingungen: Region, Format, Größe, Name, Konsole, DAT-Status\n" +
                "Operatoren: eq, neq, contains, gt, lt, regex\n" +
                "Aktionen: junk, keep, quarantine\n\n" +
                "Regeln werden nach Priorität (höher = zuerst) ausgewertet.\n" +
                "Die erste passende Regel gewinnt.\n\n" +
                "Keine rules.json gefunden.\n" +
                "Konfiguration in data/rules.json";
        }

        var json = File.ReadAllText(rulesPath);
        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();
        sb.AppendLine("Regel-Engine");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Datei: {rulesPath}\n");

        int idx = 0;
        foreach (var rule in doc.RootElement.EnumerateArray())
        {
            idx++;
            var name = rule.TryGetProperty("name", out var np) ? np.GetString() : $"Regel {idx}";
            var priority = rule.TryGetProperty("priority", out var pp) ? pp.GetInt32() : 0;
            var action = rule.TryGetProperty("action", out var ap) ? ap.GetString() : "?";

            sb.AppendLine($"  [{idx}] {name}  (Priorität: {priority}, Aktion: {action})");

            if (rule.TryGetProperty("conditions", out var conds))
            {
                foreach (var cond in conds.EnumerateArray())
                {
                    var field = cond.TryGetProperty("field", out var fp) ? fp.GetString() : "?";
                    var op = cond.TryGetProperty("operator", out var opp) ? opp.GetString() : "?";
                    var val = cond.TryGetProperty("value", out var vp) ? vp.GetString() : "?";
                    sb.AppendLine($"      Bedingung: {field} {op} {val}");
                }
            }
            sb.AppendLine();
        }

        if (idx == 0)
            sb.AppendLine("  Keine Regeln definiert.");

        return sb.ToString();
    }

    // ═══ ARCADE MERGE/SPLIT REPORT ══════════════════════════════════════

    /// <summary>
    /// Parse a MAME/FBNEO DAT file and build a merge/split analysis report.
    /// </summary>
    public static string BuildArcadeMergeSplitReport(string datPath)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(datPath, settings);
        var doc = XDocument.Load(reader);

        var games = doc.Descendants("game").ToList();
        if (games.Count == 0)
            games = doc.Descendants("machine").ToList();

        var parents = games.Where(g => g.Attribute("cloneof") == null).ToList();
        var clones = games.Where(g => g.Attribute("cloneof") != null).ToList();

        var cloneMap = clones.GroupBy(g => g.Attribute("cloneof")?.Value ?? "")
            .ToDictionary(g => g.Key, g => g.ToList());

        var totalRoms = games.Sum(g => g.Descendants("rom").Count());
        var largestParent = parents.OrderByDescending(p =>
        {
            var name = p.Attribute("name")?.Value ?? "";
            return cloneMap.TryGetValue(name, out var c) ? c.Count : 0;
        }).FirstOrDefault();
        var largestName = largestParent?.Attribute("name")?.Value ?? "?";
        var largestCloneCount = cloneMap.TryGetValue(largestName, out var lc) ? lc.Count : 0;

        var sb = new StringBuilder();
        sb.AppendLine("Arcade Merge/Split Analyse");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  DAT: {Path.GetFileName(datPath)}");
        sb.AppendLine($"  Einträge gesamt: {games.Count}");
        sb.AppendLine($"  Parents:         {parents.Count}");
        sb.AppendLine($"  Clones:          {clones.Count}");
        sb.AppendLine($"  ROMs gesamt:     {totalRoms}");
        sb.AppendLine($"\n  Größte Familie: {largestName} ({largestCloneCount} Clones)");
        sb.AppendLine($"\n  Set-Typ-Empfehlung:");
        sb.AppendLine($"    Non-Merged: {parents.Count + clones.Count} Sets (portabel, groß)");
        sb.AppendLine($"    Split:      {parents.Count + clones.Count} Sets (Clones nur diff)");
        sb.AppendLine($"    Merged:     {parents.Count} Sets (Parents enthalten alles)");

        var top10 = parents
            .Select(p => new { Name = p.Attribute("name")?.Value ?? "?",
                Clones = cloneMap.TryGetValue(p.Attribute("name")?.Value ?? "", out var cc) ? cc.Count : 0 })
            .OrderByDescending(x => x.Clones)
            .Take(10);
        sb.AppendLine($"\n  Top 10 Parents (meiste Clones):");
        foreach (var p in top10)
            sb.AppendLine($"    {p.Name,-30} {p.Clones} Clones");

        return sb.ToString();
    }

    // ═══ CONVERT QUEUE REPORT ═══════════════════════════════════════════

    /// <summary>
    /// Build a conversion queue report from conversion estimates.
    /// </summary>
    public static string BuildConvertQueueReport(ConversionEstimateResult est)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Konvert-Warteschlange");
        sb.AppendLine(new string('═', 60));
        sb.AppendLine($"\n  Dateien: {est.Details.Count}");
        sb.AppendLine($"  Quellgröße: {FormatSize(est.TotalSourceBytes)}");
        sb.AppendLine($"  Geschätzte Zielgröße: {FormatSize(est.EstimatedTargetBytes)}");
        sb.AppendLine($"  Ersparnis: {FormatSize(est.SavedBytes)}\n");

        if (est.Details.Count > 0)
        {
            sb.AppendLine($"  {"Datei",-40} {"Quelle",-8} {"Ziel",-8} {"Größe",12}");
            sb.AppendLine($"  {new string('-', 40)} {new string('-', 8)} {new string('-', 8)} {new string('-', 12)}");
            foreach (var d in est.Details)
                sb.AppendLine($"  {d.FileName,-40} {d.SourceFormat,-8} {d.TargetFormat,-8} {FormatSize(d.SourceBytes),12}");
        }
        else
        {
            sb.AppendLine("  Keine konvertierbaren Dateien gefunden.");
        }

        return sb.ToString();
    }

    // ═══ PIPELINE REPORT ════════════════════════════════════════════════

    /// <summary>
    /// Build a pipeline engine report from a run result and candidate list.
    /// If result is null, returns a default help text.
    /// </summary>
    public static string BuildPipelineReport(RunResult? result, IReadOnlyList<RomCandidate> candidates)
    {
        if (result is null)
        {
            return "Pipeline-Engine\n\n" +
                "Bedingte Multi-Step-Pipelines:\n\n" +
                "  1. Scan → Dateien erfassen\n" +
                "  2. Dedupe → Duplikate erkennen\n" +
                "  3. Sort → Nach Konsole sortieren\n" +
                "  4. Convert → Formate konvertieren\n" +
                "  5. Verify → Konvertierung prüfen\n\n" +
                "Jeder Schritt kann übersprungen werden.\n" +
                "DryRun-aware: Kein Schreibzugriff im DryRun-Modus.\n\n" +
                "Starte einen Lauf, um Pipeline-Ergebnisse zu sehen.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Pipeline-Engine — Letzter Lauf");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Status: {result.Status}");
        sb.AppendLine($"  Dauer:  {result.DurationMs / 1000.0:F1}s\n");

        sb.AppendLine($"  {"Phase",-20} {"Status",-15} {"Details"}");
        sb.AppendLine($"  {new string('-', 20)} {new string('-', 15)} {new string('-', 30)}");

        sb.AppendLine($"  {"Scan",-20} {"OK",-15} {result.TotalFilesScanned} Dateien");
        sb.AppendLine($"  {"Dedupe",-20} {"OK",-15} {result.GroupCount} Gruppen, {result.WinnerCount} Winner");

        var junkCount = candidates.Count(c => c.Category == "JUNK");
        sb.AppendLine($"  {"Junk-Erkennung",-20} {"OK",-15} {junkCount} Junk-Dateien");

        if (result.ConsoleSortResult is { } cs)
            sb.AppendLine($"  {"Konsolen-Sort",-20} {"OK",-15} sortiert");
        else
            sb.AppendLine($"  {"Konsolen-Sort",-20} {"Übersprungen",-15}");

        if (result.ConvertedCount > 0)
            sb.AppendLine($"  {"Konvertierung",-20} {"OK",-15} {result.ConvertedCount} konvertiert");
        else
            sb.AppendLine($"  {"Konvertierung",-20} {"Übersprungen",-15}");

        if (result.MoveResult is { } mv)
            sb.AppendLine($"  {"Move",-20} {(mv.FailCount > 0 ? "WARNUNG" : "OK"),-15} {mv.MoveCount} verschoben, {mv.FailCount} Fehler");
        else
            sb.AppendLine($"  {"Move",-20} {"DryRun",-15} keine Änderungen");

        return sb.ToString();
    }

    // ═══ FILTER BUILDER ═════════════════════════════════════════════════

    /// <summary>Parse a filter expression like "field=value" or "field>=value".</summary>
    public static (string Field, string Op, string Value)? ParseFilterExpression(string input)
    {
        string field, op, value;
        if (input.Contains(">=")) { var p = input.Split(">=", 2); field = p[0].Trim().ToLowerInvariant(); op = ">="; value = p[1].Trim(); }
        else if (input.Contains("<=")) { var p = input.Split("<=", 2); field = p[0].Trim().ToLowerInvariant(); op = "<="; value = p[1].Trim(); }
        else if (input.Contains('>')) { var p = input.Split('>', 2); field = p[0].Trim().ToLowerInvariant(); op = ">"; value = p[1].Trim(); }
        else if (input.Contains('<')) { var p = input.Split('<', 2); field = p[0].Trim().ToLowerInvariant(); op = "<"; value = p[1].Trim(); }
        else if (input.Contains('=')) { var p = input.Split('=', 2); field = p[0].Trim().ToLowerInvariant(); op = "="; value = p[1].Trim(); }
        else return null;
        return (field, op, value);
    }

    /// <summary>Apply a parsed filter to candidates and build a report.</summary>
    public static string BuildFilterReport(IReadOnlyList<RomCandidate> candidates, string field, string op, string value)
    {
        var filtered = candidates.Where(c =>
        {
            string fieldValue = field switch
            {
                "region" => c.Region,
                "category" => c.Category,
                "extension" or "ext" => c.Extension,
                "gamekey" or "game" => c.GameKey,
                "type" or "consolekey" or "console" => c.ConsoleKey,
                "datmatch" or "dat" => c.DatMatch.ToString(),
                "sizemb" => (c.SizeBytes / 1048576.0).ToString("F1"),
                "sizebytes" or "size" => c.SizeBytes.ToString(),
                "filename" or "name" => Path.GetFileName(c.MainPath),
                _ => ""
            };
            if (op == "=")
                return fieldValue.Contains(value, StringComparison.OrdinalIgnoreCase);
            if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var numVal) &&
                double.TryParse(fieldValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var fieldNum))
            {
                return op switch { ">" => fieldNum > numVal, "<" => fieldNum < numVal, ">=" => fieldNum >= numVal, "<=" => fieldNum <= numVal, _ => false };
            }
            return false;
        }).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Filter-Builder: {field} {op} {value}");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Gesamt: {candidates.Count}");
        sb.AppendLine($"  Gefiltert: {filtered.Count}\n");
        foreach (var r in filtered.Take(50))
            sb.AppendLine($"  {Path.GetFileName(r.MainPath),-45} [{r.Region}] {r.Extension} {r.Category} {FormatSize(r.SizeBytes)}");
        if (filtered.Count > 50)
            sb.AppendLine($"\n  … und {filtered.Count - 50} weitere");
        return sb.ToString();
    }

    // ═══ PLUGIN MARKETPLACE ═════════════════════════════════════════════

    /// <summary>Build a report of installed plugins.</summary>
    public static string BuildPluginMarketplaceReport(string pluginDir)
    {
        if (!Directory.Exists(pluginDir))
            Directory.CreateDirectory(pluginDir);

        var manifests = Directory.GetFiles(pluginDir, "*.json", SearchOption.AllDirectories);
        var dlls = Directory.GetFiles(pluginDir, "*.dll", SearchOption.AllDirectories);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Plugin-Manager (Coming Soon)\n");
        sb.AppendLine("  ℹ Das Plugin-System ist in Planung und noch nicht funktionsfähig.");
        sb.AppendLine($"  Plugin-Verzeichnis: {pluginDir}\n");
        sb.AppendLine($"  Manifeste:   {manifests.Length}");
        sb.AppendLine($"  DLLs:        {dlls.Length}\n");

        if (manifests.Length == 0 && dlls.Length == 0)
        {
            sb.AppendLine("  Keine Plugins installiert.\n");
            sb.AppendLine("  Plugin-Struktur:");
            sb.AppendLine("    plugins/");
            sb.AppendLine("      mein-plugin/");
            sb.AppendLine("        manifest.json");
            sb.AppendLine("        MeinPlugin.dll\n");
            sb.AppendLine("  Manifest-Format:");
            sb.AppendLine("    {");
            sb.AppendLine("      \"name\": \"Mein Plugin\",");
            sb.AppendLine("      \"version\": \"1.0.0\",");
            sb.AppendLine("      \"type\": \"console|format|report\"");
            sb.AppendLine("    }");
        }
        else
        {
            foreach (var manifest in manifests)
            {
                try
                {
                    var json = File.ReadAllText(manifest);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var name = doc.RootElement.TryGetProperty("name", out var np) ? np.GetString() : Path.GetFileName(manifest);
                    var ver = doc.RootElement.TryGetProperty("version", out var vp) ? vp.GetString() : "?";
                    var type = doc.RootElement.TryGetProperty("type", out var tp) ? tp.GetString() : "?";
                    sb.AppendLine($"  [{type}] {name} v{ver}");
                    sb.AppendLine($"         {Path.GetDirectoryName(manifest)}");
                }
                catch
                {
                    sb.AppendLine($"  [?] {Path.GetFileName(manifest)} (manifest ungültig)");
                }
            }
            if (dlls.Length > 0)
            {
                sb.AppendLine($"\n  DLLs:");
                foreach (var dll in dlls)
                    sb.AppendLine($"    {Path.GetFileName(dll)}");
            }
        }
        return sb.ToString();
    }

    // ═══ MULTI-INSTANCE SYNC ════════════════════════════════════════════

    /// <summary>Build a report about lock files found in the given roots.</summary>
    public static string BuildMultiInstanceReport(IReadOnlyList<string> roots, bool isBusy)
    {
        var locks = new List<(string path, string content)>();
        foreach (var root in roots)
        {
            var lockFile = Path.Combine(root, ".romcleanup.lock");
            if (File.Exists(lockFile))
            {
                try { locks.Add((lockFile, File.ReadAllText(lockFile))); }
                catch { locks.Add((lockFile, "(nicht lesbar)")); }
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Multi-Instanz-Synchronisation");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Konfigurierte Roots: {roots.Count}");
        sb.AppendLine($"  Aktive Locks:       {locks.Count}");

        if (locks.Count > 0)
        {
            sb.AppendLine("\n  Gefundene Lock-Dateien:");
            foreach (var (path, content) in locks)
            {
                sb.AppendLine($"    {path}");
                sb.AppendLine($"      {content}");
            }
        }
        else
        {
            sb.AppendLine("\n  Keine aktiven Locks gefunden.");
        }

        sb.AppendLine($"\n  Diese Instanz:");
        sb.AppendLine($"    PID:      {Environment.ProcessId}");
        sb.AppendLine($"    Hostname: {Environment.MachineName}");
        sb.AppendLine($"    Status:   {(isBusy ? "LÄUFT" : "Bereit")}");
        return sb.ToString();
    }

    /// <summary>Remove lock files from roots. Returns number removed.</summary>
    public static int RemoveLockFiles(IReadOnlyList<string> roots)
    {
        int removed = 0;
        foreach (var root in roots)
        {
            var lockFile = Path.Combine(root, ".romcleanup.lock");
            if (File.Exists(lockFile))
            {
                try { File.Delete(lockFile); removed++; }
                catch { /* in use */ }
            }
        }
        return removed;
    }

    /// <summary>Check if any lock files exist in the given roots.</summary>
    public static bool HasLockFiles(IReadOnlyList<string> roots) =>
        roots.Any(r => File.Exists(Path.Combine(r, ".romcleanup.lock")));

    // ═══ MOBILE WEB UI ══════════════════════════════════════════════════

    /// <summary>Try to find the API project path.</summary>
    public static string? FindApiProjectPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "RomCleanup.Api", "RomCleanup.Api.csproj"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "RomCleanup.Api", "RomCleanup.Api.csproj")
        };
        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }

    // ═══ RULE PACK SHARING ══════════════════════════════════════════════

    /// <summary>Export rules.json to a user-chosen path.</summary>
    public static bool ExportRulePack(string rulesPath, string savePath)
    {
        if (!File.Exists(rulesPath)) return false;
        File.Copy(rulesPath, savePath, overwrite: true);
        return true;
    }

    /// <summary>Import rules.json from an external file (validates JSON first).</summary>
    public static void ImportRulePack(string importPath, string rulesPath)
    {
        var json = File.ReadAllText(importPath);
        System.Text.Json.JsonDocument.Parse(json).Dispose(); // validate
        var dir = Path.GetDirectoryName(rulesPath);
        if (dir is not null) Directory.CreateDirectory(dir);
        File.Copy(importPath, rulesPath, overwrite: true);
    }

    // ═══ BATCH-3 EXTRACTIONS ════════════════════════════════════════════

    /// <summary>Build NKit conversion info report, including tool detection.</summary>
    public static string BuildNKitConvertReport(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var isNkit = filePath.Contains(".nkit", StringComparison.OrdinalIgnoreCase);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"NKit-Konvertierung\n");
        sb.AppendLine($"  Image:       {fileName}");
        sb.AppendLine($"  NKit-Format: {(isNkit ? "Ja" : "Nein")}");

        try
        {
            var runner = new ToolRunnerAdapter(null);
            var nkitPath = runner.FindTool("nkit");
            if (nkitPath is not null)
            {
                sb.AppendLine($"  NKit-Tool:   {nkitPath}");
                sb.AppendLine("\nKonvertierungs-Anleitung:");
                sb.AppendLine("  NKit → ISO: NKit.exe recover <Datei>");
                sb.AppendLine("  NKit → RVZ: Erst recover, dann dolphintool convert");
                sb.AppendLine("\nEmpfohlenes Zielformat: RVZ (GameCube/Wii)");
            }
            else
            {
                sb.AppendLine("\n  NKit-Tool nicht gefunden.");
                sb.AppendLine("\n  Nach dem Download das Tool in den PATH aufnehmen");
                sb.AppendLine("  oder im Programmverzeichnis ablegen.");
            }
        }
        catch
        {
            sb.AppendLine("\n  NKit-Tool-Suche fehlgeschlagen.");
            sb.AppendLine("  Konvertierung nach ISO/RVZ erfordert das Tool 'NKit'.");
        }
        return sb.ToString();
    }

    /// <summary>Import a DAT file into datRoot with path-traversal protection.</summary>
    public static string ImportDatFileToRoot(string sourcePath, string datRoot)
    {
        var safeName = Path.GetFileName(sourcePath);
        var targetPath = Path.GetFullPath(Path.Combine(datRoot, safeName));
        if (!targetPath.StartsWith(Path.GetFullPath(datRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Pfad außerhalb des DatRoot.");
        File.Copy(sourcePath, targetPath, overwrite: true);
        return targetPath;
    }

    /// <summary>Validate and parse an FTP/SFTP URL. Returns report text.</summary>
    public static (bool Valid, bool IsPlainFtp, string Report) BuildFtpSourceReport(string input)
    {
        var isValid = input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                      input.StartsWith("sftp://", StringComparison.OrdinalIgnoreCase);
        if (!isValid)
            return (false, false, $"Ungültige FTP-URL: {input} (muss mit ftp:// oder sftp:// beginnen)");

        var isPlainFtp = input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);
        try
        {
            var uri = new Uri(input);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("FTP-Quelle konfiguriert\n");
            sb.AppendLine($"  Protokoll: {uri.Scheme.ToUpperInvariant()}");
            sb.AppendLine($"  Host:      {uri.Host}");
            sb.AppendLine($"  Port:      {(uri.Port > 0 ? uri.Port : (uri.Scheme == "sftp" ? 22 : 21))}");
            sb.AppendLine($"  Pfad:      {uri.AbsolutePath}");
            sb.AppendLine("\n  ℹ FTP-Download ist noch nicht implementiert.");
            sb.AppendLine("  Aktuell wird die URL nur registriert und angezeigt.");
            sb.AppendLine("  Geplantes Feature: Dateien vor Verarbeitung lokal cachen.");
            return (true, isPlainFtp, sb.ToString());
        }
        catch (Exception ex)
        {
            return (false, isPlainFtp, $"FTP-URL ungültig: {ex.Message}");
        }
    }

    /// <summary>Build GPU hashing status report.</summary>
    public static (string Report, bool IsEnabled) BuildGpuHashingStatus()
    {
        var openCl = File.Exists(Path.Combine(Environment.SystemDirectory, "OpenCL.dll"));
        var currentSetting = Environment.GetEnvironmentVariable("ROMCLEANUP_GPU_HASHING") ?? "off";
        var isEnabled = currentSetting.Equals("on", StringComparison.OrdinalIgnoreCase);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("GPU-Hashing Konfiguration\n");
        sb.AppendLine($"  OpenCL verfügbar: {(openCl ? "Ja" : "Nein")}");
        sb.AppendLine($"  CPU-Kerne:        {Environment.ProcessorCount}");
        sb.AppendLine($"  Aktueller Status: {(isEnabled ? "AKTIVIERT" : "Deaktiviert")}");

        if (!openCl)
        {
            sb.AppendLine("\n  GPU-Hashing benötigt OpenCL-Treiber.");
            sb.AppendLine("  Installiere aktuelle GPU-Treiber für Unterstützung.");
        }
        else
        {
            sb.AppendLine("\n  GPU-Hashing kann SHA1/SHA256-Berechnungen");
            sb.AppendLine("  um 5-20x beschleunigen (experimentell).");
        }
        return (sb.ToString(), isEnabled);
    }

    /// <summary>Toggle GPU hashing and return the new state.</summary>
    public static bool ToggleGpuHashing()
    {
        var current = Environment.GetEnvironmentVariable("ROMCLEANUP_GPU_HASHING") ?? "off";
        var isEnabled = current.Equals("on", StringComparison.OrdinalIgnoreCase);
        Environment.SetEnvironmentVariable("ROMCLEANUP_GPU_HASHING", isEnabled ? "off" : "on");
        return !isEnabled;
    }

    /// <summary>Build report data for PDF/HTML export.</summary>
    public static (ReportSummary Summary, List<ReportEntry> Entries) BuildPdfReportData(
        IReadOnlyList<RomCandidate> candidates, IReadOnlyList<DedupeResult> groups,
        RunResult? runResult, bool dryRun)
    {
        var summary = new ReportSummary
        {
            Mode = dryRun ? "DryRun" : "Move",
            TotalFiles = candidates.Count,
            KeepCount = groups.Count,
            MoveCount = groups.Sum(g => g.Losers.Count),
            JunkCount = candidates.Count(c => c.Category == "JUNK"),
            GroupCount = groups.Count,
            Duration = TimeSpan.FromMilliseconds(runResult?.DurationMs ?? 0)
        };
        var entries = candidates.Select(c => new ReportEntry
        {
            GameKey = c.GameKey, Action = c.Category == "JUNK" ? "JUNK" : "KEEP",
            Category = c.Category, Region = c.Region, FilePath = c.MainPath,
            FileName = Path.GetFileName(c.MainPath), Extension = c.Extension,
            SizeBytes = c.SizeBytes, RegionScore = c.RegionScore, FormatScore = c.FormatScore,
            VersionScore = c.VersionScore, DatMatch = c.DatMatch
        }).ToList();
        return (summary, entries);
    }

    /// <summary>Build formatted conversion estimate report.</summary>
    public static string BuildConversionEstimateReport(IReadOnlyList<RomCandidate> candidates)
    {
        var est = GetConversionEstimate(candidates);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Konvertierungs-Schätzung");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"  Quellgröße:     {FormatSize(est.TotalSourceBytes)}");
        sb.AppendLine($"  Geschätzt:      {FormatSize(est.EstimatedTargetBytes)}");
        sb.AppendLine($"  Ersparnis:      {FormatSize(est.SavedBytes)} ({(1 - est.CompressionRatio) * 100:F1}%)");
        sb.AppendLine($"\nDetails ({est.Details.Count} konvertierbare Dateien):");
        foreach (var d in est.Details.Take(20))
            sb.AppendLine($"  {d.FileName}: {d.SourceFormat}→{d.TargetFormat} ({FormatSize(d.SourceBytes)}→{FormatSize(d.EstimatedBytes)})");
        if (est.Details.Count > 20)
            sb.AppendLine($"  … und {est.Details.Count - 20} weitere");
        return sb.ToString();
    }

    /// <summary>Validate a hex hash string (CRC32=8, SHA1=40 chars).</summary>
    public static bool IsValidHexHash(string hash, int expectedLength) =>
        hash.Length == expectedLength && Regex.IsMatch(hash, $"^[0-9A-Fa-f]{{{expectedLength}}}$");

    /// <summary>Build custom DAT XML entry using SecurityElement.Escape for safe XML.</summary>
    public static string BuildCustomDatXmlEntry(string gameName, string romName, string crc32, string sha1)
    {
        return $"  <game name=\"{System.Security.SecurityElement.Escape(gameName)}\">\n" +
               $"    <description>{System.Security.SecurityElement.Escape(gameName)}</description>\n" +
               $"    <rom name=\"{System.Security.SecurityElement.Escape(romName)}\" size=\"0\" crc=\"{crc32}\"" +
               (sha1.Length > 0 ? $" sha1=\"{sha1}\"" : "") + " />\n" +
               $"  </game>";
    }

    // ═══ BATCH-4 EXTRACTIONS ════════════════════════════════════════════

    /// <summary>Detect auto-profile recommendation based on file extensions in roots.</summary>
    public static string DetectAutoProfile(IReadOnlyList<string> roots)
    {
        var hasDisc = false;
        var hasCartridge = false;
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var f in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories).Take(200))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is ".chd" or ".iso" or ".bin" or ".cue" or ".gdi") hasDisc = true;
                if (ext is ".nes" or ".sfc" or ".gba" or ".nds" or ".z64" or ".gb") hasCartridge = true;
            }
        }
        return (hasDisc, hasCartridge) switch
        {
            (true, true) => "Gemischt (Disc + Cartridge): Konvertierung empfohlen",
            (true, false) => "Disc-basiert: CHD-Konvertierung empfohlen, aggressive Deduplizierung",
            (false, true) => "Cartridge-basiert: ZIP-Komprimierung, leichte Deduplizierung",
            _ => "Unbekannt: Keine erkannten ROM-Formate gefunden. Bitte überprüfen Sie die Root-Ordner."
        };
    }

    /// <summary>Build playtime tracker report from .lrtl files.</summary>
    public static string BuildPlaytimeReport(string directory)
    {
        var lrtlFiles = Directory.GetFiles(directory, "*.lrtl", SearchOption.AllDirectories);
        if (lrtlFiles.Length == 0) return "";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Spielzeit-Tracker: {lrtlFiles.Length} Dateien\n");
        sb.AppendLine("Hinweis: Es werden nur RetroArch .lrtl-Dateien unterstützt.\n");
        foreach (var f in lrtlFiles.Take(20))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var lines = File.ReadAllLines(f);
            sb.AppendLine($"  {name}: {lines.Length} Einträge");
        }
        return sb.ToString();
    }

    /// <summary>Build collection manager report grouped by genre.</summary>
    public static string BuildCollectionManagerReport(IReadOnlyList<RomCandidate> candidates)
    {
        var byConsole = candidates.GroupBy(c => ClassifyGenre(c.GameKey))
            .OrderByDescending(g => g.Count()).ToList();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Smart Collection Manager\n");
        sb.AppendLine($"Gesamt: {candidates.Count} ROMs\n");
        foreach (var g in byConsole)
            sb.AppendLine($"  {g.Key,-20} {g.Count(),5} ROMs");
        return sb.ToString();
    }

    /// <summary>Build command palette results report.</summary>
    public static string BuildCommandPaletteReport(string input,
        IReadOnlyList<(string key, string name, string shortcut, int score)> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Ergebnisse für \"{input}\":\n");
        foreach (var r in results)
            sb.AppendLine($"  {r.shortcut,-12} {r.name}");
        return sb.ToString();
    }

    /// <summary>Build parallel hashing configuration report.</summary>
    public static string BuildParallelHashingReport(int cores, int newThreads)
    {
        return $"Parallel-Hashing Konfiguration\n\n" +
            $"CPU-Kerne: {cores}\nThreads (neu): {newThreads}\n\n" +
            "Die Änderung wird beim nächsten Hash-Vorgang wirksam.";
    }
}

// ═══ RECORD TYPES ═══════════════════════════════════════════════════

public sealed record ConversionEstimateResult(
    long TotalSourceBytes, long EstimatedTargetBytes, long SavedBytes, double CompressionRatio,
    IReadOnlyList<ConversionDetail> Details,
    string Disclaimer = "Schätzwerte basieren auf statischen Durchschnitts-Kompressionsraten. Tatsächliche Ergebnisse können abweichen.");

public sealed record ConversionDetail(
    string FileName, string SourceFormat, string TargetFormat, long SourceBytes, long EstimatedBytes);

public sealed record HeatmapEntry(string Console, int Total, int Duplicates, double DuplicatePercent);
public sealed record DuplicateSourceEntry(string Directory, int Count);
public sealed record JunkReportEntry(string Tag, string Reason, string Level);

public sealed record DryRunCompareResult(
    IReadOnlyList<ReportEntry> OnlyInA, IReadOnlyList<ReportEntry> OnlyInB,
    IReadOnlyList<(ReportEntry left, ReportEntry right)> Different, int Identical);

public sealed record TrendSnapshot(
    DateTime Timestamp, int TotalFiles, long SizeBytes, int Verified, int Dupes, int Junk, int QualityScore);

public sealed record IntegrityEntry(string Hash, long Size, DateTime LastModified);
public sealed record IntegrityCheckResult(
    IReadOnlyList<string> Changed, IReadOnlyList<string> Missing,
    IReadOnlyList<string> Intact, bool BitRotRisk, string? Message = null);

public sealed record RomHeaderInfo(string Platform, string Format, string Details);
public sealed record ConfigDiffEntry(string Key, string SavedValue, string CurrentValue);
public sealed record DatDiffResult(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, int ModifiedCount, int UnchangedCount);
