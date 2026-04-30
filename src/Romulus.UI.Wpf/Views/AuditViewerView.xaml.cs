using System.Windows.Controls;

namespace Romulus.UI.Wpf.Views;

/// <summary>
/// T-W4-AUDIT-VIEWER-UI — code-behind. No business logic (project rule:
/// GUI/Code-Behind frei von Businesslogik). Pure UserControl shell; all
/// behavior lives in <c>AuditViewerViewModel</c> and the read-only
/// <c>IAuditViewerBackingService</c> port.
/// </summary>
public partial class AuditViewerView : UserControl
{
    public AuditViewerView()
    {
        InitializeComponent();
    }
}
