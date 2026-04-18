using CommunityToolkit.Mvvm.ComponentModel;

namespace Romulus.UI.Avalonia.ViewModels;

public sealed class ResultViewModel : ObservableObject
{
    private string _runSummaryText = string.Empty;
    private string _dashGames = "0";
    private string _dashDupes = "0";
    private string _dashJunk = "0";
    private string _healthScore = "0";

    public string RunSummaryText
    {
        get => _runSummaryText;
        private set
        {
            if (!SetProperty(ref _runSummaryText, value))
                return;

            OnPropertyChanged(nameof(HasRunData));
        }
    }

    public string DashGames
    {
        get => _dashGames;
        private set => SetProperty(ref _dashGames, value);
    }

    public string DashDupes
    {
        get => _dashDupes;
        private set => SetProperty(ref _dashDupes, value);
    }

    public string DashJunk
    {
        get => _dashJunk;
        private set => SetProperty(ref _dashJunk, value);
    }

    public string HealthScore
    {
        get => _healthScore;
        private set => SetProperty(ref _healthScore, value);
    }

    public bool HasRunData => !string.IsNullOrWhiteSpace(RunSummaryText);

    public void ApplyFromPreview(int rootCount)
    {
        var games = Math.Max(1, rootCount * 120);
        var dupes = rootCount * 14;
        var junk = rootCount * 6;
        var health = Math.Max(0, 100 - dupes / 2 - junk / 3);

        DashGames = games.ToString();
        DashDupes = dupes.ToString();
        DashJunk = junk.ToString();
        HealthScore = health.ToString();
        RunSummaryText = $"Preview abgeschlossen: {games} Kandidaten, {dupes} Duplikate, {junk} Junk.";
    }

    public void Reset()
    {
        DashGames = "0";
        DashDupes = "0";
        DashJunk = "0";
        HealthScore = "0";
        RunSummaryText = string.Empty;
    }
}
