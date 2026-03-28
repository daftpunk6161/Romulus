using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RomCleanup.UI.Wpf.ViewModels;

namespace RomCleanup.UI.Wpf.Views;

public partial class LibraryReportView : UserControl
{
    public LibraryReportView()
    {
        InitializeComponent();

        btnRefreshReportPreview.Click += (_, _) => RefreshReportPreview();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && !string.IsNullOrEmpty(vm.LastReportPath) && File.Exists(vm.LastReportPath))
            RefreshReportPreview();
    }

    public async void RefreshReportPreview()
    {
        if (DataContext is not MainViewModel vm) return;

        if (string.IsNullOrEmpty(vm.LastReportPath) || !File.Exists(vm.LastReportPath))
        {
            // In DryRun mode, populate error summary from last run if available
            if (vm.Run.HasRunData)
                vm.PopulateErrorSummary();
            else
            {
                vm.ErrorSummaryItems.Clear();
                vm.ErrorSummaryItems.Add(new Models.UiError("GUI-NOREPORT", "Kein Report vorhanden.", Models.UiErrorSeverity.Info));
            }

            await EnsureWebView2Initialized(vm);
            webReportPreview.NavigateToString(
                "<html><body style='background:#1a1a2e;color:#888;font-family:Consolas;padding:16px'>" +
                "<p>HTML-Report nur im Execute-Modus (Mode=Move) verfügbar.</p>" +
                "<p style='margin-top:8px;font-size:0.9em;color:#666'>Die Fehler-Zusammenfassung oben zeigt bereits die Ergebnisse der Vorschau.</p>" +
                "</body></html>");
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(vm.LastReportPath);
            await EnsureWebView2Initialized(vm);
            webReportPreview.Source = new Uri(fullPath);
            vm.PopulateErrorSummary();
            vm.AddLog($"Report-Vorschau geladen: {Path.GetFileName(fullPath)}", "INFO");
        }
        catch (Exception ex)
        {
            vm.ErrorSummaryItems.Clear();
            vm.ErrorSummaryItems.Add(new Models.UiError("GUI-REPORTERR", ex.Message, Models.UiErrorSeverity.Error));
            vm.AddLog($"Report-Vorschau fehlgeschlagen: {ex.Message}", "ERROR");
        }
    }

    private async Task EnsureWebView2Initialized(MainViewModel vm)
    {
        if (webReportPreview.CoreWebView2 is not null) return;
        try
        {
            await webReportPreview.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            vm.AddLog($"WebView2-Runtime nicht verfügbar: {ex.Message}", "ERROR");
            webReportPreview.Visibility = Visibility.Collapsed;
            if (webReportPreview.Parent is Panel panel && !panel.Children.OfType<TextBlock>().Any(tb => tb.Name == "webView2Fallback"))
            {
                panel.Children.Remove(webReportPreview);
                var fallback = new TextBlock
                {
                    Text = "WebView2-Runtime nicht installiert.\nBericht kann über 'Bericht öffnen' im Browser angezeigt werden.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)FindResource("BrushWarning"),
                    FontSize = 12,
                    Margin = new Thickness(8),
                    Name = "webView2Fallback",
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(fallback);
            }
        }
    }
}
