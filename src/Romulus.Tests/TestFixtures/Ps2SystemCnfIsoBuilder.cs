using System.Buffers.Binary;
using System.Text;

namespace Romulus.Tests.TestFixtures;

internal static class Ps2SystemCnfIsoBuilder
{
    private const int SectorSize = 2048;
    private const int PvdSector = 16;
    private const int RootDirectorySector = 20;
    private const int SystemCnfSector = 21;
    private const int RootDirectoryRecordOffset = 156;

    public static void WriteIso(string path, string systemCnfContent, long? finalLength = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(systemCnfContent);

        var normalizedContent = systemCnfContent.Replace("\n", "\r\n", StringComparison.Ordinal);
        var systemCnfBytes = Encoding.ASCII.GetBytes(normalizedContent);
        var totalSectors = Math.Max(SystemCnfSector + 1, 24);
        var image = new byte[totalSectors * SectorSize];

        WritePrimaryVolumeDescriptor(image, totalSectors);
        WriteRootDirectory(image, systemCnfBytes.Length);
        Array.Copy(systemCnfBytes, 0, image, SystemCnfSector * SectorSize, systemCnfBytes.Length);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(image, 0, image.Length);

        if (finalLength.HasValue && finalLength.Value > image.Length)
            stream.SetLength(finalLength.Value);
    }

    private static void WritePrimaryVolumeDescriptor(byte[] image, int totalSectors)
    {
        var offset = PvdSector * SectorSize;
        image[offset] = 0x01;
        Encoding.ASCII.GetBytes("CD001").CopyTo(image, offset + 1);
        image[offset + 6] = 0x01;
        Encoding.ASCII.GetBytes("PLAYSTATION").CopyTo(image, offset + 8);
        Encoding.ASCII.GetBytes("ROMULUS_PS2_TEST").CopyTo(image, offset + 40);
        WriteBothEndianInt32(image, offset + 80, totalSectors);
        WriteBothEndianInt16(image, offset + 128, SectorSize);

        WriteDirectoryRecord(
            image.AsSpan(offset + RootDirectoryRecordOffset),
            RootDirectorySector,
            SectorSize,
            isDirectory: true,
            fileIdentifier: [0x00]);
    }

    private static void WriteRootDirectory(byte[] image, int systemCnfLength)
    {
        var offset = RootDirectorySector * SectorSize;
        var cursor = 0;

        cursor += WriteDirectoryRecord(
            image.AsSpan(offset + cursor),
            RootDirectorySector,
            SectorSize,
            isDirectory: true,
            fileIdentifier: [0x00]);

        cursor += WriteDirectoryRecord(
            image.AsSpan(offset + cursor),
            RootDirectorySector,
            SectorSize,
            isDirectory: true,
            fileIdentifier: [0x01]);

        WriteDirectoryRecord(
            image.AsSpan(offset + cursor),
            SystemCnfSector,
            systemCnfLength,
            isDirectory: false,
            fileIdentifier: Encoding.ASCII.GetBytes("SYSTEM.CNF;1"));
    }

    private static int WriteDirectoryRecord(
        Span<byte> destination,
        int extentLba,
        int dataLength,
        bool isDirectory,
        byte[] fileIdentifier)
    {
        var recordLength = 33 + fileIdentifier.Length + (fileIdentifier.Length % 2 == 0 ? 1 : 0);
        destination[..recordLength].Clear();
        destination[0] = (byte)recordLength;
        destination[1] = 0x00;
        WriteBothEndianInt32(destination, 2, extentLba);
        WriteBothEndianInt32(destination, 10, dataLength);
        destination[18] = 124;
        destination[19] = 4;
        destination[20] = 15;
        destination[25] = isDirectory ? (byte)0x02 : (byte)0x00;
        WriteBothEndianInt16(destination, 28, 1);
        destination[32] = (byte)fileIdentifier.Length;
        fileIdentifier.CopyTo(destination[33..]);
        return recordLength;
    }

    private static void WriteBothEndianInt32(Span<byte> destination, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, 4), value);
        BinaryPrimitives.WriteInt32BigEndian(destination.Slice(offset + 4, 4), value);
    }

    private static void WriteBothEndianInt16(Span<byte> destination, int offset, int value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(offset, 2), (short)value);
        BinaryPrimitives.WriteInt16BigEndian(destination.Slice(offset + 2, 2), (short)value);
    }
}
