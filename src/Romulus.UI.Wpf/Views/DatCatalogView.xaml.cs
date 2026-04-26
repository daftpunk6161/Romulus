using System.Windows;
using System.Windows.Controls;
using Romulus.UI.Wpf.ViewModels;

namespace Romulus.UI.Wpf.Views;

public partial class DatCatalogView : UserControl
{
    private string _lastScannedDatRoot = "";

    public DatCatalogView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private async void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || DataContext is not DatCatalogViewModel vm || vm.IsBusy)
            return;

        // Only auto-load if DatRoot has been configured (settings loaded).
        // Before LoadInitialSettings, DatRoot is "" and we'd get 0 local files.
        var datRoot = vm.GetDatRoot();
        if (string.IsNullOrWhiteSpace(datRoot))
            return;

        // Refresh on first show OR whenever DatRoot changed since the last scan
        // (e.g. user updated the path in Setup and switched back to DAT-Verwaltung).
        if (string.Equals(datRoot, _lastScannedDatRoot, StringComparison.OrdinalIgnoreCase))
            return;

        _lastScannedDatRoot = datRoot;
        await vm.RefreshCommand.ExecuteAsync(null);
    }
}
