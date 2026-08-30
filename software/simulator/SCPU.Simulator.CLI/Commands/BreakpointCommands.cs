using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public abstract class BreakpointCommand(DebugSession session) { protected DebugSession Session { get; } = session; }

public sealed class BreakAddCommand(DebugSession session) : Command<BreakAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ADDRESS>")] public string Address { get; init; } = default!;
    }
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        uint address = DebugSession.IsSourceLocation(settings.Address)
            ? session.ResolveSourceAddresses(settings.Address)[0]
            : session.ResolveAddress(settings.Address);
        if (address > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(settings.Address), "A breakpoint must target ROM.");
        bool added = session.Breakpoints.Add((ushort)address);
        AnsiConsole.MarkupLine(added ? $"[green]Breakpoint added[/] at 0x{address:X4}." : $"[grey]Breakpoint already exists[/] at 0x{address:X4}.");
        return 0;
    }
}

public sealed class BreakDeleteCommand(DebugSession session) : Command<BreakDeleteCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ADDRESS>")] public string Address { get; init; } = default!;
    }
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        uint address = DebugSession.IsSourceLocation(settings.Address)
            ? session.ResolveSourceAddresses(settings.Address)[0]
            : session.ResolveAddress(settings.Address);
        bool removed = address <= ushort.MaxValue && session.Breakpoints.Remove((ushort)address);
        AnsiConsole.MarkupLine(removed ? $"[green]Breakpoint removed[/] from 0x{address:X4}." : "[yellow]No breakpoint at that address.[/]");
        return removed ? 0 : 1;
    }
}

public sealed class BreakListCommand(DebugSession session) : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        if (session.Breakpoints.Count == 0) { AnsiConsole.MarkupLine("[grey]No breakpoints.[/]"); return 0; }
        var symbols = session.Symbols.GroupBy(x => x.Value).ToDictionary(x => x.Key, x => string.Join(", ", x.Select(y => y.Key)));
        var table = new Table().AddColumns("Address", "Symbol", "Source");
        foreach (ushort address in session.Breakpoints.Order())
        {
            var source = session.Program?.Rom.FirstOrDefault(entry => entry.Address == address)?.Source;
            table.AddRow($"0x{address:X4}", symbols.TryGetValue(address, out var label) ? Markup.Escape(label) : "",
                source is null ? "" : Markup.Escape($"{Path.GetFileName(source.Identifier)}:{source.Line}"));
        }
        AnsiConsole.Write(table);
        return 0;
    }
}

public sealed class BreakClearCommand(DebugSession session) : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        int count = session.Breakpoints.Count;
        session.Breakpoints.Clear();
        AnsiConsole.MarkupLine($"[green]Cleared {count} breakpoint(s).[/]");
        return 0;
    }
}
