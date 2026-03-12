using System.IO;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace RomCleanup.UI.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogFatalException(e.Exception);
        MessageBox.Show(
            $"Ein unerwarteter Fehler ist aufgetreten:\n\n{e.Exception.Message}\n\nDetails wurden in crash.log gespeichert.",
            "RomCleanup – Fehler",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogFatalException(ex);
    }

    private static void LogFatalException(Exception ex)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RomCleanupRegionDedupe");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "crash.log");
            File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] {ex}\n\n");
        }
        catch { /* best effort — don't throw during crash handling */ }
    }
}
