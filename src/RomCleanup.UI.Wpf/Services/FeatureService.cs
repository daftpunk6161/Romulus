using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Reporting;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>
/// Backend logic for all WPF feature buttons.
/// Port of PowerShell modules: ConversionEstimate, JunkReport, DuplicateHeatmap,
/// CompletenessTracker, HeaderAnalysis, CollectionCsvExport, FilterBuilder, etc.
/// </summary>
public static class FeatureService
{
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
        var parts = path.Replace('\\', '/').Split('/');
        // Typically: root/ConsoleName/game.ext → take second-to-last dir
        return parts.Length >= 3 ? parts[^2] : "Unbekannt";
    }

    // ═══ DUPLICATE INSPECTOR ════════════════════════════════════════════
    // Port of WpfSlice.ReportPreview.ps1 - btnDuplicateInspector

    public static List<DuplicateSourceEntry> GetDuplicateInspector(string? auditPath)
    {
        if (string.IsNullOrEmpty(auditPath) || !File.Exists(auditPath))
            return [];

        var dirCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(auditPath).Skip(1)) // skip header
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
                if (snesTitle.Length > 0 && snesTitle.All(c => c >= 0x20 && c <= 0x7E))
                    return new RomHeaderInfo("SNES", "LoROM", $"Title={snesTitle}");
            }

            // SNES HiROM (header at 0xFFC0)
            if (header.Length >= 0x10000)
            {
                var snesTitle = Encoding.ASCII.GetString(header, 0xFFC0, 21).TrimEnd('\0', ' ');
                if (snesTitle.Length > 0 && snesTitle.All(c => c >= 0x20 && c <= 0x7E))
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

    private static readonly string BaselinePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RomCleanupRegionDedupe", "integrity-baseline.json");

    public static async Task<Dictionary<string, IntegrityEntry>> CreateBaseline(
        IReadOnlyList<string> filePaths, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var baseline = new Dictionary<string, IntegrityEntry>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        foreach (var path in filePaths)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(path)) continue;
            var fi = new FileInfo(path);
            progress?.Report($"Baseline: {++i}/{filePaths.Count} – {Path.GetFileName(path)}");
            var hash = await Task.Run(() => ComputeSha256(path), ct);
            baseline[path] = new IntegrityEntry(hash, fi.Length, fi.LastWriteTimeUtc);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
        File.WriteAllText(BaselinePath, JsonSerializer.Serialize(baseline, new JsonSerializerOptions { WriteIndented = true }));
        return baseline;
    }

    public static async Task<IntegrityCheckResult> CheckIntegrity(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(BaselinePath))
            return new IntegrityCheckResult([], [], [], false);

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

        foreach (var path in filePaths)
        {
            if (!File.Exists(path)) continue;
            var dest = Path.Combine(sessionDir, Path.GetFileName(path));
            File.Copy(path, dest, overwrite: false);
        }
        return sessionDir;
    }

    public static int CleanupOldBackups(string backupRoot, int retentionDays)
    {
        if (!Directory.Exists(backupRoot)) return 0;
        int removed = 0;
        var cutoff = DateTime.Now.AddDays(-retentionDays);
        foreach (var dir in Directory.GetDirectories(backupRoot))
        {
            if (Directory.GetCreationTime(dir) < cutoff)
            {
                Directory.Delete(dir, recursive: true);
                removed++;
            }
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
        var dataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "i18n");
        if (!Directory.Exists(dataDir))
            dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "i18n");

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

    // ═══ PORTABLE MODE CHECK ════════════════════════════════════════════
    // Port of PortableMode.ps1

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
            if (!File.Exists(c.MainPath)) continue;
            var fi = new FileInfo(c.MainPath);
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

        var results = new List<(string key, string name, string shortcut, int score)>();
        foreach (var cmd in PaletteCommands)
        {
            // Substring match = best score
            if (cmd.name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                cmd.key.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add((cmd.key, cmd.name, cmd.shortcut, 0));
                continue;
            }
            // Levenshtein fuzzy match
            var dist = LevenshteinDistance(query.ToLowerInvariant(), cmd.key.ToLowerInvariant());
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
                if (segments.Length == 2 && int.TryParse(segments[1], out var step) && step > 0 && value % step == 0)
                    return true;
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
                path = w.MainPath,
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
            if (lower.Contains(keyword))
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
            EXPOSE 5000
            ENV ASPNETCORE_URLS=http://+:5000
            ENTRYPOINT ["dotnet", "RomCleanup.Api.dll"]
            """;
    }

    public static string GenerateDockerCompose()
    {
        return """
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
        return $"""
            Windows Registry Editor Version 5.00

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_DryRun]
            @="ROM Cleanup – DryRun Scan"

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_DryRun\command]
            @="{exePath.Replace("\\", "\\\\")} --roots \"%V\" --mode DryRun"

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_Move]
            @="ROM Cleanup – Move Sort"

            [HKEY_CURRENT_USER\Software\Classes\Directory\shell\RomCleanup_Move\command]
            @="{exePath.Replace("\\", "\\\\")} --roots \"%V\" --mode Move"
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
            var docA = XDocument.Load(pathA);
            var docB = XDocument.Load(pathB);
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
            var doc = XDocument.Load(path);
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
            byte[] data = File.ReadAllBytes(path);
            // Verify iNES magic: 4E 45 53 1A
            if (data.Length < 16 ||
                data[0] != 0x4E || data[1] != 0x45 || data[2] != 0x53 || data[3] != 0x1A)
                return false;

            // Check if bytes 12-15 are dirty (non-zero)
            bool dirty = false;
            for (int i = 12; i <= 15; i++)
            {
                if (data[i] != 0x00)
                { dirty = true; break; }
            }

            if (!dirty) return false;

            // Create backup
            File.Copy(path, path + ".bak", overwrite: true);

            // Zero bytes 12-15
            data[12] = 0x00;
            data[13] = 0x00;
            data[14] = 0x00;
            data[15] = 0x00;
            File.WriteAllBytes(path, data);
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
}

// ═══ RECORD TYPES ═══════════════════════════════════════════════════

public sealed record ConversionEstimateResult(
    long TotalSourceBytes, long EstimatedTargetBytes, long SavedBytes, double CompressionRatio,
    IReadOnlyList<ConversionDetail> Details);

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
    IReadOnlyList<string> Intact, bool BitRotRisk);

public sealed record RomHeaderInfo(string Platform, string Format, string Details);
public sealed record ConfigDiffEntry(string Key, string SavedValue, string CurrentValue);
public sealed record DatDiffResult(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, int ModifiedCount, int UnchangedCount);
