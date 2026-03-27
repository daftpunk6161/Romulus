using System.Windows.Controls;
using RomCleanup.UI.Wpf.Helpers;
using RomCleanup.UI.Wpf.ViewModels;

namespace RomCleanup.UI.Wpf.Views;

public partial class ConfigOptionsView : UserControl
{
    public ConfigOptionsView()
    {
        InitializeComponent();
        listRoots.DragEnter += RootsDragDropHelper.OnDragEnter;
        listRoots.Drop += (s, e) => RootsDragDropHelper.OnDrop(s, e, DataContext as MainViewModel);
    }
}
