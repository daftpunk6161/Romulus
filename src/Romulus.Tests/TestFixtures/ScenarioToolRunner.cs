using Romulus.Contracts.Ports;

namespace Romulus.Tests.TestFixtures;

/// <summary>
/// Block D3 - centralized <see cref="IToolRunner"/> test double covering the
/// canonical conversion failure scenarios (Crash, HashMismatch, Cancellation,
/// OutputTooSmall, DiskFull). Replaces locally duplicated stubs with a single
/// configurable factory.
///
/// Use <see cref="ForScenario(ConversionFailureScenario)"/> for the most common
/// case, or supply a custom <see cref="Func{T1, T2, T3}"/> for bespoke matrices.
/// </summary>
internal sealed class ScenarioToolRunner : IToolRunner
{
    private readonly Func<string, string[], ToolResult> _invoke;
    private readonly Dictionary<string, string> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ScenarioToolRunner(Func<string, string[], ToolResult> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);
        _invoke = invoke;
    }

    public void RegisterTool(string toolName, string path) => _tools[toolName] = path;
    public string? FindTool(string toolName) => _tools.TryGetValue(toolName, out var p) ? p : null;

    public ToolResult InvokeProcess(string filePath, string[] arguments, string? errorLabel = null)
        => _invoke(filePath, arguments);

    public ToolResult Invoke7z(string sevenZipPath, string[] arguments)
        => _invoke(sevenZipPath, arguments);

    /// <summary>
    /// Build a <see cref="ScenarioToolRunner"/> for one of the canonical
    /// conversion failure scenarios. Output side-effects (e.g. writing a
    /// truncated file for <see cref="ConversionFailureScenario.OutputTooSmall"/>)
    /// must be staged by the caller via <paramref name="outputPathProvider"/>.
    /// </summary>
    public static ScenarioToolRunner ForScenario(
        ConversionFailureScenario scenario,
        Func<string, string[], string?>? outputPathProvider = null)
    {
        return scenario switch
        {
            ConversionFailureScenario.Crash =>
                new ScenarioToolRunner((_, _) => throw new InvalidOperationException("Tool crashed.")),

            ConversionFailureScenario.HashMismatch =>
                // Tool reports success but produces output with unexpected hash.
                // The caller's verify step is what should detect this; the runner
                // only signals "succeeded" here.
                new ScenarioToolRunner((_, _) => new ToolResult(0, "ok", true)),

            ConversionFailureScenario.Cancellation =>
                new ScenarioToolRunner((_, _) => throw new OperationCanceledException()),

            ConversionFailureScenario.OutputTooSmall =>
                new ScenarioToolRunner((file, args) =>
                {
                    var outPath = outputPathProvider?.Invoke(file, args);
                    if (!string.IsNullOrWhiteSpace(outPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                        File.WriteAllBytes(outPath, [0x00]); // 1 byte = clearly too small
                    }
                    return new ToolResult(0, "ok", true);
                }),

            ConversionFailureScenario.DiskFull =>
                new ScenarioToolRunner((_, _) =>
                    new ToolResult(112 /* ERROR_DISK_FULL */, "There is not enough space on the disk.", false)),

            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
    }
}

/// <summary>
/// Block D3 - canonical conversion failure scenarios used in safety-/verify-
/// related tests across the conversion subsystem.
/// </summary>
internal enum ConversionFailureScenario
{
    Crash,
    HashMismatch,
    Cancellation,
    OutputTooSmall,
    DiskFull
}
