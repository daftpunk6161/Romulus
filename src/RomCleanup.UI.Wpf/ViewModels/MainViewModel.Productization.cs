using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using RomCleanup.Contracts;
using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Orchestration;
using RomCleanup.Infrastructure.Profiles;
using RomCleanup.Infrastructure.Workflow;
using RomCleanup.UI.Wpf.Services;

namespace RomCleanup.UI.Wpf.ViewModels;

public sealed partial class MainViewModel
{
    private RunProfileService _runProfileService = null!;
    private RunConfigurationMaterializer _runConfigurationMaterializer = null!;
    private bool _suppressRunConfigurationSelectionApply;
    private bool _applyingRunConfigurationSelection;

    public ObservableCollection<RunProfileSummary> AvailableRunProfiles { get; } = [];
    public ObservableCollection<WorkflowScenarioDefinition> AvailableWorkflows { get; } = [];

    private string? _selectedWorkflowScenarioId;
    public string? SelectedWorkflowScenarioId
    {
        get => _selectedWorkflowScenarioId;
        set
        {
            var normalized = NormalizeSelection(value);
            if (!SetProperty(ref _selectedWorkflowScenarioId, normalized))
                return;

            OnRunConfigurationSelectionChanged();
        }
    }

    private string? _selectedRunProfileId;
    public string? SelectedRunProfileId
    {
        get => _selectedRunProfileId;
        set
        {
            var normalized = NormalizeSelection(value);
            if (!SetProperty(ref _selectedRunProfileId, normalized))
                return;

            OnRunConfigurationSelectionChanged();
        }
    }

    public bool HasSelectedWorkflow => TryGetSelectedWorkflow() is not null;
    public bool HasSelectedRunProfile => TryGetSelectedProfileSummary() is not null;

    public string SelectedWorkflowName => TryGetSelectedWorkflow()?.Name ?? "Kein Workflow";
    public string SelectedWorkflowDescription => TryGetSelectedWorkflow()?.Description ?? "Kein gefuehrtes Szenario ausgewaehlt.";
    public string SelectedWorkflowStepsSummary => TryGetSelectedWorkflow() is { Steps.Length: > 0 } workflow
        ? string.Join(" -> ", workflow.Steps)
        : "Keine Workflow-Schritte aktiv.";

    public string SelectedRunProfileName => TryGetSelectedProfileSummary()?.Name ?? "Kein Profil";
    public string SelectedRunProfileDescription => TryGetSelectedProfileSummary()?.Description ?? "Kein Profil ausgewaehlt.";

    public string RunConfigurationSelectionSummary
        => $"{SelectedWorkflowName} | {SelectedRunProfileName}";

    public bool CanAdvanceWizard => Shell.WizardStep switch
    {
        0 => Roots.Count > 0,
        1 => GetPreferredRegions().Length > 0,
        _ => true
    };

    internal RunProfileService RunProfileService => _runProfileService;
    internal RunConfigurationMaterializer RunConfigurationMaterializer => _runConfigurationMaterializer;

    private void InitializeRunConfigurationServices(
        RunProfileService? runProfileService,
        RunConfigurationMaterializer? runConfigurationMaterializer)
    {
        var dataDir = FeatureService.ResolveDataDirectory()
                      ?? RunEnvironmentBuilder.ResolveDataDir();

        _runProfileService = runProfileService
            ?? new RunProfileService(new JsonRunProfileStore(), dataDir);
        _runConfigurationMaterializer = runConfigurationMaterializer
            ?? new RunConfigurationMaterializer(
                new RunConfigurationResolver(_runProfileService),
                new RunOptionsFactory());

        RefreshRunConfigurationCatalogs();
    }

