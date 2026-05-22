using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace RagTools.Tests.E2E;

// ──────────────────────────────────────────────────────────────────────────────
// xUnit logging sink — mirrors the pattern used in ECommerceApp integration tests
// (ECommerceApp.Shared.TestInfrastructure.XunitLogSink / XunitLoggerProvider).
//
// How it works:
//   1. Each class fixture (IngestE2EFixture, HttpIngestE2EFixture, SharedOnnxFixture)
//      owns one XunitLogSink.
//   2. Each test-class constructor receives an ITestOutputHelper from xUnit and
//      calls fixture.Sink.SetOutput(output).
//   3. ILoggerProvider / ILogger instances route log messages to that output.
//   4. The test-class Dispose() calls fixture.Sink.SetOutput(null) so messages
//      between tests are silently dropped rather than going to the wrong test.
//
// Thread safety: all SetOutput / Write calls are locked so parallel fixtures
// cannot corrupt each other's output pointers.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Thread-safe bridge between an <see cref="ILogger"/> and xUnit's
/// <see cref="ITestOutputHelper"/>.  The output can be hot-swapped between
/// tests without re-creating the logger infrastructure.
/// </summary>
public sealed class XunitLogSink
{
    private ITestOutputHelper? _output;

    /// <summary>
    /// Redirect subsequent log writes to <paramref name="output"/>.
    /// Call with <c>null</c> to silence logging between tests.
    /// </summary>
    public void SetOutput(ITestOutputHelper? output)
    {
        lock (this) { _output = output; }
    }

    /// <summary>Write one line to the active output.  No-op when output is null.</summary>
    internal void Write(string message)
    {
        ITestOutputHelper? output;
        lock (this) { output = _output; }
        try { output?.WriteLine(message); }
        catch { /* xUnit output can throw if the test has already completed */ }
    }
}

// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// <see cref="ILoggerProvider"/> that writes to a <see cref="XunitLogSink"/>.
/// Register via <c>builder.Logging.AddProvider(new XunitLoggerProvider(fixture.Sink))</c>.
/// </summary>
public sealed class XunitLoggerProvider(XunitLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new XunitLogger(sink, TrimCategory(categoryName));

    public void Dispose() { }

    // Strip long namespace prefixes so the output stays readable.
    private static string TrimCategory(string cat) =>
        cat.Length > 48 ? "…" + cat[^45..] : cat;
}

// ──────────────────────────────────────────────────────────────────────────────

internal sealed class XunitLogger(XunitLogSink sink, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var level = logLevel switch
        {
            LogLevel.Trace       => "trc",
            LogLevel.Debug       => "dbg",
            LogLevel.Information => "inf",
            LogLevel.Warning     => "WRN",
            LogLevel.Error       => "ERR",
            LogLevel.Critical    => "CRT",
            _                    => "   ",
        };

        sink.Write($"[{level}] {category}: {formatter(state, exception)}");
        if (exception is not null)
            sink.Write(exception.ToString());
    }
}
