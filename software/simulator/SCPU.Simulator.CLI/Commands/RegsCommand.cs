using SCPU.Architecture;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands
{
    public sealed class RegsCommand(DebugSession session) : Command<RegsCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-v|--verbose")]
            public bool Verbose { get; set; }
        }

        private readonly DebugSession _session = session;

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var verbose = settings.Verbose;
            settings.Verbose = false;
            return Render(_session, verbose);
        }

        public static int Render(DebugSession s, bool verbose = false)
        {
            var t = new Table().Border(TableBorder.Rounded);
            t.Title("CPU Registers");
            t.AddColumn("Name");
            t.AddColumn("Value");

            t.AddRow("Ticks", $"{s.TickCount}");
            t.AddRow("PC", $"[cyan]0x{s.Cpu.ProgramCounter:X4}[/]");
            t.AddRow("Step", s.Cpu.StepCounter.ToString());
            t.AddRow("IR", $"[cyan]0x{s.Cpu.InstructionRegister:X4}[/]");
            t.AddRow("ACC", $"[yellow]0x{s.Cpu.AccumulatorRegister:X4}[/]");
            t.AddRow("Flags", $"C={(s.Cpu.CarryFlag ? 1 : 0)}  IND={(s.Cpu.IndirectedFlag ? 1 : 0)}");
            t.AddRow("SP", FormatPointer(s.Cpu.LookupValue(ReservedAddresses.StackPointer)));
            t.AddRow("FP", FormatPointer(s.Cpu.LookupValue(ReservedAddresses.FramePointer)));
            if (verbose)
            {
                var snapshot = s.Snapshot();
                t.AddRow("Instruction", Markup.Escape($"{snapshot.Instruction.ToMnemonic().ToUpperInvariant()} / {snapshot.AddressingMode}"));
                t.AddRow("Operand", $"0x{snapshot.Operand:X4}");
                t.AddRow("Data bus", $"0x{snapshot.DataBus:X4}");
                t.AddRow("ALU operand", $"0x{snapshot.AluOperand:X4}");
                t.AddRow("Data source", Markup.Escape(snapshot.DataSource));
                t.AddRow("Next word", $"0x{snapshot.NextInstruction:X4}");
                t.AddRow("Instructions", snapshot.Instructions.ToString());
                t.AddRow("State", snapshot.StopReason is null ? snapshot.State.ToString() : $"{snapshot.State} ({snapshot.StopReason})");
            }

            AnsiConsole.Write(t);
            return 0;
        }

        private static string FormatPointer(ushort value) => value is >= 0x2000 and <= 0x27FF
            ? $"0x{MemoryMap.VirtualAddressBias + value:X5}"
            : $"0x{value:X4}";
    }
}
