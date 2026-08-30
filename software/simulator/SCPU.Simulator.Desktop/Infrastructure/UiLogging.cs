using Microsoft.Extensions.Logging;

namespace SCPU.Simulator.Desktop.Infrastructure;

public sealed record UiLogEntry(DateTimeOffset Timestamp, LogLevel Level, string Source, string Message)
{
    public string Time => Timestamp.ToString("HH:mm:ss.fff");
    public string Severity => Level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "FATAL",
        _ => "NONE"
    };
}

public sealed class UiLogStore
{
    private readonly object _sync = new();
    private readonly Queue<UiLogEntry> _entries = [];

    public int Capacity { get; init; } = 1_000;
    public event EventHandler<UiLogEntry>? EntryAdded;

    public IReadOnlyList<UiLogEntry> Snapshot()
    {
        lock (_sync) return [.. _entries];
    }

    internal void Add(UiLogEntry entry)
    {
        lock (_sync)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > Capacity) _entries.Dequeue();
        }
        EntryAdded?.Invoke(this, entry);
    }
}

public sealed class UiLoggerProvider(UiLogStore store) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new UiLogger(categoryName, store);
    public void Dispose() { }

    private sealed class UiLogger(string category, UiLogStore store) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            var message = formatter(state, exception);
            if (exception is not null) message += $" — {exception.Message}";
            var source = category[(category.LastIndexOf('.') + 1)..];
            store.Add(new UiLogEntry(DateTimeOffset.Now, level, source, message));
        }
    }
}
