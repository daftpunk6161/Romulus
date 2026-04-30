using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Romulus.Contracts.Ports;
using Romulus.UI.Wpf.Services;

namespace Romulus.UI.Wpf.ViewModels;

/// <summary>
/// T-W4-AUDIT-VIEWER-UI — WPF projection over <see cref="IAuditViewerBackingService"/>.
/// Sichtbarmachung des USP-Kerns Audit / Rollback. Read-only by design:
/// the VM only ever calls the read methods of the backing port and never
/// touches <c>AuditCsvParser</c> / <c>AuditCsvStore</c> / <c>AuditSigningService</c>.
/// Rollback is delegated to the supplied callback (which itself goes
/// through the existing rollback service) and is gated by a typed
/// danger-confirm token via <see cref="IDialogService.DangerConfirm"/>.
/// </summary>
public sealed class AuditViewerViewModel : ObservableObject
{
    private readonly IAuditViewerBackingService _backingService;
    private readonly IDialogService _dialog;
    private readonly ILocalizationService _loc;
    private readonly Func<string, bool>? _rollbackCallback;

    private string _auditRoot = string.Empty;
    private AuditRunSummary? _selectedRun;
    private AuditSidecarInfo? _selectedSidecar;
    private string? _lastError;
    private bool _isBusy;

    public AuditViewerViewModel(
        IAuditViewerBackingService backingService,
        IDialogService dialog,
        ILocalizationService loc,
        Func<string, bool>? rollbackCallback = null)
    {
        ArgumentNullException.ThrowIfNull(backingService);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(loc);
        _backingService = backingService;
        _dialog = dialog;
        _loc = loc;
        _rollbackCallback = rollbackCallback;

        RefreshCommand = new RelayCommand(Refresh);
        RollbackCommand = new RelayCommand(InvokeRollback, CanRollback);
    }

    /// <summary>Audit root folder (typically AppData\Romulus\audit-logs).</summary>
    public string AuditRoot
    {
        get => _auditRoot;
        set => SetProperty(ref _auditRoot, value);
    }

    /// <summary>Discovered audit runs (one per CSV under <see cref="AuditRoot"/>).</summary>
    public ObservableCollection<AuditRunSummary> Runs { get; } = [];

    /// <summary>Rows of the currently selected run.</summary>
    public ObservableCollection<AuditRowView> SelectedRunRows { get; } = [];

    public AuditRunSummary? SelectedRun
    {
        get => _selectedRun;
        set
        {
            if (!SetProperty(ref _selectedRun, value)) return;
            LoadSelectedRun();
            RollbackCommand.NotifyCanExecuteChanged();
        }
    }

    public AuditSidecarInfo? SelectedSidecar
    {
        get => _selectedSidecar;
        private set => SetProperty(ref _selectedSidecar, value);
    }

    public string? LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand RollbackCommand { get; }

    private void Refresh()
    {
        if (string.IsNullOrWhiteSpace(_auditRoot))
        {
            Runs.Clear();
            SelectedRun = null;
            return;
        }

        try
        {
            IsBusy = true;
            Runs.Clear();
            foreach (var run in _backingService.ListRuns(_auditRoot))
                Runs.Add(run);
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadSelectedRun()
    {
        SelectedRunRows.Clear();
        SelectedSidecar = null;

        if (_selectedRun is null) return;

        try
        {
            var page = _backingService.ReadRunRows(_selectedRun.AuditCsvPath);
            foreach (var row in page.Rows)
                SelectedRunRows.Add(row);

            SelectedSidecar = _backingService.ReadSidecar(_selectedRun.AuditCsvPath);
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private bool CanRollback()
        => _selectedRun is not null && _rollbackCallback is not null;

    private void InvokeRollback()
    {
        if (_selectedRun is null || _rollbackCallback is null) return;

        var title = _loc["Audit.Rollback.Title"];
        var message = _loc.Format("Audit.Rollback.Message", _selectedRun.FileName);
        var token = _loc["Audit.Rollback.ConfirmText"];
        var buttonLabel = _loc["Audit.Rollback.ButtonLabel"];

        if (!_dialog.DangerConfirm(title, message, token, buttonLabel))
            return;

        try
        {
            _rollbackCallback(_selectedRun.AuditCsvPath);
            // Re-list runs to reflect post-rollback state.
            Refresh();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }
}
