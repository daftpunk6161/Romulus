using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Romulus.UI.Wpf.ViewModels;

namespace Romulus.UI.Wpf.Views;

public partial class WizardView : UserControl
{
    public WizardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private bool _bindingsReattached;

    /// <summary>
    /// WPF defers binding activation when an element is parsed inside a Collapsed
    /// parent (Shell.ShowFirstRunWizard starts false → MainWindow renders WizardView
    /// with Visibility=Collapsed). When the wizard later becomes visible those
    /// bindings stay BindingStatus.Unattached and never produce values.
    /// We force activation by clearing and re-setting every binding the first time
    /// the wizard becomes visible.
    /// </summary>
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_bindingsReattached || e.NewValue is not true)
            return;
        _bindingsReattached = true;
        Dispatcher.BeginInvoke(new Action(() => ReattachBindings(this)),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static void ReattachBindings(DependencyObject root)
    {
        if (root is FrameworkElement fe)
            ReattachBindingsOn(fe);
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
            ReattachBindings(VisualTreeHelper.GetChild(root, i));
    }

    private static readonly DependencyProperty[] _commonBindableProps =
    {
        TextBlock.TextProperty,
        ContentControl.ContentProperty,
        UIElement.VisibilityProperty,
        UIElement.IsEnabledProperty,
        ButtonBase_CommandProperty(),
        AutomationProperties.NameProperty,
    };

    private static DependencyProperty ButtonBase_CommandProperty()
        => System.Windows.Controls.Primitives.ButtonBase.CommandProperty;

    private static void ReattachBindingsOn(FrameworkElement fe)
    {
        foreach (var dp in _commonBindableProps)
        {
            var be = BindingOperations.GetBindingExpression(fe, dp);
            if (be is null || be.Status == BindingStatus.Active)
                continue;
            var binding = be.ParentBinding;
            BindingOperations.ClearBinding(fe, dp);
            fe.SetBinding(dp, binding);
        }

        // Style DataTriggers (e.g. WizardStepXPanelStyle) are evaluated against
        // the styled element's DataContext at apply-time. When the wizard was
        // parsed inside a Collapsed parent these triggers also failed to evaluate.
        // Re-apply the Style to force WPF to re-run all triggers.
        if (fe.Style is { } style)
        {
            fe.Style = null;
            fe.Style = style;
        }
    }

    // GUI-095: Focus management on wizard step change (now on Shell)
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
            oldVm.Shell.PropertyChanged -= OnShellPropertyChanged;
        if (e.NewValue is MainViewModel newVm)
            newVm.Shell.PropertyChanged += OnShellPropertyChanged;
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.WizardStep))
            Dispatcher.BeginInvoke(() => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
    }
}
