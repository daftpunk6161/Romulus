using System.Windows.Controls;

namespace Romulus.UI.Wpf.Views;

/// <summary>
/// T-W4-REVIEW-INBOX (W5) — code-behind. No business logic (project rule:
/// GUI/Code-Behind frei von Businesslogik). Pure UserControl shell; all
/// behavior lives in <c>ReviewInboxViewModel</c> and the
/// <c>ReviewInboxProjection</c> Single Source of Truth.
/// </summary>
public partial class ReviewInboxView : UserControl
{
    public ReviewInboxView()
    {
        InitializeComponent();
    }
}
