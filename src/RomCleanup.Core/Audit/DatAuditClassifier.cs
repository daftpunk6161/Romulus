using RomCleanup.Contracts.Models;

namespace RomCleanup.Core.Audit;

/// <summary>
/// Pure DAT audit status classifier.
/// </summary>
public static class DatAuditClassifier
{
    /// <summary>
    /// Classifies a ROM against the DAT index.
    /// Tries headerlessHash first (for NES/SNES/7800/Lynx), then falls back to regular hash.
    /// </summary>
    public static DatAuditStatus Classify(
        string? hash,
        string? headerlessHash,
        string actualFileName,
        string? consoleKey,
        DatIndex datIndex)
    {
        ArgumentNullException.ThrowIfNull(actualFileName);
        ArgumentNullException.ThrowIfNull(datIndex);

        // Try headerless hash first (No-Intro DATs hash headered ROMs without header)
        if (!string.IsNullOrWhiteSpace(headerlessHash))
        {
            var result = ClassifyWithHash(headerlessHash, actualFileName, consoleKey, datIndex);
            if (result != DatAuditStatus.Unknown && result != DatAuditStatus.Miss)
                return result;

            // If headerless hash matched as Miss (known console, no match), it's a real Miss
            if (result == DatAuditStatus.Miss && IsRealConsoleKey(consoleKey))
                return DatAuditStatus.Miss;
        }

        // Fall back to regular hash
        return ClassifyWithHash(hash, actualFileName, consoleKey, datIndex);
    }

    /// <summary>Backward-compatible overload without headerlessHash.</summary>
    public static DatAuditStatus Classify(
        string? hash,
        string actualFileName,
        string? consoleKey,
        DatIndex datIndex)
        => Classify(hash, headerlessHash: null, actualFileName, consoleKey, datIndex);

    private static DatAuditStatus ClassifyWithHash(
        string? hash,
        string actualFileName,
        string? consoleKey,
        DatIndex datIndex)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return DatAuditStatus.Unknown;

        if (IsRealConsoleKey(consoleKey))
        {
            var inConsole = datIndex.LookupWithFilename(consoleKey!, hash);
            if (inConsole is null)
                return DatAuditStatus.Miss;

            return IsSameFileName(actualFileName, inConsole.Value.RomFileName)
                ? DatAuditStatus.Have
                : DatAuditStatus.HaveWrongName;
        }

        var matches = datIndex.LookupAllByHash(hash);
        if (matches.Count == 0)
            return DatAuditStatus.Unknown;

        if (matches.Count > 1)
            return DatAuditStatus.Ambiguous;

        return IsSameFileName(actualFileName, matches[0].Entry.RomFileName)
            ? DatAuditStatus.Have
            : DatAuditStatus.HaveWrongName;
    }

    /// <summary>
    /// Checks whether UNKNOWN/AMBIGUOUS sentinel values are real console keys.
    /// These are detection sentinels, not actual console identifiers in the DAT index.
    /// </summary>
    private static bool IsRealConsoleKey(string? consoleKey)
        => !string.IsNullOrWhiteSpace(consoleKey)
           && !string.Equals(consoleKey, "UNKNOWN", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(consoleKey, "AMBIGUOUS", StringComparison.OrdinalIgnoreCase);

    private static bool IsSameFileName(string actualFileName, string? datRomFileName)
    {
        if (string.IsNullOrWhiteSpace(datRomFileName))
            return true;

        var actual = Path.GetFileName(actualFileName);
        var expected = Path.GetFileName(datRomFileName);

        // Exact match (same name + extension)
        if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            return true;

        // Stem match: handles archive containers ("Game.zip" vs "Game.nes")
        // and format variants ("Game.chd" vs "Game (Track 01).bin")
        return string.Equals(
            Path.GetFileNameWithoutExtension(actual),
            Path.GetFileNameWithoutExtension(expected),
            StringComparison.OrdinalIgnoreCase);
    }
}
