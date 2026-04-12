using Romulus.Contracts.Ports;

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
            var header = new byte[16];
            using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var read = fs.Read(header, 0, header.Length);
                if (read < 16)
                    return false;
            }

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

            // Crash-safe: write repaired version to .tmp, then rename
            _fileSystem.CopyFile(path, path + ".bak", overwrite: true);

            var tmpPath = path + ".tmp";
            using (var source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var target = File.Open(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long absoluteOffset = 0;
                int read;

                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var zeroStart = (int)Math.Max(0, 12 - absoluteOffset);
                    var zeroEnd = (int)Math.Min(read - 1, 15 - absoluteOffset);
                    if (zeroStart <= zeroEnd)
                    {
                        for (var i = zeroStart; i <= zeroEnd; i++)
                            buffer[i] = 0x00;
                    }

                    target.Write(buffer, 0, read);
                    absoluteOffset += read;
                }

                target.Flush();
            }

            File.Move(tmpPath, path, overwrite: true);
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

            _fileSystem.CopyFile(path, path + ".bak", overwrite: true);

            // Crash-safe: write to .tmp, then rename
            var tmpPath = path + ".tmp";
            using (var source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var target = File.Open(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                source.Seek(512, SeekOrigin.Begin);
                source.CopyTo(target);
                target.Flush();
            }

            File.Move(tmpPath, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
