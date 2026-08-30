using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public sealed class TerminalInputCommand(BufferedTerminalDevice terminal) : Command<TerminalInputCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<TEXT>")] public string Text { get; init; } = default!;
        [CommandOption("-n|--new-line")] [Description("Append an Enter key after the text.")]
        public bool NewLine { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        bool newLine = settings.NewLine;
        settings.NewLine = false;
        terminal.Enqueue(settings.Text, newLine);
        AnsiConsole.MarkupLine($"[green]Queued {settings.Text.Length + (newLine ? 1 : 0)} terminal character(s).[/]");
        return 0;
    }
}

public sealed class TerminalStatusCommand(BufferedTerminalDevice terminal) : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"Pending input: [cyan]{terminal.PendingInput}[/] character(s)");
        AnsiConsole.WriteLine("Captured output:");
        AnsiConsole.Write(new Panel(Markup.Escape(terminal.Output)).Border(BoxBorder.Rounded));
        return 0;
    }
}

public sealed class TerminalClearCommand(BufferedTerminalDevice terminal) : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        terminal.Reset();
        AnsiConsole.MarkupLine("[green]Terminal buffers cleared.[/]");
        return 0;
    }
}
