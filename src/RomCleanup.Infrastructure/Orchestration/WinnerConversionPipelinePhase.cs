using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;

namespace RomCleanup.Infrastructure.Orchestration;

/// <summary>
/// Pipeline phase that converts winner files after deduplication.
/// </summary>
public sealed class WinnerConversionPipelinePhase : IPipelinePhase<WinnerConversionPhaseInput, WinnerConversionPhaseOutput>
{
    public string Name => "FormatConvert";

    public WinnerConversionPhaseOutput Execute(WinnerConversionPhaseInput input, PipelineContext context, CancellationToken cancellationToken)
    {
        context.Metrics.StartPhase(Name);
        context.OnProgress?.Invoke($"[Convert] Starte Formatkonvertierung für {input.GameGroups.Count} Gruppen…");

        int converted = 0;
        int convertErrors = 0;
        int convertSkipped = 0;

        foreach (var group in input.GameGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var winnerPath = group.Winner.MainPath;
            if (input.JunkRemovedPaths.Contains(winnerPath) || !File.Exists(winnerPath))
                continue;

            var ext = Path.GetExtension(winnerPath).ToLowerInvariant();
            var consoleKey = group.Winner.ConsoleKey ?? "";
            var target = input.Converter.GetTargetFormat(consoleKey, ext);
            if (target is null)
                continue;

            var convResult = input.Converter.Convert(winnerPath, target, cancellationToken);
            if (convResult.Outcome == ConversionOutcome.Success)
            {
                var verificationOk = convResult.TargetPath is not null && input.Converter.Verify(convResult.TargetPath, target);
                if (verificationOk)
                {
                    converted++;
                    AppendConversionAudit(context, input.Options, winnerPath, convResult.TargetPath, target.ToolName);
                    MoveConvertedSourceToTrash(context, input.Options, winnerPath, convResult.TargetPath);
                }
                else
                {
                    convertErrors++;
                    if (convResult.TargetPath is not null)
                    {
                        context.OnProgress?.Invoke($"WARNING: Verification failed for {convResult.TargetPath}");
                        AppendConversionFailedAudit(context, input.Options, winnerPath, convResult.TargetPath, target.ToolName);
                        // SEC-CONV-04: Clean up failed output to prevent orphaned corrupt files
                        try { if (File.Exists(convResult.TargetPath)) File.Delete(convResult.TargetPath); }
                        catch { /* best-effort cleanup */ }
                    }
                }
            }
            else if (convResult.Outcome == ConversionOutcome.Skipped)
            {
                convertSkipped++;
            }
            else
            {
                convertErrors++;
                context.OnProgress?.Invoke($"WARNING: Conversion failed for {winnerPath}: {convResult.Reason}");
                AppendConversionErrorAudit(context, input.Options, winnerPath, convResult.Reason);
            }
        }

        context.OnProgress?.Invoke($"[Convert] Abgeschlossen: {converted} konvertiert, {convertSkipped} übersprungen, {convertErrors} Fehler");
        context.Metrics.CompletePhase(converted);

        return new WinnerConversionPhaseOutput(converted, convertErrors, convertSkipped);
    }

    private static void AppendConversionAudit(PipelineContext context, RunOptions options, string sourcePath, string? targetPath, string toolName)
    {
        if (string.IsNullOrEmpty(options.AuditPath) || string.IsNullOrEmpty(targetPath))
            return;

        var root = FindRootForPath(sourcePath, options.Roots);
        if (root is not null)
            context.AuditStore.AppendAuditRow(options.AuditPath, root, sourcePath, targetPath, "CONVERT", "GAME", "", $"format-convert:{toolName}");
    }

    private static void AppendConversionFailedAudit(PipelineContext context, RunOptions options, string sourcePath, string? targetPath, string toolName)
    {
        if (string.IsNullOrEmpty(options.AuditPath) || string.IsNullOrEmpty(targetPath))
            return;

        var root = FindRootForPath(sourcePath, options.Roots);
        if (root is not null)
            context.AuditStore.AppendAuditRow(options.AuditPath, root, sourcePath, targetPath, "CONVERT_FAILED", "GAME", "", $"verify-failed:{toolName}");
    }

    private static void MoveConvertedSourceToTrash(PipelineContext context, RunOptions options, string sourcePath, string? convertedPath)
    {
        if (string.IsNullOrEmpty(convertedPath) || !File.Exists(convertedPath))
            return;

        var root = FindRootForPath(sourcePath, options.Roots);
        if (root is null)
            return;

        var trashBase = string.IsNullOrEmpty(options.TrashRoot) ? root : options.TrashRoot;
        var trashDir = Path.Combine(trashBase, "_TRASH_CONVERTED");
        context.FileSystem.EnsureDirectory(trashDir);
        var fileName = Path.GetFileName(sourcePath);
        var trashDest = context.FileSystem.ResolveChildPathWithinRoot(trashBase, Path.Combine("_TRASH_CONVERTED", fileName));
        if (trashDest is null)
            return;

        try
        {
            context.FileSystem.MoveItemSafely(sourcePath, trashDest);
        }
        catch (Exception ex)
        {
            context.OnProgress?.Invoke($"WARNING: Could not move source after conversion: {ex.Message}");
        }
    }

    private static void AppendConversionErrorAudit(PipelineContext context, RunOptions options, string sourcePath, string? reason)
    {
        if (string.IsNullOrEmpty(options.AuditPath))
            return;

        var root = FindRootForPath(sourcePath, options.Roots);
        if (root is not null)
            context.AuditStore.AppendAuditRow(options.AuditPath, root, sourcePath, "", "CONVERT_ERROR", "GAME", "", $"convert-error:{reason}");
    }

    private static string? FindRootForPath(string filePath, IReadOnlyList<string> roots)
    {
        var fullPath = Path.GetFullPath(filePath);
        foreach (var root in roots)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return root;
        }

        return null;
    }
}

public sealed record WinnerConversionPhaseInput(
    IReadOnlyList<DedupeResult> GameGroups,
    RunOptions Options,
    IReadOnlySet<string> JunkRemovedPaths,
    IFormatConverter Converter);

public sealed record WinnerConversionPhaseOutput(
    int Converted,
    int ConvertErrors,
    int ConvertSkipped);