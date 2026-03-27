using System.Diagnostics;
using RomCleanup.Contracts.Models;
using RomCleanup.Contracts.Ports;

namespace RomCleanup.Infrastructure.Conversion.ToolInvokers;

public sealed class PsxtractInvoker(IToolRunner tools) : IToolInvoker
{
    private readonly IToolRunner _tools = tools ?? throw new ArgumentNullException(nameof(tools));

    public bool CanHandle(ConversionCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return string.Equals(capability.Tool.ToolName, "psxtract", StringComparison.OrdinalIgnoreCase);
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
            return ToolInvokerSupport.SourceNotFound();

        var commandToken = ToolInvokerSupport.ReadSafeCommandToken(capability.Command);
        if (commandToken is null)
            return ToolInvokerSupport.InvalidCommand();

        var toolPath = _tools.FindTool("psxtract");
        if (string.IsNullOrWhiteSpace(toolPath))
            return ToolInvokerSupport.ToolNotFound("psxtract");

        var constraintError = ToolInvokerSupport.ValidateToolConstraints(toolPath, capability.Tool);
        if (constraintError is not null)
            return ToolInvokerSupport.ConstraintFailure(constraintError);

        var watch = Stopwatch.StartNew();
        var result = _tools.InvokeProcess(toolPath, [commandToken, "-i", sourcePath, "-o", targetPath], "psxtract");
        watch.Stop();

        return ToolInvokerSupport.FromToolResult(targetPath, result, watch);
    }

    public VerificationStatus Verify(string targetPath, ConversionCapability capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(capability);

        if (!File.Exists(targetPath))
            return VerificationStatus.VerifyFailed;

        // V07 audit fix: verify CHD magic bytes ("MComprHD") instead of just file existence
        try
        {
            using var stream = File.OpenRead(targetPath);
            if (stream.Length < 8)
                return VerificationStatus.VerifyFailed;

            Span<byte> magic = stackalloc byte[8];
            var read = stream.ReadAtLeast(magic, 8, throwOnEndOfStream: false);
            if (read < 8)
                return VerificationStatus.VerifyFailed;

            return magic.SequenceEqual("MComprHD"u8)
                ? VerificationStatus.Verified
                : VerificationStatus.VerifyFailed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return VerificationStatus.VerifyFailed;
        }
    }
}
