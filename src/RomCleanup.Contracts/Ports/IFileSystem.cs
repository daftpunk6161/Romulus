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
    bool FileExists(string literalPath) => File.Exists(literalPath);
    bool DirectoryExists(string literalPath) => Directory.Exists(literalPath);
    IReadOnlyList<string> GetDirectoryFiles(string directoryPath, string searchPattern)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || string.IsNullOrWhiteSpace(searchPattern))
            return Array.Empty<string>();

        try
        {
            if (!Directory.Exists(directoryPath))
                return Array.Empty<string>();

            var files = Directory.GetFiles(directoryPath, searchPattern);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }
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

        // SEC-RENAME-01: Block path segments in newFileName (prevents traversal via "..\..\")
        if (!string.Equals(Path.GetFileName(newFileName), newFileName, StringComparison.Ordinal))
            throw new InvalidOperationException("Blocked: Rename target must be a file name without path segments.");

        // SEC-RENAME-02: Block NTFS Alternate Data Streams
        if (newFileName.Contains(':'))
            throw new InvalidOperationException("Blocked: Rename target contains ADS separator.");

        // SEC-RENAME-03: Block invalid filename characters
        if (newFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("Blocked: Rename target contains invalid filename characters.");

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

        // SEC-DIRMOVE-01: Block directory traversal in destination
        var destSegments = destinationPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (destSegments.Any(s => s == ".."))
            throw new InvalidOperationException("Blocked: Destination path contains directory traversal.");

        // SEC-DIRMOVE-02: Normalize and validate paths
        var fullSource = Path.GetFullPath(sourcePath);
        var fullDest = Path.GetFullPath(destinationPath);

        if (string.Equals(fullSource, fullDest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source and destination are the same path.");

        // SEC-DIRMOVE-03: Block reparse points on source directory
        if (Directory.Exists(fullSource))
        {
            var sourceInfo = new DirectoryInfo(fullSource);
            if ((sourceInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Blocked: Source directory is a reparse point.");
        }

        // SEC-DIRMOVE-04: Block reparse points on destination parent
        var destParent = Path.GetDirectoryName(fullDest);
        if (!string.IsNullOrEmpty(destParent) && Directory.Exists(destParent))
        {
            var destParentInfo = new DirectoryInfo(destParent);
            if ((destParentInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Blocked: Destination parent is a reparse point.");
        }

        try
        {
            Directory.Move(fullSource, fullDest);
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
