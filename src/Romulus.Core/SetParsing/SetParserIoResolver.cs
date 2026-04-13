using Romulus.Contracts.Ports;

namespace Romulus.Core.SetParsing;

public static class SetParserIoResolver
{
    private static readonly object DefaultGate = new();
    private static Lazy<ISetParserIo> _defaultIo = new(CreateUnconfiguredIo, LazyThreadSafetyMode.ExecutionAndPublication);
    private static bool _isConfigured;

    public static void ConfigureDefault(Func<ISetParserIo> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        const string message = "Set parser I/O default is already configured and cannot be changed at runtime.";

        lock (DefaultGate)
        {
            if (_isConfigured)
                throw new InvalidOperationException(message);

            _defaultIo = new Lazy<ISetParserIo>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
            _isConfigured = true;
        }
    }

    internal static ISetParserIo Resolve(ISetParserIo? io)
        => io ?? _defaultIo.Value;

    private static ISetParserIo CreateUnconfiguredIo()
        => new UnconfiguredSetParserIo();

    private sealed class UnconfiguredSetParserIo : ISetParserIo
    {
        private const string Message = "Set parser I/O is not configured. Inject ISetParserIo from Infrastructure before invoking parser logic.";

        public bool Exists(string path)
            => throw new InvalidOperationException(Message);

        public IEnumerable<string> ReadLines(string path)
            => throw new InvalidOperationException(Message);
    }
}
