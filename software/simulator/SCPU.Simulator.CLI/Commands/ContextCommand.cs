using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public sealed class ContextCommand(DebugSession session) : Command<ContextCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-c|--code <COUNT>")] [DefaultValue(8)] public int Code { get; set; } = 8;
        [CommandOption("-s|--stack <COUNT>")] [DefaultValue(4)] public int Stack { get; set; } = 4;
        public override ValidationResult Validate() => Code is < 1 or > 64 || Stack is < 0 or > 64
            ? ValidationResult.Error("--code must be 1..64 and --stack 0..64.")
            : ValidationResult.Success();
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var code = settings.Code;
        var stackCount = settings.Stack;
        settings.Code = 8;
        settings.Stack = 4;
        AnsiConsole.MarkupLine($"[bold]Context[/]  state=[cyan]{session.State}[/]  pc=[cyan]0x{session.Cpu.ProgramCounter:X4}[/]");
        RegsCommand.Render(session, verbose: true);
        var start = session.Cpu.ProgramCounter > code / 2 ? (uint)(session.Cpu.ProgramCounter - code / 2) : 0;
        DisasmCommand.Render(session, start, Math.Min(code, session.Cpu.ROM.Length - (int)start));

        if (session.Watches.Count > 0)
        {
            var watches = new Table().Title("Watches").AddColumns("Expression", "Address", "Value");
            foreach (var watch in session.Watches.OrderBy(item => item.Id))
                watches.AddRow(Markup.Escape(watch.Expression), $"0x{watch.Address:X5}", $"0x{session.ReadMemory(watch.Address):X4}");
            AnsiConsole.Write(watches);
        }
        if (stackCount > 0)
        {
            var stack = session.GetStack(stackCount);
            var table = new Table().Title("Stack").AddColumns("Address", "Value");
            foreach (var entry in stack.Entries)
                table.AddRow($"{(entry.IsFramePointer ? "FP " : "")}0x{entry.Address:X5}", $"0x{entry.Value:X4}");
            AnsiConsole.Write(table);
        }
        return 0;
    }
}
