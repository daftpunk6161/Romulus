using RomCleanup.Contracts.Models;
using RomCleanup.Infrastructure.Metrics;
using RomCleanup.Infrastructure.Orchestration;

namespace RomCleanup.Api;

internal static class ApiRunResultMapper
{
    public static ApiRunResult Map(RunResult result, RunProjection projection)
    {
        return new ApiRunResult
        {
            OrchestratorStatus = projection.Status,
            ExitCode = projection.ExitCode,
            TotalFiles = projection.TotalFiles,
            Candidates = projection.Candidates,
            Groups = projection.Groups,
            Winners = projection.Keep,
            Losers = projection.Dupes,
            Games = projection.Games,
            Unknown = projection.Unknown,
            Junk = projection.Junk,
            Bios = projection.Bios,
            DatMatches = projection.DatMatches,
            HealthScore = projection.HealthScore,
            ConvertedCount = projection.ConvertedCount,
            ConvertErrorCount = projection.ConvertErrorCount,
            ConvertSkippedCount = projection.ConvertSkippedCount,
            ConvertBlockedCount = projection.ConvertBlockedCount,
            JunkRemovedCount = projection.JunkRemovedCount,
            FilteredNonGameCount = projection.FilteredNonGameCount,
            JunkFailCount = projection.JunkFailCount,
            MoveCount = projection.MoveCount,
            SkipCount = projection.SkipCount,
            ConsoleSortMoved = projection.ConsoleSortMoved,
            ConsoleSortFailed = projection.ConsoleSortFailed,
            FailCount = projection.FailCount,
            SavedBytes = projection.SavedBytes,
            DurationMs = projection.DurationMs,
            PreflightWarnings = result.Preflight?.Warnings?.ToArray() ?? Array.Empty<string>(),
            PhaseMetrics = BuildPhaseMetricsPayload(result.PhaseMetrics),
            DedupeGroups = BuildDedupeGroupsPayload(result.DedupeGroups)
        };
    }

    private static ApiPhaseMetrics BuildPhaseMetricsPayload(PhaseMetricsResult? metrics)
    {
        if (metrics is null)
        {
            return new ApiPhaseMetrics
            {
                Phases = Array.Empty<ApiPhaseMetric>()
            };
        }

        return new ApiPhaseMetrics
        {
            RunId = metrics.RunId,
            StartedAt = metrics.StartedAt,
            TotalDurationMs = (long)metrics.TotalDuration.TotalMilliseconds,
            Phases = metrics.Phases.Select(phase => new ApiPhaseMetric
            {
                Phase = phase.Phase,
                StartedAt = phase.StartedAt,
                DurationMs = (long)phase.Duration.TotalMilliseconds,
                ItemCount = phase.ItemCount,
                ItemsPerSec = phase.ItemsPerSec,
                PercentOfTotal = phase.PercentOfTotal,
                Status = phase.Status
            }).ToArray()
        };
    }

    private static ApiDedupeGroup[] BuildDedupeGroupsPayload(IReadOnlyList<DedupeResult> dedupeGroups)
    {
        if (dedupeGroups.Count == 0)
            return Array.Empty<ApiDedupeGroup>();

        return dedupeGroups.Select(group => new ApiDedupeGroup
        {
            GameKey = group.GameKey,
            Winner = group.Winner,
            Losers = group.Losers.ToArray()
        }).ToArray();
    }
}
