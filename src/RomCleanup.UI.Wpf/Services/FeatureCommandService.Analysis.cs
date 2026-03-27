using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Paths;
using RomCleanup.Infrastructure.Reporting;
using RomCleanup.Infrastructure.Tools;
using RomCleanup.UI.Wpf.ViewModels;
namespace RomCleanup.UI.Wpf.Services;

public sealed partial class FeatureCommandService
{
    // ═══ ANALYSE & BERICHTE ═════════════════════════════════════════════

    private void JunkReport()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var report = FeatureService.BuildJunkReport(_vm.LastCandidates, _vm.AggressiveJunk);
        _dialog.ShowText("Junk-Bericht", report);
    }

    private void RomFilter()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Keine ROM-Daten geladen.", "WARN"); return; }
        var input = _dialog.ShowInputBox("Suchbegriff eingeben (Name, Region, Konsole, Format):", "ROM-Filter", "");
        if (string.IsNullOrWhiteSpace(input)) return;
        var results = FeatureService.SearchRomCollection(_vm.LastCandidates, input);
        var sb = new StringBuilder();
        sb.AppendLine($"ROM-Filter: \"{input}\" → {results.Count} Treffer\n");
        foreach (var r in results.Take(50))
            sb.AppendLine($"  {Path.GetFileName(r.MainPath),-40} [{r.Region}] {r.Extension} {r.Category}");
        if (results.Count > 50)
            sb.AppendLine($"\n  … und {results.Count - 50} weitere");
        _dialog.ShowText("ROM-Filter", sb.ToString());
    }

    private void MissingRom()
    {
        if (!_vm.UseDat || string.IsNullOrWhiteSpace(_vm.DatRoot))
        { _vm.AddLog("DAT muss aktiviert und konfiguriert sein.", "WARN"); return; }
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen DryRun mit aktiviertem DAT starten.", "WARN"); return; }

        var unverified = _vm.LastCandidates.Where(c => !c.DatMatch).ToList();
        if (unverified.Count == 0)
        { _dialog.Info("Alle ROMs haben einen DAT-Match. Keine fehlenden ROMs erkannt.", "Fehlende ROMs"); return; }

        var roots = _vm.Roots.Select(ArtifactPathResolver.NormalizeRoot).ToList();
        string GetSubDir(string filePath)
        {
            var full = Path.GetFullPath(filePath);
            var root = ArtifactPathResolver.FindContainingRoot(filePath, roots);
            if (root is not null)
            {
                var relative = full[(root.Length + 1)..];
                var sep = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                return sep > 0 ? relative[..sep] : "(Root)";
            }
            return Path.GetDirectoryName(filePath) ?? "(Unbekannt)";
        }
        var byDir = unverified.GroupBy(c => GetSubDir(c.MainPath)).OrderByDescending(g => g.Count()).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Fehlende ROMs (ohne DAT-Match)");
        sb.AppendLine(new string('═', 50));
        sb.AppendLine($"\n  Gesamt ohne DAT-Match: {unverified.Count} / {_vm.LastCandidates.Count}");
        sb.AppendLine($"\n  Nach Verzeichnis:\n");
        foreach (var g in byDir)
            sb.AppendLine($"    {g.Count(),5}  {g.Key}");
        _dialog.ShowText("Fehlende ROMs", sb.ToString());
    }

    private void HeaderAnalysis()
    {
        var path = _dialog.BrowseFile("ROM für Header-Analyse wählen",
            "ROM-Dateien (*.nes;*.sfc;*.gba;*.z64;*.v64;*.n64;*.smc)|*.nes;*.sfc;*.gba;*.z64;*.v64;*.n64;*.smc|Alle (*.*)|*.*");
        if (path is null) return;
        var header = FeatureService.AnalyzeHeader(path);
        if (header is null)
        { _vm.AddLog($"Header konnte nicht gelesen werden: {path}", "ERROR"); return; }
        _dialog.ShowText("Header-Analyse", $"Datei: {Path.GetFileName(path)}\n\n" +
            $"Plattform: {header.Platform}\nFormat: {header.Format}\nDetails: {header.Details}");
    }

    private void Completeness()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var verified = _vm.LastCandidates.Count(c => c.DatMatch);
        var total = _vm.LastCandidates.Count;
        var pct = total > 0 ? 100.0 * verified / total : 0;
        _dialog.ShowText("Vollständigkeit", $"Sammlungs-Vollständigkeit\n\n" +
            $"Verifizierte Dateien: {verified} / {total} ({pct:F1}%)\n\n" +
            $"Für eine DAT-basierte Vollständigkeitsanalyse\naktiviere DAT-Verifizierung und starte einen DryRun.");
    }

    private void DryRunCompare()
    {
        var fileA = _dialog.BrowseFile("Ersten DryRun-Report wählen", "CSV (*.csv)|*.csv|HTML (*.html)|*.html");
        var fileB = fileA is not null ? _dialog.BrowseFile("Zweiten DryRun-Report wählen", "CSV (*.csv)|*.csv|HTML (*.html)|*.html") : null;
        if (fileA is null || fileB is null) return;
        _vm.AddLog($"DryRun-Vergleich: {Path.GetFileName(fileA)} vs. {Path.GetFileName(fileB)}", "INFO");

        if (fileA.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
            fileB.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var setA = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var setB = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadLines(fileA).Skip(1))
                { var mainPath = FeatureService.ExtractFirstCsvField(line); if (!string.IsNullOrWhiteSpace(mainPath)) setA.Add(mainPath); }
                foreach (var line in File.ReadLines(fileB).Skip(1))
                { var mainPath = FeatureService.ExtractFirstCsvField(line); if (!string.IsNullOrWhiteSpace(mainPath)) setB.Add(mainPath); }

                var added = setB.Except(setA).ToList();
                var removed = setA.Except(setB).ToList();
                var same = setA.Intersect(setB).Count();

                var sb = new StringBuilder();
                sb.AppendLine("DryRun-Vergleich (CSV)");
                sb.AppendLine(new string('═', 50));
                sb.AppendLine($"\n  A: {Path.GetFileName(fileA)} ({setA.Count} Einträge)");
                sb.AppendLine($"  B: {Path.GetFileName(fileB)} ({setB.Count} Einträge)");
                sb.AppendLine($"\n  Gleich:     {same}");
                sb.AppendLine($"  Hinzugefügt: {added.Count}");
                sb.AppendLine($"  Entfernt:    {removed.Count}");
                if (added.Count > 0)
                {
                    sb.AppendLine($"\n  --- Hinzugefügt (erste {Math.Min(30, added.Count)}) ---");
                    foreach (var entry in added.Take(30)) sb.AppendLine($"    + {Path.GetFileName(entry)}");
                    if (added.Count > 30) sb.AppendLine($"    … und {added.Count - 30} weitere");
                }
                if (removed.Count > 0)
                {
                    sb.AppendLine($"\n  --- Entfernt (erste {Math.Min(30, removed.Count)}) ---");
                    foreach (var entry in removed.Take(30)) sb.AppendLine($"    - {Path.GetFileName(entry)}");
                    if (removed.Count > 30) sb.AppendLine($"    … und {removed.Count - 30} weitere");
                }
                _dialog.ShowText("DryRun-Vergleich", sb.ToString());
            }
            catch (Exception ex) { LogError("GUI-DRYRUN", $"DryRun-Vergleich Fehler: {ex.Message}"); }
        }
        else
        {
            _dialog.ShowText("DryRun-Vergleich", $"Vergleich:\n  A: {fileA}\n  B: {fileB}\n\n" +
                "Detaillierter Vergleich erfordert CSV-Reports.\nExportiere Reports als CSV und vergleiche erneut.");
        }
    }

}
