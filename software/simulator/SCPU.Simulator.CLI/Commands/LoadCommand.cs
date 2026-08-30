using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace SCPU.Simulator.CLI.Commands
{
    public sealed class LoadCommand(DebugSession session) : AsyncCommand<LoadCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<FILE>")]
            [Description("Path to ROM / .asm / .scode file.")]
            public string Path { get; init; } = default!;
        }

        private readonly DebugSession _session = session;

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var fi = new FileInfo(settings.Path);
            if (!fi.Exists)
            {
                AnsiConsole.MarkupLine("[red]File not found[/]: " + settings.Path);
                return -1;
            }

            await AnsiConsole.Status().StartAsync("Loading...", async _ =>
            {
                await _session.LoadAsync(fi, cancellationToken);
            });

            AnsiConsole.MarkupLine("[green]Loaded.[/]");
            return 0;
        }
    }
}
