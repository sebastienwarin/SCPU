using SCPU.Architecture;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands
{
    public sealed class StackCommand(DebugSession session) : Command<StackCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--count <N>")]
            public uint Count { get; set; } = 0;
        }

        private readonly DebugSession _session = session;

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var count = settings.Count;
            settings.Count = 0;
            var stack = _session.GetStack(count == 0 ? null : checked((int)count));

            var tbl = new Table().AddColumns("Addr", "Word");
            foreach (var entry in stack.Entries)
                tbl.AddRow($"0x{entry.Address:X5}", $"0x{entry.Value:X4}");
            AnsiConsole.Write(tbl);
            return 0;
        }
    }
}
