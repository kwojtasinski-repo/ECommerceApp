using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RagTools.Core;
using Xunit;

namespace RagTools.Tests;

public class TextSanitizerTests
{
    [Fact]
    public void RemoveReplacementChars_NoReplacementChar_ReturnsSameString()
    {
        var input = "Hello world\nNo garbage here.";
        var result = TextSanitizer.RemoveReplacementChars(input);
        Assert.Same(input, result); // optimization: returns same reference
    }

    [Fact]
    public void RemoveReplacementChars_WithReplacementChar_ReplacesWithQuestionMark()
    {
        var input = "Caf\uFFFD au lait";
        var result = TextSanitizer.RemoveReplacementChars(input);
        Assert.Equal("Caf? au lait", result);
    }

    [Fact]
    public void RemoveReplacementChars_MultipleOccurrences_AllReplaced()
    {
        var input = "\uFFFD\uFFFD\uFFFD";
        var result = TextSanitizer.RemoveReplacementChars(input);
        Assert.Equal("???", result);
    }

    [Fact]
    public void RemoveReplacementChars_Empty_ReturnsEmpty()
    {
        Assert.Equal("", TextSanitizer.RemoveReplacementChars(""));
    }

    [Fact]
    public void RemoveReplacementChars_WithLogger_NoGarbage_DoesNotLog()
    {
        var logger = new CapturingLogger();
        var input = "all good";
        var result = TextSanitizer.RemoveReplacementChars(input, "docs/x.md", logger);
        Assert.Same(input, result);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void RemoveReplacementChars_WithLogger_GarbageFound_LogsWarningWithCount()
    {
        var logger = new CapturingLogger();
        var input = "x\uFFFDy\uFFFDz";
        var result = TextSanitizer.RemoveReplacementChars(input, "docs/bad.md", logger);
        Assert.Equal("x?y?z", result);
        Assert.Single(logger.Warnings);
        var msg = logger.Warnings[0];
        Assert.Contains("docs/bad.md", msg);
        Assert.Contains("2", msg); // count
        Assert.Contains("U+FFFD", msg);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
