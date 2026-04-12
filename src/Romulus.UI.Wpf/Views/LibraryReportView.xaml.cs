using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Romulus.UI.Wpf.ViewModels;

namespace Romulus.UI.Wpf.Views;

public partial class LibraryReportView : UserControl
{
    private bool _webViewFallbackActivated;

    public LibraryReportView()
    {
        InitializeComponent();

        btnRefreshReportPreview.Click += async (_, _) => await RefreshReportPreviewAsync();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Run.HasRunData)
        {
            await RefreshReportPreviewAsync();
        }
    }

    internal static bool TryNormalizeReportPath(string? value, out string normalizedPath)
        => MainViewModel.TryNormalizeReportPath(value, out normalizedPath);

    public async Task RefreshReportPreviewAsync()
    {
        if (DataContext is not MainViewModel vm) return;

        var preview = vm.BuildReportPreviewResult();

        await EnsureWebView2Initialized(vm);
        if (webReportPreview.CoreWebView2 is null)
            return;

        if (!string.IsNullOrWhiteSpace(preview.ReportFilePath))
        {
            webReportPreview.Source = new Uri(preview.ReportFilePath);
            return;
        }

        webReportPreview.NavigateToString(preview.InlineHtml ?? "<html><body></body></html>");
    }

    private async Task EnsureWebView2Initialized(MainViewModel vm)
    {
        if (_webViewFallbackActivated || webReportPreview.CoreWebView2 is not null) return;

        try
        {
            await webReportPreview.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            var reason = ex.GetType().Name.Contains("RuntimeNotFound", StringComparison.OrdinalIgnoreCase)
                ? "WebView2-Runtime nicht verfügbar."
                : "Report-Vorschau konnte nicht initialisiert werden.";

            vm.AddLog($"{reason} ({ex.GetType().Name})", "WARN");
            ActivateWebViewFallback();
        }
    }

    private void ActivateWebViewFallback()
    {
        if (_webViewFallbackActivated)
            return;

        _webViewFallbackActivated = true;
        webReportPreview.Visibility = Visibility.Collapsed;

        if (webReportPreview is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // best effort: fallback mode remains active even if disposal fails
            }
        }

        if (webReportPreview.Parent is not Panel panel)
            return;

        if (panel.Children.OfType<TextBlock>().Any(tb => tb.Name == "webView2Fallback"))
            return;

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
