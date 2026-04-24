using Romulus.Contracts.Ports;
using Romulus.Infrastructure.FileSystem;

namespace Romulus.Infrastructure.Hashing;

/// <summary>
/// Header repair implementation for iNES and SNES copier headers.
/// </summary>
public sealed class HeaderRepairService : IHeaderRepairService
{
    private readonly IFileSystem _fileSystem;

    public HeaderRepairService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public bool RepairNesHeader(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !_fileSystem.TestPath(path, "Leaf"))
            return false;

        try
        {
            var data = File.ReadAllBytes(path);
            if (data.Length < 16)
                return false;

            var header = data.AsSpan(0, 16);

            if (header[0] != 0x4E || header[1] != 0x45 || header[2] != 0x53 || header[3] != 0x1A)
                return false;

            var dirty = false;
            for (var i = 12; i <= 15; i++)
            {
                if (header[i] == 0x00)
                    continue;

                dirty = true;
                break;
            }

            if (!dirty)
                return false;

            var backupPath = BuildBackupPath(path, "nes-header");
            _fileSystem.CopyFile(path, backupPath, overwrite: false);

            for (var i = 12; i <= 15; i++)
                data[i] = 0x00;

            AtomicFileWriter.WriteAllBytes(path, data);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool RemoveCopierHeader(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !_fileSystem.TestPath(path, "Leaf"))
            return false;

        try
        {
            var fi = new FileInfo(path);
            if (fi.Length < 512 || fi.Length % 1024 != 512)
                return false;

            var data = File.ReadAllBytes(path);
            _fileSystem.CopyFile(path, BuildBackupPath(path, "snes-copier"), overwrite: false);
            AtomicFileWriter.WriteAllBytes(path, data[512..]);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string BuildBackupPath(string path, string reason)
        => path + $".{DateTime.UtcNow:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.{reason}.bak";
}
