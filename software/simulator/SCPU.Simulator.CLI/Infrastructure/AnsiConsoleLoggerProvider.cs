using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace SCPU.Simulator.CLI.Infrastructure
{
    public sealed class AnsiConsoleLoggerProvider(InteractiveConsoleState? consoleState = null) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new AnsiConsoleLogger(categoryName, consoleState);
        public void Dispose() { }

        private sealed class AnsiConsoleLogger(string category, InteractiveConsoleState? consoleState) : ILogger
        {
            private readonly string _category = category;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && consoleState?.IsActive != true;

            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(level)) return;
                var msg = formatter(state, ex);

                var levelTag = level switch
                {
                    LogLevel.Trace => "[grey]trace[/]",
                    LogLevel.Debug => "[grey]debug[/]",
                    LogLevel.Information => "[deepskyblue1]info[/]",
                    LogLevel.Warning => "[yellow]warn[/]",
                    LogLevel.Error => "[red]error[/]",
                    LogLevel.Critical => "[bold red]crit[/]",
                    _ => "log"
                };

                AnsiConsole.MarkupLine($"{levelTag} [dim]{_category}[/]: {Markup.Escape(msg)}");
                if (ex != null)
                {
                    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.ToString())}[/]");
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}
