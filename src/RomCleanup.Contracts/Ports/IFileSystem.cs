namespace RomCleanup.Contracts.Ports;

/// <summary>
/// Port interface for file system operations.
/// Maps to New-FileSystemPort in PortInterfaces.ps1.
/// </summary>
public interface IFileSystem
{
    bool TestPath(string literalPath, string pathType = "Any");
    string EnsureDirectory(string path);
    IReadOnlyList<string> GetFilesSafe(string root, IEnumerable<string>? allowedExtensions = null);
    string? MoveItemSafely(string sourcePath, string destinationPath);
    /// <summary>
    /// SEC-MOVE-04: Move with explicit root containment validation.
    /// Returns null if destination is outside allowedRoot.
    /// </summary>
    string? MoveItemSafely(string sourcePath, string destinationPath, string allowedRoot)
    {
        if (string.IsNullOrWhiteSpace(allowedRoot))
            throw new ArgumentException("Allowed root must not be empty.", nameof(allowedRoot));

        var normalizedRoot = Path.GetFullPath(allowedRoot).TrimEnd(Path.DirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;
        var normalizedDest = Path.GetFullPath(destinationPath).TrimEnd(Path.DirectorySeparatorChar)
                           + Path.DirectorySeparatorChar;

        if (!normalizedDest.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return null;

        return MoveItemSafely(sourcePath, destinationPath);
    }
    string? RenameItemSafely(string sourcePath, string newFileName)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(newFileName))
            return null;

        var sourceDir = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(sourceDir))
            return null;

        var targetPath = Path.Combine(sourceDir, newFileName);
        return MoveItemSafely(sourcePath, targetPath);
    }
    bool MoveDirectorySafely(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            return false;

        try
        {
            Directory.Move(sourcePath, destinationPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return false;
        }
    }
    string? ResolveChildPathWithinRoot(string rootPath, string relativePath);
    bool IsReparsePoint(string path);
    void DeleteFile(string path);
    void CopyFile(string sourcePath, string destinationPath, bool overwrite = false);
}
