using System.ComponentModel;

namespace RomCleanup.UI.Wpf.Models;

/// <summary>
/// Single log entry for the Protokoll tab.
/// Level drives color via LogLevelToBrushConverter in XAML.
/// </summary>
public sealed record LogEntry(string Text, string Level);

/// <summary>
/// Bindable file-extension filter checkbox item (UX-004).
/// Category is used for visual grouping in the UI.
/// </summary>
public sealed class ExtensionFilterItem : INotifyPropertyChanged
{
    public required string Extension { get; init; }
    public required string Category { get; init; }
    public required string ToolTip { get; init; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Bindable console filter checkbox item.
/// Category is used for visual grouping (Sony, Nintendo, Sega, Andere).
/// </summary>
public sealed class ConsoleFilterItem : INotifyPropertyChanged
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
