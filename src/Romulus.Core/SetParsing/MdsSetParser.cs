using Romulus.Contracts.Ports;

namespace Romulus.Core.SetParsing;

/// <summary>
/// Resolves Alcohol 120% (.mds) related files: .mdf with same base name.
/// Mirrors Get-MdsRelatedFiles from SetParsing.ps1.
/// </summary>
public static class MdsSetParser
{
    public static IReadOnlyList<string> GetRelatedFiles(string mdsPath, ISetParserIo? io = null)
    {
        var parserIo = SetParserIoResolver.Resolve(io);
        if (string.IsNullOrWhiteSpace(mdsPath))
            return Array.Empty<string>();

        string fullMdsPath;
        try
        {
            fullMdsPath = Path.GetFullPath(mdsPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Array.Empty<string>();
        }

        if (!parserIo.Exists(fullMdsPath))
            return Array.Empty<string>();

        var dir = Path.GetDirectoryName(fullMdsPath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(fullMdsPath);
        var mdfPath = Path.GetFullPath(Path.Combine(dir, baseName + ".mdf"));

        return parserIo.Exists(mdfPath) ? new[] { mdfPath } : Array.Empty<string>();
    }

    public static IReadOnlyList<string> GetMissingFiles(string mdsPath, ISetParserIo? io = null)
    {
        var parserIo = SetParserIoResolver.Resolve(io);
        if (string.IsNullOrWhiteSpace(mdsPath))
            return Array.Empty<string>();

        string fullMdsPath;
        try
        {
            fullMdsPath = Path.GetFullPath(mdsPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Array.Empty<string>();
        }

        if (!parserIo.Exists(fullMdsPath))
            return Array.Empty<string>();

        var dir = Path.GetDirectoryName(fullMdsPath) ?? "";
        var baseName = Path.GetFileNameWithoutExtension(fullMdsPath);
        var mdfPath = Path.GetFullPath(Path.Combine(dir, baseName + ".mdf"));

        return !parserIo.Exists(mdfPath) ? new[] { mdfPath } : Array.Empty<string>();
    }
}
