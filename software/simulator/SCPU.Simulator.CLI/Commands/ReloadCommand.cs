using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands
{
    public sealed class ReloadCommand(DebugSession session) : AsyncCommand
    {
        private readonly DebugSession _session = session;

        protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            await AnsiConsole.Status().StartAsync("Reloading...", async _ =>
            {
                await _session.ReloadAsync(cancellationToken);
            });

            AnsiConsole.MarkupLine("[green]Reloaded last image.[/]");
            return 0;
        }
    }
}
