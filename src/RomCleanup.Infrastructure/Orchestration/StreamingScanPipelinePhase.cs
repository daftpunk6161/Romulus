using System.Runtime.CompilerServices;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;
using RomCleanup.Core.SetParsing;

namespace RomCleanup.Infrastructure.Orchestration;

/// <summary>
/// Streaming scan phase that exposes async file enumeration via contract port.
/// Keeps existing scan safety filters (blocklist, duplicate path guard, set-member pruning).
/// </summary>
public sealed class StreamingScanPipelinePhase : IAsyncFileScanner
{
    private readonly PipelineContext _context;

    public StreamingScanPipelinePhase(PipelineContext context)
    {
        _context = context;
    }

    public async IAsyncEnumerable<ScannedFileEntry> EnumerateFilesAsync(
        IReadOnlyList<string> roots,
        IReadOnlyCollection<string> extensions,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var candidates = new List<ScannedFileEntry>();
        var seenCandidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var setMemberPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.OnProgress?.Invoke($"[Scan] {root}: Dateien sammeln…");

            var files = _context.FileSystem.GetFilesSafe(root, extensions);
            _context.OnProgress?.Invoke($"[Scan] {root}: {files.Count} Dateien gefunden");

            var processed = 0;
            var fileCount = files.Count;

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalizedPath = Path.GetFullPath(filePath);
                if (!seenCandidatePaths.Add(normalizedPath))
                    continue;

                if (ExecutionHelpers.IsBlocklisted(filePath))
                    continue;

                processed++;
                if (processed % 500 == 0)
                    _context.OnProgress?.Invoke($"[Scan] {processed}/{fileCount} Dateien verarbeitet…");

                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var setMembers = GetSetMembers(filePath, ext);
                foreach (var member in setMembers)
                    setMemberPaths.Add(Path.GetFullPath(member));

                candidates.Add(new ScannedFileEntry(root, normalizedPath, ext));
            }
        }

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (setMemberPaths.Contains(candidate.Path))
                continue;

            yield return candidate;
            await Task.Yield();
        }
    }

    private static IReadOnlyList<string> GetSetMembers(string filePath, string ext)
    {
        return ext switch
        {
            ".cue" => CueSetParser.GetRelatedFiles(filePath),
            ".gdi" => GdiSetParser.GetRelatedFiles(filePath),
            ".ccd" => CcdSetParser.GetRelatedFiles(filePath),
            // Keep playlist targets as first-class scan candidates for DAT/hash matching.
            ".m3u" => Array.Empty<string>(),
            _ => Array.Empty<string>()
        };
    }
}
