using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Export;
using RomCleanup.Infrastructure.Reporting;
using RomCleanup.Infrastructure.Tools;
using RomCleanup.UI.Wpf.ViewModels;
namespace RomCleanup.UI.Wpf.Services;

public sealed partial class FeatureCommandService
{
    // ═══ EXPORT & INTEGRATION ═══════════════════════════════════════════

    private void HtmlReport()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var path = _dialog.SaveFile("HTML-Report speichern", "HTML (*.html)|*.html", "report.html");
        if (path is null) return;
        var summary = new ReportSummary
        {
            Mode = _vm.DryRun ? "DryRun" : "Move",
            TotalFiles = _vm.LastCandidates.Count,
            KeepCount = _vm.LastDedupeGroups.Count,
            MoveCount = _vm.LastDedupeGroups.Sum(g => g.Losers.Count),
            JunkCount = _vm.LastCandidates.Count(c => c.Category == FileCategory.Junk),
            GroupCount = _vm.LastDedupeGroups.Count,
            Duration = TimeSpan.FromMilliseconds(_vm.LastRunResult?.DurationMs ?? 0)
        };
        var loserPaths = new HashSet<string>(
            _vm.LastDedupeGroups.SelectMany(g => g.Losers.Select(l => l.MainPath)),
            StringComparer.OrdinalIgnoreCase);
        var entries = _vm.LastCandidates.Select(c => new ReportEntry
        {
            GameKey = c.GameKey,
            Action = c.Category == FileCategory.Junk ? "JUNK" : loserPaths.Contains(c.MainPath) ? "MOVE" : "KEEP",
            Category = FeatureService.ToCategoryLabel(c.Category), Region = c.Region, FilePath = c.MainPath,
            FileName = Path.GetFileName(c.MainPath), Extension = c.Extension,
            SizeBytes = c.SizeBytes, RegionScore = c.RegionScore, FormatScore = c.FormatScore,
            VersionScore = (int)c.VersionScore, DatMatch = c.DatMatch
        }).ToList();
        try
        {
            ReportGenerator.WriteHtmlToFile(path, Path.GetDirectoryName(path) ?? ".", summary, entries);
            _vm.AddLog($"Report erstellt: {path} (Im Browser drucken → PDF)", "INFO");
            TryOpenWithShell(path, "Report");
        }
        catch (Exception ex) { LogError("GUI-REPORT", $"Report-Fehler: {ex.Message}"); }
    }

    private void LauncherIntegration()
    {
        var path = _dialog.SaveFile("RetroArch Playlist exportieren", "Playlist (*.lpl)|*.lpl", "RomCleanup.lpl");
        if (path is null) return;
        if (!TryLoadFrontendExportResult(
                FrontendExportTargets.RetroArch,
                path,
                Path.GetFileNameWithoutExtension(path),
                out var exportResult) ||
            exportResult is null)
        {
            return;
        }

        _vm.AddLog($"Playlist exportiert: {path} ({exportResult.GameCount} Eintraege)", "INFO");
    }

    private void DatImport()
    {
        var path = _dialog.BrowseFile("DAT-Datei importieren (ClrMamePro, RomVault, Logiqx)",
            "DAT (*.dat;*.xml)|*.dat;*.xml|Alle (*.*)|*.*");
        if (path is null) return;
        _vm.AddLog($"DAT-Import: {Path.GetFileName(path)}", "INFO");
        if (string.IsNullOrWhiteSpace(_vm.DatRoot))
        { _dialog.Error("DAT-Root ist nicht konfiguriert. Bitte zuerst den DAT-Root-Ordner setzen.", "DAT-Import"); return; }
        try
        {
            var safeName = Path.GetFileName(path);
            var targetPath = Path.GetFullPath(Path.Combine(_vm.DatRoot, safeName));
            if (!targetPath.StartsWith(Path.GetFullPath(_vm.DatRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            { _vm.AddLog("DAT-Import blockiert: Pfad außerhalb des DatRoot.", "ERROR"); return; }
            File.Copy(path, targetPath, overwrite: true);
            _vm.AddLog($"DAT importiert nach: {targetPath}", "INFO");
            _dialog.Info($"DAT erfolgreich importiert:\n\n  Quelle: {path}\n  Ziel: {targetPath}", "DAT-Import");
        }
        catch (Exception ex) { LogError("DAT-IMPORT", $"DAT-Import fehlgeschlagen: {ex.Message}"); }
    }

    private void ExportCollection()
    {
        var choice = _dialog.ShowInputBox(
            "Export-Format waehlen:\n\n" +
            "  1 - CSV (Sammlung)\n" +
            "  2 - Excel-XML (Sammlung)\n" +
            "  3 - CSV (nur Duplikate)\n" +
            "  4 - RetroArch Playlist\n" +
            "  5 - LaunchBox XML\n" +
            "  6 - EmulationStation gamelist\n" +
            "  7 - Playnite Bibliothek\n\n" +
            "Nummer eingeben:",
            "Sammlung exportieren");
        if (string.IsNullOrWhiteSpace(choice))
            return;

        switch (choice.Trim())
        {
            case "1":
                ExportFrontend(FrontendExportTargets.Csv, "CSV (*.csv)|*.csv", "sammlung.csv", "Romulus");
                break;
            case "2":
                ExportFrontend(FrontendExportTargets.Excel, "Excel XML (*.xml)|*.xml", "sammlung.xml", "Romulus");
                break;
            case "3":
                ExportDuplicateCsv();
                break;
            case "4":
                ExportFrontend(FrontendExportTargets.RetroArch, "Playlist (*.lpl)|*.lpl", "Romulus.lpl", "Romulus");
                break;
            case "5":
                ExportFrontend(FrontendExportTargets.LaunchBox, "LaunchBox XML (*.xml)|*.xml", "LaunchBox.xml", "Romulus");
                break;
            case "6":
                ExportFrontend(FrontendExportTargets.EmulationStation, "Ordner|*.*", "emulationstation", "Romulus");
                break;
            case "7":
                ExportFrontend(FrontendExportTargets.Playnite, "JSON (*.json)|*.json", "playnite-library.json", "Romulus");
                break;
            default:
                _vm.AddLog("Ungueltige Auswahl. Bitte 1 bis 7 eingeben.", "WARN");
                break;
        }
    }

    private void ExportFrontend(string frontend, string filter, string defaultFileName, string collectionName)
    {
        var path = _dialog.SaveFile("Export speichern", filter, defaultFileName);
        if (path is null)
            return;

        if (!TryLoadFrontendExportResult(frontend, path, collectionName, out var exportResult) || exportResult is null)
            return;

        _vm.AddLog($"Export erstellt: {path} ({exportResult.GameCount} Spiele, Quelle={exportResult.Source})", "INFO");
        _dialog.ShowText("Frontend-Export", FormatFrontendExportSummary(exportResult));
    }

    private void ExportDuplicateCsv()
    {
        if (_vm.LastDedupeGroups.Count == 0)
        {
            _vm.AddLog(_vm.Loc["Cmd.NoExportData"], "WARN");
            return;
        }

        var path = _dialog.SaveFile(_vm.Loc["Cmd.DupeExportTitle"], _vm.Loc["Cmd.FilterCsv"], "duplikate.csv");
        if (path is null)
            return;

        var losers = _vm.LastDedupeGroups.SelectMany(static group => group.Losers).ToList();
        var dupeCsv = FeatureService.ExportCollectionCsv(losers);
        File.WriteAllText(path, dupeCsv, Encoding.UTF8);
        _vm.AddLog(_vm.Loc.Format("Cmd.DupeExported", path, losers.Count), "INFO");
    }

}
