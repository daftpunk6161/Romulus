using System.IO;
using System.Windows;
using System.Windows.Controls;
using RomCleanup.Infrastructure.Conversion;

namespace RomCleanup.UI.Wpf.Views;

public partial class ToolsConversionView : UserControl
{
    public ToolsConversionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadRegistry();
    }

    private void LoadRegistry()
    {
        var registryPath = FindDataFile("conversion-registry.json");
        var consolesPath = FindDataFile("consoles.json");

        if (registryPath is null || consolesPath is null)
        {
            EmptyState.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var loader = new ConversionRegistryLoader(registryPath, consolesPath);
            var capabilities = loader.GetCapabilities();
            var items = capabilities.Select(c => new ConversionRow(
                c.SourceExtension,
                c.TargetExtension,
                c.Tool.ToolName,
                c.Command,
                c.Lossless ? "✓" : "✗",
                c.Cost,
                c.ApplicableConsoles is not null ? string.Join(", ", c.ApplicableConsoles) : "Alle"
            )).ToList();

            CapabilityGrid.ItemsSource = items;
            EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            EmptyState.Visibility = Visibility.Visible;
        }
    }

    private static string? FindDataFile(string name)
    {
        // Walk up from assembly directory to find data/
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, "data", name);
            if (File.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent is null || parent == dir) break;
            dir = parent;
        }
        return null;
    }
}

internal sealed record ConversionRow(
    string Source,
    string Target,
    string Tool,
    string Command,
    string LosslessDisplay,
    int Cost,
    string Consoles);
