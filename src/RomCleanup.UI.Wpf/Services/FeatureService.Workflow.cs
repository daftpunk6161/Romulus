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

public static partial class FeatureService
{

    // ═══ SORT TEMPLATES ═════════════════════════════════════════════════
    // Port of SortTemplates.ps1

    public static Dictionary<string, string> GetSortTemplates()
    {
        var ext = UiLookupData.Instance.SortTemplates;
        if (ext.Count > 0) return new(ext);
        return new()
        {
            ["RetroArch"] = "{console}/{filename}",
            ["EmulationStation"] = "roms/{console_lower}/{filename}",
            ["LaunchBox"] = "Games/{console}/{filename}",
            ["Batocera"] = "share/roms/{console_lower}/{filename}",
            ["Flat"] = "{filename}"
        };
    }


    // ═══ CRON TESTER ═════════════════════════════════════════════════════
    // Port of CronTester (formerly SchedulerAdvanced.ps1)

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


    internal static bool CronFieldMatch(string field, int value)
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


    // ═══ CSV FIELD EXTRACTION ══════════════════════════════════════════

    /// <summary>
    /// Extract the first field from a CSV line, auto-detecting delimiter (semicolon or comma).
    /// Handles RFC 4180 quoted fields.
    /// </summary>
    internal static string ExtractFirstCsvField(string line)
    {
        if (string.IsNullOrEmpty(line)) return "";
        if (line[0] == '"')
        {
            // Quoted field — find closing quote
            for (int i = 1; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    { i++; continue; } // escaped quote
                    return line[1..i].Replace("\"\"", "\"");
                }
            }
            return line[1..].Replace("\"\"", "\"");
        }
        // Unquoted — split on first semicolon or comma
        var idxSemi = line.IndexOf(';');
        var idxComma = line.IndexOf(',');
        int idx;
        if (idxSemi < 0) idx = idxComma;
        else if (idxComma < 0) idx = idxSemi;
        else idx = Math.Min(idxSemi, idxComma);
        return idx >= 0 ? line[..idx] : line;
    }

}
