using System.Collections;
using System.Windows;
using System.Windows.Controls;
using RomCleanup.UI.Wpf.Models;
using RomCleanup.UI.Wpf.ViewModels;

namespace RomCleanup.UI.Wpf.Views;

public partial class DecisionsView : UserControl
{
    public DecisionsView()
    {
        InitializeComponent();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var term = txtSearch.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(term))
        {
            treeDecisions.ItemsSource = vm.Run.DedupeGroupItems;
            return;
        }

        // Client-side filter on GameKey and Winner/Loser filenames
        var filtered = new List<DedupeGroupItem>();
        foreach (var group in vm.Run.DedupeGroupItems)
        {
            if (group.GameKey.Contains(term, StringComparison.OrdinalIgnoreCase)
                || group.Winner.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || group.Losers.Any(l => l.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                filtered.Add(group);
            }
        }

        treeDecisions.ItemsSource = filtered;
    }
}
