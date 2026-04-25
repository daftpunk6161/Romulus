using System.Diagnostics;

namespace Romulus.Tests.TestFixtures;

/// <summary>
/// Block D6 - centralized System.Diagnostics.Trace capture utility.
/// Extracted from <c>AuditABEndToEndRedTests.CaptureTrace</c> so multiple
/// test suites can verify trace/log emission without duplicating the
/// listener boilerplate.
/// </summary>
internal static class TraceCapture
{
    /// <summary>
    /// Run <paramref name="action"/> while capturing all output written
    /// through <see cref="Trace"/>. Listener is added/removed and
    /// <see cref="Trace.AutoFlush"/> restored even on exceptions.
    /// </summary>
    public static string Capture(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var writer = new StringWriter();
        using var listener = new TextWriterTraceListener(writer);
        Trace.Listeners.Add(listener);
        var previousAutoFlush = Trace.AutoFlush;
        Trace.AutoFlush = true;

        try
        {
            action();
            listener.Flush();
            return writer.ToString();
        }
        finally
        {
            Trace.AutoFlush = previousAutoFlush;
            Trace.Listeners.Remove(listener);
        }
    }
}
