using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public sealed class MemCommand(DebugSession session) : Command<MemCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ADDRESS>")] public string Address { get; set; } = default!;
        [CommandOption("-c|--count <COUNT>")] [DefaultValue(16)] public int Count { get; set; } = 16;
        [CommandOption("-w|--write <VALUE>")] public string? Value { get; set; }

        public override ValidationResult Validate() => Count is < 1 or > 1024
            ? ValidationResult.Error("--count must be between 1 and 1024.")
            : ValidationResult.Success();
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        uint address = session.ResolveAddress(settings.Address);
        string? valueText = settings.Value;
        int count = settings.Count;
        settings.Value = null; // CommandApp reuses settings in shell and chained modes.
        settings.Count = 16;
        if (valueText is not null)
        {
            ushort value = ParseWord(valueText);
            session.WriteMemory(address, value);
            AnsiConsole.MarkupLine($"[green]0x{address:X5}[/] = 0x{value:X4}");
            return 0;
        }

        Dump(address, count);
        return 0;
    }

    private void Dump(uint address, int count)
    {
        const int width = 8;
        for (int offset = 0; offset < count; offset += width)
        {
            uint lineAddress = address + (uint)offset;
            var words = Enumerable.Range(0, Math.Min(width, count - offset))
                .Select(i => $"{session.Cpu.LookupValue(lineAddress + (uint)i):X4}");
            AnsiConsole.MarkupLine($"[grey]0x{lineAddress:X5}[/]  {string.Join(' ', words)}");
        }
    }

    private static ushort ParseWord(string text) => text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? Convert.ToUInt16(text[2..], 16)
        : Convert.ToUInt16(text);
}
