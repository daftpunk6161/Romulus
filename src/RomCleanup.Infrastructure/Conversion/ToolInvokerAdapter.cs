using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;

namespace RomCleanup.Infrastructure.Conversion;

/// <summary>
/// Executes conversion capabilities through IToolRunner and performs format-specific verification.
/// </summary>
public sealed class ToolInvokerAdapter(IToolRunner tools) : IToolInvoker
{
    private readonly IToolRunner _tools = tools ?? throw new ArgumentNullException(nameof(tools));

    public bool CanHandle(ConversionCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return !string.IsNullOrWhiteSpace(capability.Tool.ToolName);
    }

    public ToolInvocationResult Invoke(
        string sourcePath,
        string targetPath,
        ConversionCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(capability);

        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(sourcePath))
        {
            return new ToolInvocationResult(false, null, -1, null, "source-not-found", 0, VerificationStatus.NotAttempted);
        }

        if (string.IsNullOrWhiteSpace(capability.Command))
        {
            return new ToolInvocationResult(false, null, -1, null, "invalid-command", 0, VerificationStatus.NotAttempted);
        }

        var toolName = capability.Tool.ToolName;
        var toolPath = _tools.FindTool(toolName);
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            return new ToolInvocationResult(false, null, -1, null, $"tool-not-found:{toolName}", 0, VerificationStatus.VerifyNotAvailable);
        }

        var toolConstraintError = ValidateToolConstraints(toolPath, capability.Tool);
        if (toolConstraintError is not null)
        {
            return new ToolInvocationResult(false, null, -1, null, toolConstraintError, 0, VerificationStatus.VerifyNotAvailable);
        }

        var args = BuildArguments(sourcePath, targetPath, capability);
        if (args.Length == 1 && string.Equals(args[0], "__invalid_command__", StringComparison.Ordinal))
        {
            return new ToolInvocationResult(false, null, -1, null, "invalid-command", 0, VerificationStatus.NotAttempted);
        }

        var watch = Stopwatch.StartNew();
        var result = _tools.InvokeProcess(toolPath, args, toolName);
        watch.Stop();

        // SEC-CONV-06: Check cancellation after long-running tool invocation
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.Success)
        {
            return new ToolInvocationResult(
                false,
                null,
                result.ExitCode,
                result.Output,
                result.Output,
                watch.ElapsedMilliseconds,
                VerificationStatus.NotAttempted);
        }

        return new ToolInvocationResult(
            true,
            targetPath,
            result.ExitCode,
            result.Output,
            null,
            watch.ElapsedMilliseconds,
            VerificationStatus.NotAttempted);
    }

    public VerificationStatus Verify(string targetPath, ConversionCapability capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(capability);

        if (!File.Exists(targetPath))
            return VerificationStatus.VerifyFailed;

        return capability.Verification switch
        {
            VerificationMethod.None => VerificationStatus.NotAttempted,
            VerificationMethod.FileExistenceCheck => new FileInfo(targetPath).Length > 0
                ? VerificationStatus.Verified
                : VerificationStatus.VerifyFailed,
            VerificationMethod.RvzMagicByte => VerifyRvz(targetPath)
                ? VerificationStatus.Verified
                : VerificationStatus.VerifyFailed,
            VerificationMethod.SevenZipTest => VerifySevenZip(targetPath),
            VerificationMethod.ChdmanVerify => VerifyChd(targetPath),
            _ => VerificationStatus.VerifyNotAvailable
        };
    }

    private string[] BuildArguments(string sourcePath, string targetPath, ConversionCapability capability)
    {
        var toolName = capability.Tool.ToolName.ToLowerInvariant();
        var command = ReadSafeCommandToken(capability.Command);

        if (command is null)
            return ["__invalid_command__"];

        if (toolName == "chdman")
        {
            var chdCommand = command;
            if (string.Equals(command, "createdvd", StringComparison.OrdinalIgnoreCase)
                && IsLikelyCdImage(sourcePath))
            {
                chdCommand = "createcd";
            }

            return [chdCommand, "-i", sourcePath, "-o", targetPath];
        }

        if (toolName == "dolphintool")
        {
            return [
                command,
                "-i", sourcePath,
                "-o", targetPath,
                "-f", "rvz",
                "-c", "zstd",
                "-l", "5",
                "-b", "131072"
            ];
        }

        if (toolName == "7z")
        {
            return ["a", "-tzip", "-y", targetPath, sourcePath];
        }

        if (toolName == "psxtract")
        {
            return [command, "-i", sourcePath, "-o", targetPath];
        }

        return [command, "-i", sourcePath, "-o", targetPath];
    }

    private static string? ReadSafeCommandToken(string rawCommand)
    {
        var token = rawCommand.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        if (token.IndexOfAny(['/', '\\']) >= 0)
            return null;

        return token;
    }

    private static string? ValidateToolConstraints(string toolPath, ToolRequirement requirement)
    {
        if (!File.Exists(toolPath))
            return "tool-not-found-on-disk";

        if (!string.IsNullOrWhiteSpace(requirement.ExpectedHash))
        {
            var actualHash = ComputeSha256(toolPath);
            if (!FixedTimeHashEquals(actualHash, requirement.ExpectedHash))
                return "tool-hash-mismatch";
        }

        if (!string.IsNullOrWhiteSpace(requirement.MinVersion))
        {
            var actualVersion = TryReadFileVersion(toolPath);
            if (actualVersion is null)
                return "tool-version-unavailable";

            if (!System.Version.TryParse(requirement.MinVersion, out var requiredVersion))
                return "tool-minversion-invalid";

            if (actualVersion < requiredVersion)
                return "tool-version-too-old";
        }

        return null;
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var bytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool FixedTimeHashEquals(string actualHash, string expectedHash)
    {
        var a = Encoding.ASCII.GetBytes(actualHash.ToLowerInvariant());
        var e = Encoding.ASCII.GetBytes(expectedHash.ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(a, e);
    }

    private static System.Version? TryReadFileVersion(string toolPath)
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(toolPath);

        if (System.Version.TryParse(versionInfo.FileVersion, out var fileVersion))
            return fileVersion;

        if (System.Version.TryParse(versionInfo.ProductVersion, out var productVersion))
            return productVersion;

        return null;
    }

    private static bool IsLikelyCdImage(string sourcePath)
    {
        try
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (ext is not (".iso" or ".bin" or ".img"))
                return false;

            const long ps2CdThreshold = 700L * 1024 * 1024;
            var size = new FileInfo(sourcePath).Length;
            return size > 0 && size < ps2CdThreshold;
        }
        catch
        {
            return false;
        }
    }

    private VerificationStatus VerifyChd(string targetPath)
    {
        var chdman = _tools.FindTool("chdman");
        if (chdman is null)
            return VerificationStatus.VerifyNotAvailable;

        var result = _tools.InvokeProcess(chdman, ["verify", "-i", targetPath], "chdman verify");
        return result.Success ? VerificationStatus.Verified : VerificationStatus.VerifyFailed;
    }

    private VerificationStatus VerifySevenZip(string targetPath)
    {
        var sevenZip = _tools.FindTool("7z");
        if (sevenZip is null)
            return VerificationStatus.VerifyNotAvailable;

        var result = _tools.InvokeProcess(sevenZip, ["t", "-y", targetPath], "7z verify");
        return result.Success ? VerificationStatus.Verified : VerificationStatus.VerifyFailed;
    }

    private static bool VerifyRvz(string targetPath)
    {
        var info = new FileInfo(targetPath);
        if (!info.Exists || info.Length < 4)
            return false;

        try
        {
            using var fs = File.OpenRead(targetPath);
            Span<byte> magic = stackalloc byte[4];
            if (fs.ReadAtLeast(magic, 4, throwOnEndOfStream: false) < 4)
                return false;

            return magic[0] == 'R' && magic[1] == 'V' && magic[2] == 'Z' && magic[3] == 0x01;
        }
        catch
        {
            return false;
        }
    }
}
