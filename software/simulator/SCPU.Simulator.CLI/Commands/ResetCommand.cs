using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands
{
    public sealed class ResetCommand(DebugSession session) : Command
    {
        private readonly DebugSession _session = session;

        protected override int Execute(CommandContext context, CancellationToken cancellationToken)
        {
            _session.Reset();
            AnsiConsole.MarkupLine("[green]CPU reset.[/]");
            return 0;
        }
    }
}
