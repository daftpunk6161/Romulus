using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using RomCleanup.UI.Wpf.ViewModels;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>
/// Manages system tray icon lifecycle: creation, context menu, minimize-to-tray, disposal.
/// Extracted from MainWindow.xaml.cs (RF-007).
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly Window _window;
    private readonly MainViewModel _vm;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private IntPtr _trayIconHandle;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    public TrayService(Window window, MainViewModel vm)
    {
        _window = window;
        _vm = vm;
    }

    public bool IsActive => _trayIcon is not null;

    /// <summary>
    /// Toggle tray mode. If already active, minimizes to tray; otherwise creates tray icon and minimizes.
    /// </summary>
    public void Toggle()
    {
        if (_trayIcon is not null)
        {
            _window.WindowState = WindowState.Minimized;
            return;
        }

        IntPtr hicon;
        using (var bitmap = new Bitmap(32, 32))
        {
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(40, 100, 210));
                using var font = new Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
                using var brush = new SolidBrush(Color.White);
                g.DrawString("R", font, brush, 2, 2);
            }
            hicon = bitmap.GetHicon();
        }

        _trayIconHandle = hicon;
        var icon = Icon.FromHandle(hicon);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Anzeigen", null, (_, _) => RestoreWindow());
        menu.Items.Add("DryRun starten", null, (_, _) =>
        {
            _window.Dispatcher.InvokeAsync(() =>
            {
                _vm.DryRun = true;
                _vm.RunCommand.Execute(null);
            });
        });
        menu.Items.Add("Status", null, (_, _) =>
        {
            _window.Dispatcher.InvokeAsync(() =>
            {
                var status = _vm.IsBusy ? "Lauf aktiv..." : "Bereit";
                _trayIcon?.ShowBalloonTip(3000, "RomCleanup Status", status, System.Windows.Forms.ToolTipIcon.Info);
            });
        });
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) =>
        {
            _window.Dispatcher.InvokeAsync(() => _window.Close());
        });

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "RomCleanup",
            Visible = true,
            ContextMenuStrip = menu
        };

        _trayIcon.DoubleClick += (_, _) => RestoreWindow();

        _window.StateChanged -= OnWindowStateChanged;
        _window.StateChanged += OnWindowStateChanged;

        _trayIcon.ShowBalloonTip(2000, "RomCleanup", "In den System-Tray minimiert.", System.Windows.Forms.ToolTipIcon.Info);
        _window.WindowState = WindowState.Minimized;
    }

    public void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_window.WindowState == WindowState.Minimized && _trayIcon is not null)
        {
            _window.Hide();
            _trayIcon.ShowBalloonTip(2000, "RomCleanup", "Anwendung läuft im Hintergrund.", System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    public void Dispose()
    {
        _window.StateChanged -= OnWindowStateChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;
        if (_trayIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_trayIconHandle);
            _trayIconHandle = IntPtr.Zero;
        }
    }

    private void RestoreWindow()
    {
        _window.Dispatcher.InvokeAsync(() =>
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }
}
