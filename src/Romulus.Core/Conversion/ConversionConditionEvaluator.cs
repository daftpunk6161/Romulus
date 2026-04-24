namespace Romulus.Core.Conversion;

using Romulus.Contracts.Models;

/// <summary>
/// Evaluates runtime conditions for conversion capabilities.
/// </summary>
public sealed class ConversionConditionEvaluator
{
    private readonly Func<string, long> _fileSizeProvider;
    private readonly Func<string, bool>? _encryptedPbpDetector;
    private readonly Func<string, bool?>? _ps2CdDetector;

    /// <param name="fileSizeProvider">Returns file size in bytes for a given path.</param>
    /// <param name="encryptedPbpDetector">Returns true if a PBP file is encrypted. Optional; defaults to false when null.</param>
    /// <param name="ps2CdDetector">Returns true for PS2 CD, false for PS2 DVD, null when undetectable.</param>
    public ConversionConditionEvaluator(
        Func<string, long> fileSizeProvider,
        Func<string, bool>? encryptedPbpDetector = null,
        Func<string, bool?>? ps2CdDetector = null)
    {
        _fileSizeProvider = fileSizeProvider ?? throw new ArgumentNullException(nameof(fileSizeProvider));
        _encryptedPbpDetector = encryptedPbpDetector;
        _ps2CdDetector = ps2CdDetector;
    }

    /// <summary>
    /// Evaluates whether a condition holds for a given source path.
    /// </summary>
    public bool Evaluate(ConversionCondition condition, string sourcePath, string? consoleKey = null)
    {
        var extension = Path.GetExtension(sourcePath);
        var fileName = Path.GetFileName(sourcePath);

        return condition switch
        {
            ConversionCondition.None => true,
            ConversionCondition.FileSizeLessThan700MB => EvaluatePs2CdAwareThreshold(sourcePath, consoleKey, expectCd: true),
            ConversionCondition.FileSizeGreaterEqual700MB => EvaluatePs2CdAwareThreshold(sourcePath, consoleKey, expectCd: false),
            ConversionCondition.IsNKitSource => fileName.Contains(".nkit.", StringComparison.OrdinalIgnoreCase),
            ConversionCondition.IsWadFile => string.Equals(extension, ".wad", StringComparison.OrdinalIgnoreCase),
            ConversionCondition.IsCdiSource => string.Equals(extension, ".cdi", StringComparison.OrdinalIgnoreCase),
            ConversionCondition.IsEncryptedPbp => IsEncryptedPbp(sourcePath, extension),
            _ => throw new NotSupportedException($"ConversionCondition '{condition}' has no evaluator.")
        };
    }

    private bool EvaluatePs2CdAwareThreshold(string sourcePath, string? consoleKey, bool expectCd)
    {
        if (string.Equals(consoleKey, "PS2", StringComparison.OrdinalIgnoreCase)
            && _ps2CdDetector is not null)
        {
            var detectedPs2Cd = _ps2CdDetector(sourcePath);
            if (detectedPs2Cd.HasValue)
                return detectedPs2Cd.Value == expectCd;
        }

        var size = SafeSize(sourcePath);
        if (size <= 0)
            return false;

        return expectCd
            ? size < ConversionThresholds.CdImageThresholdBytes
            : size >= ConversionThresholds.CdImageThresholdBytes;
    }

    private long SafeSize(string sourcePath)
    {
        try
        {
            return _fileSizeProvider(sourcePath);
        }
        catch (IOException)
        {
            return -1;
        }
    }

    private bool IsEncryptedPbp(string sourcePath, string extension)
    {
        if (!string.Equals(extension, ".pbp", StringComparison.OrdinalIgnoreCase))
            return false;

        if (_encryptedPbpDetector is not null)
            return _encryptedPbpDetector(sourcePath);

        return false;
    }
}
