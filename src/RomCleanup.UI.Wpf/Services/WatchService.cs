using System.IO;
using System.Windows.Threading;

namespace RomCleanup.UI.Wpf.Services;

/// <summary>
/// Manages FileSystemWatchers for watch-mode auto-DryRun.
/// Extracted from MainWindow.xaml.cs (RF-006).
/// Handles watcher lifecycle, debounce timer, and max-wait enforcement.
/// </summary>
public sealed class WatchService : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private DispatcherTimer? _debounceTimer;
    private DateTime _firstChangeUtc = DateTime.MaxValue;
    private bool _pendingWhileBusy;
    private bool _disposed;

    /// <summary>Raised when debounce fires and a DryRun should be triggered.</summary>
    public event Action? RunTriggered;

    /// <summary>Whether any watchers are currently active.</summary>
    public bool IsActive => _watchers.Count > 0;

    /// <summary>Whether a change was queued while busy.</summary>
    public bool HasPending => _pendingWhileBusy;

    /// <summary>
    /// Start watching the given root directories.
    /// If watchers are already active, stops them first (toggle behavior).
    /// Returns the number of successfully created watchers.
    /// </summary>
    public int Start(IReadOnlyList<string> roots)
    {
        if (_watchers.Count > 0)
        {
            Stop();
            return 0; // Toggle off
        }

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            try
            {
                var watcher = new FileSystemWatcher(root)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;
                watcher.Deleted += OnFileChanged;
                watcher.Renamed += (s, e) => OnFileChanged(s, e);
                _watchers.Add(watcher);
            }
            catch
            {
                // Individual watcher failure is non-fatal
            }
        }

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _debounceTimer.Tick += OnDebounceTick;

        return _watchers.Count;
    }

    /// <summary>Stop all watchers and dispose resources.</summary>
    public void Stop()
    {
        _debounceTimer?.Stop();
        _debounceTimer = null;
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
        _firstChangeUtc = DateTime.MaxValue;
        _pendingWhileBusy = false;
    }

    /// <summary>
    /// Notify the watch service that a run has completed and
    /// flush any pending changes queued while the run was active.
    /// </summary>
    public void FlushPendingIfNeeded()
    {
        if (_pendingWhileBusy && _watchers.Count > 0)
        {
            _pendingWhileBusy = false;
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Must marshal to UI thread for DispatcherTimer
        Dispatcher.CurrentDispatcher.InvokeAsync(() =>
        {
            if (_firstChangeUtc == DateTime.MaxValue)
                _firstChangeUtc = DateTime.UtcNow;

            // Max-wait 30s: fire immediately instead of resetting debounce
            if ((DateTime.UtcNow - _firstChangeUtc).TotalSeconds >= 30)
            {
                _debounceTimer?.Stop();
                _firstChangeUtc = DateTime.MaxValue;
                OnDebounceTick(null, EventArgs.Empty);
                return;
            }

            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        });
    }

    /// <summary>Predicate to check if a run is currently active. Set by consumer.</summary>
    public Func<bool>? IsBusyCheck { get; set; }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();
        _firstChangeUtc = DateTime.MaxValue;

        if (IsBusyCheck?.Invoke() == true)
        {
            _pendingWhileBusy = true;
            return;
        }

        RunTriggered?.Invoke();
    }

    /// <summary>Mark that a change occurred while a run was busy.</summary>
    public void MarkPendingWhileBusy() => _pendingWhileBusy = true;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
