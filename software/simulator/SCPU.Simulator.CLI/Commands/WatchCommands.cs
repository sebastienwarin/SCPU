using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public sealed class WatchAddCommand(DebugSession session) : Command<WatchAddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ADDRESSES>")]
        [System.ComponentModel.Description("One or more addresses/symbols; accepts comma lists and START..END ranges.")]
        public string[] Addresses { get; set; } = [];

        [CommandOption("--to <END>")]
        [System.ComponentModel.Description("Inclusive end address for a single start address.")]
        public string? End { get; set; }

        public override ValidationResult Validate() => Addresses.Length == 0
            ? ValidationResult.Error("Provide at least one address.")
            : End is not null && (Addresses.Length != 1 || Addresses[0].Contains(',') || Addresses[0].Contains(".."))
                ? ValidationResult.Error("--to accepts exactly one start address; lists and START..END ranges must omit --to.")
                : ValidationResult.Success();
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var addresses = settings.Addresses;
        var end = settings.End;
        settings.Addresses = [];
        settings.End = null;

        var before = session.Watches.Count;
        foreach (var expression in Expand(addresses, end)) session.AddWatch(expression);
        var added = session.Watches.Count - before;
        AnsiConsole.MarkupLine(added == 0
            ? "[grey]All requested watches already exist.[/]"
            : $"[green]Added {added} watch(es).[/]");
        return 0;
    }

    private IEnumerable<string> Expand(string[] addresses, string? end)
    {
        if (end is null && addresses is [var rangeStart, "..", var rangeEnd])
        {
            foreach (var address in ExpandRange(rangeStart, rangeEnd)) yield return $"0x{address:X5}";
            yield break;
        }

        var expressions = addresses.SelectMany(value =>
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        foreach (var expression in expressions)
        {
            var range = expression.Split("..", 2, StringSplitOptions.TrimEntries);
            if (range.Length == 2)
                foreach (var address in ExpandRange(range[0], range[1])) yield return $"0x{address:X5}";
            else if (end is not null)
                foreach (var address in ExpandRange(expression, end)) yield return $"0x{address:X5}";
            else
                yield return expression;
        }
    }

    private IEnumerable<uint> ExpandRange(string startExpression, string endExpression)
    {
        var start = session.ResolveAddress(startExpression);
        var end = session.ResolveAddress(endExpression);
        if (end < start) throw new ArgumentException("Watch range end must be greater than or equal to its start.");
        if (end - start + 1 > 1024) throw new ArgumentException("A watch range is limited to 1024 addresses.");
        for (var address = start;; address++)
        {
            yield return address;
            if (address == end) break;
        }
    }
}

public sealed class WatchDeleteCommand(DebugSession session) : Command<WatchDeleteCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ID_OR_ADDRESS>")] public string Value { get; init; } = default!;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        Watch? watch = int.TryParse(settings.Value, out var id)
            ? session.Watches.FirstOrDefault(item => item.Id == id)
            : session.Watches.FirstOrDefault(item => item.Address == session.ResolveAddress(settings.Value));
        if (watch is null)
        {
            AnsiConsole.MarkupLine("[yellow]Watch not found.[/]");
            return 1;
        }
        session.Watches.Remove(watch);
        AnsiConsole.MarkupLine($"[green]Watch #{watch.Id} removed.[/]");
        return 0;
    }
}

public sealed class WatchListCommand(DebugSession session) : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        if (session.Watches.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No watches.[/]");
            return 0;
        }
        var table = new Table().AddColumns("Id", "Expression", "Address", "Hex", "Decimal");
        foreach (var watch in session.Watches.OrderBy(item => item.Id))
        {
            var value = session.ReadMemory(watch.Address);
            table.AddRow(watch.Id.ToString(), Markup.Escape(watch.Expression), $"0x{watch.Address:X5}", $"0x{value:X4}", value.ToString());
        }
        AnsiConsole.Write(table);
        return 0;
    }
}

public sealed class WatchClearCommand(DebugSession session) : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        var count = session.Watches.Count;
        session.Watches.Clear();
        AnsiConsole.MarkupLine($"[green]Cleared {count} watch(es).[/]");
        return 0;
    }
}
