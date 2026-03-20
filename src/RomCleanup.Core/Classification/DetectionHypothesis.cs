namespace RomCleanup.Core.Classification;

/// <summary>
/// A single detection hypothesis from one detection method.
/// </summary>
/// <param name="ConsoleKey">The detected console key (e.g. "PS1", "NES").</param>
/// <param name="Confidence">Confidence 0–100. Higher = more reliable.</param>
/// <param name="Source">Which detection method produced this hypothesis.</param>
/// <param name="Evidence">Human-readable evidence string (e.g. "folder=Nintendo", "serial=SLUS-00123").</param>
public sealed record DetectionHypothesis(
    string ConsoleKey,
    int Confidence,
    DetectionSource Source,
    string Evidence);

/// <summary>
/// Aggregated console detection result from all methods.
/// </summary>
/// <param name="ConsoleKey">The winning console key, or "UNKNOWN".</param>
/// <param name="Confidence">Aggregate confidence 0–100.</param>
/// <param name="Hypotheses">All hypotheses that contributed.</param>
/// <param name="HasConflict">True if different methods disagree on the console.</param>
/// <param name="ConflictDetail">Description of the conflict, if any.</param>
public sealed record ConsoleDetectionResult(
    string ConsoleKey,
    int Confidence,
    IReadOnlyList<DetectionHypothesis> Hypotheses,
    bool HasConflict,
    string? ConflictDetail)
{
    /// <summary>Unknown result with 0 confidence.</summary>
    public static ConsoleDetectionResult Unknown { get; } = new(
        "UNKNOWN", 0, Array.Empty<DetectionHypothesis>(), false, null);
}

/// <summary>
/// Detection method source identifiers, ordered by typical reliability.
/// </summary>
public enum DetectionSource
{
    /// <summary>DAT hash match — most reliable (hash-verified content).</summary>
    DatHash = 100,

    /// <summary>Unique file extension — very high reliability.</summary>
    UniqueExtension = 95,

    /// <summary>Disc header binary signature (ISO/CHD magic bytes).</summary>
    DiscHeader = 92,

    /// <summary>Cartridge header binary signature (iNES/Genesis magic bytes).</summary>
    CartridgeHeader = 90,

    /// <summary>Serial number in filename (e.g. SLUS-00123).</summary>
    SerialNumber = 88,

    /// <summary>Folder name matches a console alias.</summary>
    FolderName = 85,

    /// <summary>Archive interior extension (ZIP/7z inner file).</summary>
    ArchiveContent = 80,

    /// <summary>System keyword tag in filename (e.g. [GBA]).</summary>
    FilenameKeyword = 75,

    /// <summary>Ambiguous extension with only one console match.</summary>
    AmbiguousExtension = 40,
}
