using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>
/// Dialog helper service: folder pickers, file pickers, confirmation dialogs.
/// Port of WpfHost.ps1 dialog functions and WpfSlice.AdvancedFeatures.ps1 Show-WpfTextInputDialog.
/// All methods are thread-safe: calls from background threads are marshalled to the UI dispatcher.
/// </summary>
public static class DialogService
{
    /// <summary>Show a folder browser dialog and return the selected path, or null.</summary>
    public static string? BrowseFolder(string title = "Ordner auswählen", Window? owner = null)
    {
        return InvokeOnUiThread(() =>
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                Multiselect = false
            };
            var effectiveOwner = owner ?? GetMainWindow();
            return dialog.ShowDialog(effectiveOwner) == true ? dialog.FolderName : null;
        });
    }

    /// <summary>Show a file open dialog and return the selected path, or null.</summary>
    public static string? BrowseFile(string title = "Datei auswählen", string filter = "Alle Dateien|*.*", Window? owner = null)
    {
        return InvokeOnUiThread(() =>
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter
            };
            var effectiveOwner = owner ?? GetMainWindow();
            return dialog.ShowDialog(effectiveOwner) == true ? dialog.FileName : null;
        });
    }

    /// <summary>Show a file save dialog and return the selected path, or null.</summary>
    public static string? SaveFile(string title = "Speichern unter", string filter = "Alle Dateien|*.*", string? defaultFileName = null, Window? owner = null)
    {
        return InvokeOnUiThread(() =>
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = defaultFileName ?? ""
            };
            var effectiveOwner = owner ?? GetMainWindow();
            return dialog.ShowDialog(effectiveOwner) == true ? dialog.FileName : null;
        });
    }

    /// <summary>Show a confirmation dialog. Returns true if user confirmed.</summary>
    public static bool Confirm(string message, string title = "Bestätigung", Window? owner = null)
    {
        return InvokeOnUiThread(() =>
        {
            var effectiveOwner = owner ?? GetMainWindow();
            var result = MessageBox.Show(
                effectiveOwner,
                message, title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        });
    }

    /// <summary>Show an info message.</summary>
    public static void Info(string message, string title = "Information", Window? owner = null)
    {
        InvokeOnUiThread(() =>
        {
            var effectiveOwner = owner ?? GetMainWindow();
            MessageBox.Show(
                effectiveOwner,
                message, title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true; // dummy return for InvokeOnUiThread<T>
        });
    }

    /// <summary>Show an error message.</summary>
    public static void Error(string message, string title = "Fehler", Window? owner = null)
    {
        InvokeOnUiThread(() =>
        {
            var effectiveOwner = owner ?? GetMainWindow();
            MessageBox.Show(
                effectiveOwner,
                message, title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return true; // dummy return for InvokeOnUiThread<T>
        });
    }

    /// <summary>
    /// Marshal a function call to the WPF UI thread if the current thread is not the dispatcher thread.
    /// If already on the UI thread, executes directly.
    /// </summary>
    private static T InvokeOnUiThread<T>(Func<T> action)
    {
        var app = Application.Current;
        if (app is null)
            return action();

        var dispatcher = app.Dispatcher;
        if (dispatcher.CheckAccess())
            return action();

        return dispatcher.Invoke(action);
    }

    /// <summary>
    /// Safely retrieve the main window, returning null if not available.
    /// Must be called on the UI thread.
    /// </summary>
    private static Window? GetMainWindow()
    {
        try { return Application.Current?.MainWindow; }
        catch { return null; }
    }
}