    internal void RefreshRunConfigurationCatalogs()
    {
        try
        {
            var profiles = _runProfileService.ListAsync().GetAwaiter().GetResult();
            AvailableRunProfiles.Clear();
            foreach (var profile in profiles)
                AvailableRunProfiles.Add(profile);

            AvailableWorkflows.Clear();
            foreach (var workflow in WorkflowScenarioCatalog.List()
                         .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                AvailableWorkflows.Add(workflow);
            }

            OnRunConfigurationSelectionMetadataChanged();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
        {
            AddLog($"[Profiles] Katalog konnte nicht geladen werden: {ex.Message}", "WARN");
        }
    }

    internal void RestoreRunConfigurationSelection(string? workflowScenarioId, string? profileId)
    {
        _suppressRunConfigurationSelectionApply = true;
        try
        {
            SelectedWorkflowScenarioId = workflowScenarioId;
            SelectedRunProfileId = profileId;
        }
        finally
        {
            _suppressRunConfigurationSelectionApply = false;
        }

        OnRunConfigurationSelectionMetadataChanged();
    }

    internal RunConfigurationDraft BuildCurrentRunConfigurationDraft(bool includeSelections = true)
    {
        return new RunConfigurationDraft
        {
            Roots = Roots.ToArray(),
            Mode = DryRun ? RunConstants.ModeDryRun : RunConstants.ModeMove,
            WorkflowScenarioId = includeSelections ? NormalizeSelection(SelectedWorkflowScenarioId) : null,
            ProfileId = includeSelections ? NormalizeSelection(SelectedRunProfileId) : null,
            PreferRegions = GetPreferredRegions(),
            Extensions = BuildSelectedExtensionsForRunConfiguration(),
            RemoveJunk = RemoveJunk,
            OnlyGames = OnlyGames,
            KeepUnknownWhenOnlyGames = KeepUnknownWhenOnlyGames,
            AggressiveJunk = AggressiveJunk,
            SortConsole = SortConsole,
            EnableDat = UseDat,
            EnableDatAudit = UseDat && EnableDatAudit,
            EnableDatRename = UseDat && EnableDatRename,
            DatRoot = string.IsNullOrWhiteSpace(DatRoot) ? null : DatRoot,
            HashType = string.IsNullOrWhiteSpace(DatHashType) ? RunConstants.DefaultHashType : DatHashType,
            ConvertFormat = (ConvertEnabled || ConvertOnly) ? "auto" : null,
            ConvertOnly = ConvertOnly,
            ApproveReviews = ApproveReviews,
            ConflictPolicy = ConflictPolicy.ToString(),
            TrashRoot = string.IsNullOrWhiteSpace(TrashRoot) ? null : TrashRoot
        };
    }

    internal RunConfigurationExplicitness BuildCurrentRunConfigurationExplicitness()
    {
        return new RunConfigurationExplicitness
        {
            Mode = true,
            PreferRegions = true,
            Extensions = true,
            RemoveJunk = true,
            OnlyGames = true,
            KeepUnknownWhenOnlyGames = true,
            AggressiveJunk = true,
            SortConsole = true,
            EnableDat = true,
            EnableDatAudit = true,
            EnableDatRename = true,
            DatRoot = true,
            HashType = true,
            ConvertFormat = true,
            ConvertOnly = true,
            ApproveReviews = true,
            ConflictPolicy = true,
            TrashRoot = true
        };
    }

    internal RunProfileDocument BuildCurrentRunProfileDocument(string id, string name, string? description)
    {
        var draft = BuildCurrentRunConfigurationDraft(includeSelections: false);
        var settings = new RunProfileSettings
        {
            PreferRegions = draft.PreferRegions,
            Extensions = draft.Extensions,
            RemoveJunk = draft.RemoveJunk,
            OnlyGames = draft.OnlyGames,
            KeepUnknownWhenOnlyGames = draft.KeepUnknownWhenOnlyGames,
            AggressiveJunk = draft.AggressiveJunk,
            SortConsole = draft.SortConsole,
            EnableDat = draft.EnableDat,
            EnableDatAudit = draft.EnableDatAudit,
            EnableDatRename = draft.EnableDatRename,
            DatRoot = draft.DatRoot,
            HashType = draft.HashType,
            ConvertFormat = draft.ConvertFormat,
            ConvertOnly = draft.ConvertOnly,
            ApproveReviews = draft.ApproveReviews,
            ConflictPolicy = draft.ConflictPolicy,
            TrashRoot = draft.TrashRoot,
            Mode = draft.Mode
        };

        return new RunProfileDocument
        {
            Version = 1,
            Id = id.Trim(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            BuiltIn = false,
            Tags = BuildProfileTags(),
            WorkflowScenarioId = NormalizeSelection(SelectedWorkflowScenarioId),
            Settings = settings
        };
    }

    internal IReadOnlyDictionary<string, string> GetCurrentRunConfigurationMap()
        => BuildRunConfigurationMap(BuildCurrentRunConfigurationDraft());

    internal static IReadOnlyDictionary<string, string> BuildRunConfigurationMap(RunConfigurationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["workflowScenarioId"] = draft.WorkflowScenarioId ?? string.Empty,
            ["profileId"] = draft.ProfileId ?? string.Empty,
            ["mode"] = draft.Mode ?? string.Empty,
            ["preferRegions"] = string.Join(",", draft.PreferRegions ?? Array.Empty<string>()),
            ["extensions"] = string.Join(",", draft.Extensions ?? Array.Empty<string>()),
            ["removeJunk"] = draft.RemoveJunk?.ToString() ?? string.Empty,
            ["onlyGames"] = draft.OnlyGames?.ToString() ?? string.Empty,
            ["keepUnknownWhenOnlyGames"] = draft.KeepUnknownWhenOnlyGames?.ToString() ?? string.Empty,
            ["aggressiveJunk"] = draft.AggressiveJunk?.ToString() ?? string.Empty,
            ["sortConsole"] = draft.SortConsole?.ToString() ?? string.Empty,
            ["enableDat"] = draft.EnableDat?.ToString() ?? string.Empty,
            ["enableDatAudit"] = draft.EnableDatAudit?.ToString() ?? string.Empty,
            ["enableDatRename"] = draft.EnableDatRename?.ToString() ?? string.Empty,
            ["datRoot"] = draft.DatRoot ?? string.Empty,
            ["hashType"] = draft.HashType ?? string.Empty,
            ["convertFormat"] = draft.ConvertFormat ?? string.Empty,
            ["convertOnly"] = draft.ConvertOnly?.ToString() ?? string.Empty,
            ["approveReviews"] = draft.ApproveReviews?.ToString() ?? string.Empty,
            ["conflictPolicy"] = draft.ConflictPolicy ?? string.Empty,
            ["trashRoot"] = draft.TrashRoot ?? string.Empty
        };
    }

    internal void ApplySelectedRunConfiguration()
    {
        if (_suppressRunConfigurationSelectionApply || _applyingRunConfigurationSelection)
            return;

        var workflowId = NormalizeSelection(SelectedWorkflowScenarioId);
        var profileId = NormalizeSelection(SelectedRunProfileId);
        if (workflowId is null && profileId is null)
        {
            OnRunConfigurationSelectionMetadataChanged();
            return;
        }

        _applyingRunConfigurationSelection = true;
        try
        {
            var dataDir = FeatureService.ResolveDataDirectory()
                          ?? RunEnvironmentBuilder.ResolveDataDir();
            var settings = RunEnvironmentBuilder.LoadSettings(dataDir);
            var baselineDraft = BuildCurrentRunConfigurationDraft(includeSelections: false);
            var selectionDraft = new RunConfigurationDraft
            {
                Roots = baselineDraft.Roots,
                WorkflowScenarioId = workflowId,
                ProfileId = profileId
            };

            var materialized = _runConfigurationMaterializer.MaterializeAsync(
                selectionDraft,
                new RunConfigurationExplicitness(),
                settings,
                baselineDraft: baselineDraft).GetAwaiter().GetResult();

            ApplyMaterializedRunConfiguration(materialized);
        }
        catch (InvalidOperationException ex)
        {
            AddLog($"[Profiles] Auswahl konnte nicht angewendet werden: {ex.Message}", "WARN");
            OnRunConfigurationSelectionMetadataChanged();
        }
        finally
        {
            _applyingRunConfigurationSelection = false;
        }
    }

    internal void ApplyMaterializedRunConfiguration(MaterializedRunConfiguration materialized)
    {
        ArgumentNullException.ThrowIfNull(materialized);

        var draft = materialized.EffectiveDraft;
        _suppressRunConfigurationSelectionApply = true;
        try
        {
            DryRun = !string.Equals(draft.Mode, RunConstants.ModeMove, StringComparison.OrdinalIgnoreCase);
            RemoveJunk = draft.RemoveJunk ?? RemoveJunk;
            OnlyGames = draft.OnlyGames ?? OnlyGames;
            KeepUnknownWhenOnlyGames = draft.KeepUnknownWhenOnlyGames ?? KeepUnknownWhenOnlyGames;
            AggressiveJunk = draft.AggressiveJunk ?? AggressiveJunk;
            SortConsole = draft.SortConsole ?? SortConsole;
            UseDat = draft.EnableDat ?? UseDat;
            EnableDatAudit = (draft.EnableDat ?? UseDat) && (draft.EnableDatAudit ?? false);
            EnableDatRename = (draft.EnableDat ?? UseDat) && (draft.EnableDatRename ?? false);
            DatRoot = draft.DatRoot ?? string.Empty;
            DatHashType = draft.HashType ?? RunConstants.DefaultHashType;
            ConvertEnabled = !string.IsNullOrWhiteSpace(draft.ConvertFormat);
            ConvertOnly = draft.ConvertOnly ?? false;
            ApproveReviews = draft.ApproveReviews ?? false;
            TrashRoot = draft.TrashRoot ?? string.Empty;
            ApplyConflictPolicyFromDraft(draft.ConflictPolicy);
            ApplyPreferredRegions(draft.PreferRegions);
            ApplySelectedExtensions(draft.Extensions);
            SetRunConfigurationSelectionInternal(materialized.Workflow?.Id, materialized.EffectiveProfileId);
        }
        finally
        {
            _suppressRunConfigurationSelectionApply = false;
        }

        RefreshStatus();
        UpdateWizardRegionSummary();
        OnRunConfigurationSelectionMetadataChanged();
    }

    private void OnRunConfigurationSelectionChanged()
    {
        OnRunConfigurationSelectionMetadataChanged();

        if (!_suppressRunConfigurationSelectionApply)
            ApplySelectedRunConfiguration();
    }

    private void OnRunConfigurationSelectionMetadataChanged()
    {
        OnPropertyChanged(nameof(HasSelectedWorkflow));
        OnPropertyChanged(nameof(HasSelectedRunProfile));
        OnPropertyChanged(nameof(SelectedWorkflowName));
        OnPropertyChanged(nameof(SelectedWorkflowDescription));
        OnPropertyChanged(nameof(SelectedWorkflowStepsSummary));
        OnPropertyChanged(nameof(SelectedRunProfileName));
        OnPropertyChanged(nameof(SelectedRunProfileDescription));
        OnPropertyChanged(nameof(RunConfigurationSelectionSummary));
        OnPropertyChanged(nameof(CanAdvanceWizard));
    }

    private void OnShellStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.WizardStep))
            OnPropertyChanged(nameof(CanAdvanceWizard));
    }

    private void SetRunConfigurationSelectionInternal(string? workflowScenarioId, string? profileId)
    {
        SelectedWorkflowScenarioId = workflowScenarioId;
        SelectedRunProfileId = profileId;
    }

    private WorkflowScenarioDefinition? TryGetSelectedWorkflow()
        => AvailableWorkflows.FirstOrDefault(item => string.Equals(item.Id, SelectedWorkflowScenarioId, StringComparison.OrdinalIgnoreCase));

    private RunProfileSummary? TryGetSelectedProfileSummary()
        => AvailableRunProfiles.FirstOrDefault(item => string.Equals(item.Id, SelectedRunProfileId, StringComparison.OrdinalIgnoreCase));

    private void ApplyConflictPolicyFromDraft(string? conflictPolicy)
    {
        if (string.IsNullOrWhiteSpace(conflictPolicy))
            return;

        if (Enum.TryParse<Models.ConflictPolicy>(conflictPolicy, ignoreCase: true, out var parsed))
            ConflictPolicy = parsed;
    }

    private void ApplyPreferredRegions(string[]? preferredRegions)
    {
        var orderedRegions = (preferredRegions ?? RunConstants.DefaultPreferRegions)
            .Where(static region => !string.IsNullOrWhiteSpace(region))
            .Select(static region => region.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ApplyRegionPreset(orderedRegions);
    }

    private void ApplySelectedExtensions(string[]? extensions)
    {
        var normalized = new HashSet<string>(
            (extensions is { Length: > 0 } ? extensions : RunOptions.DefaultExtensions)
            .Select(static extension => extension.StartsWith('.') ? extension : "." + extension),
            StringComparer.OrdinalIgnoreCase);

        foreach (var filter in ExtensionFilters)
            filter.IsChecked = normalized.Contains(filter.Extension);

        OnPropertyChanged(nameof(SelectedExtensionCount));
        OnPropertyChanged(nameof(ExtensionCountDisplay));
    }

    private string[] BuildSelectedExtensionsForRunConfiguration()
    {
        var selected = GetSelectedExtensions();
        return selected.Length == 0 ? RunOptions.DefaultExtensions : selected;
    }

    private string[] BuildProfileTags()
    {
        var tags = new List<string>();
        if (UseDat)
            tags.Add("dat");
        if (SortConsole)
            tags.Add("sorting");
        if (ConvertEnabled || ConvertOnly)
            tags.Add("conversion");
        if (DryRun)
            tags.Add("preview");

        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeSelection(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
