using Microsoft.Extensions.Logging;
using System;
using Xunit.Abstractions;

namespace ECommerceApp.Shared.TestInfrastructure
{
    public sealed class XunitLogSink
    {
        private ITestOutputHelper _output;

        public void SetOutput(ITestOutputHelper output)
        {
            lock (this) { _output = output; }
        }

        internal void Write(string message)
        {
            ITestOutputHelper output;
            lock (this) { output = _output; }
            try { output?.WriteLine(message); } catch { }
        }
    }

    public interface IHaveXunitSink
    {
        XunitLogSink Sink { get; }
    }

    public sealed class XunitLoggerProvider : ILoggerProvider
    {
        private readonly XunitLogSink _sink;

        public XunitLoggerProvider(XunitLogSink sink) => _sink = sink;

        public ILogger CreateLogger(string categoryName) => new XunitLogger(_sink, categoryName);

        public void Dispose() { }
    }

    public sealed class XunitLogger : ILogger
    {
        private readonly XunitLogSink _sink;
        private readonly string _category;

        public XunitLogger(XunitLogSink sink, string category)
        {
            _sink = sink;
            _category = category;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Trace;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _sink.Write($"[{logLevel}] {_category}: {message}");
            if (exception != null)
            {
                _sink.Write(exception.ToString());
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}

