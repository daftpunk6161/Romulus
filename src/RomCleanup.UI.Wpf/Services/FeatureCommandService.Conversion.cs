using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Input;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Infrastructure.Reporting;
using RomCleanup.Infrastructure.Tools;
using RomCleanup.UI.Wpf.ViewModels;
namespace RomCleanup.UI.Wpf.Services;

public sealed partial class FeatureCommandService
{
    // ═══ KONVERTIERUNG & HASHING ════════════════════════════════════════

    private void ConversionPipeline()
    {
        if (_vm.LastCandidates.Count == 0)
        { _vm.AddLog("Erst einen Lauf starten.", "WARN"); return; }
        var est = FeatureService.GetConversionEstimate(_vm.LastCandidates);
        _vm.AddLog($"Konvertierungs-Pipeline: {est.Details.Count} Dateien, Ersparnis ~{FeatureService.FormatSize(est.SavedBytes)}", "INFO");
        _dialog.Info($"Konvertierungs-Pipeline bereit:\n\n{est.Details.Count} Dateien konvertierbar\n" +
            $"Geschätzte Ersparnis: {FeatureService.FormatSize(est.SavedBytes)}\n\n" +
            "Aktiviere 'Konvertierung' und starte einen Move-Lauf.", "Konvertierungs-Pipeline");
    }

    private void ConversionVerify()
    {
        var dir = _dialog.BrowseFolder("Konvertierte Dateien prüfen");
        if (dir is null) return;
        var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".chd" or ".rvz" or ".7z").ToList();
        var (passed, failed, missing) = FeatureService.VerifyConversions(files);
        _dialog.ShowText("Konvertierung verifizieren", $"Verifizierung: {dir}\n\n" +
            $"Bestanden: {passed}\nFehlgeschlagen: {failed}\nFehlend: {missing}\nGesamt: {files.Count}");
    }

    private void FormatPriority()
    {
        _dialog.ShowText("Format-Priorität", FeatureService.FormatFormatPriority());
    }

}
