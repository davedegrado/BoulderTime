using Microsoft.Extensions.Logging;

namespace BoulderTime.Tests.Infrastructure;

/// <summary>A logger for tests that only need somewhere for the messages to go.</summary>
public sealed class CapturingLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) =>
        Messages.Add(formatter(state, error));
}
