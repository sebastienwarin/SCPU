using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public sealed class SourceCommand(DebugSession session) : Command<SourceCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[LOCATION]")] public string? Location { get; set; }
        [CommandOption("-a|--around <LINES>")] [DefaultValue(5)] public int Around { get; set; } = 5;

        public override ValidationResult Validate() => Around is < 0 or > 100
            ? ValidationResult.Error("--around must be between 0 and 100.")
            : ValidationResult.Success();
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var location = settings.Location;
        var around = settings.Around;
        settings.Location = null;
        settings.Around = 5;

        if (session.Program is null) throw new InvalidOperationException("No program has been loaded.");
        var address = location is null
            ? session.Cpu.ProgramCounter
            : DebugSession.IsSourceLocation(location) ? session.ResolveSourceAddresses(location)[0] : session.ResolveAddress(location);
        var source = session.Program.Rom.FirstOrDefault(entry => entry.Address == address)?.Source
            ?? throw new InvalidOperationException($"No source is mapped to address 0x{address:X4}.");
        var document = session.Program.SourceDocuments.FirstOrDefault(pair =>
            string.Equals(pair.Key, source.Identifier, StringComparison.OrdinalIgnoreCase)).Value;
        var mappedEntries = session.Program.Rom.Where(entry => entry.Source is not null &&
                string.Equals(entry.Source.Identifier, source.Identifier, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var mappings = mappedEntries
            .GroupBy(entry => entry.Source!.Line)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Address).Distinct().Order().ToArray());
        var sourceLines = document is not null
            ? document.Replace("\r\n", "\n").Split('\n').Select((text, index) => (Line: index + 1, Text: text))
            : mappedEntries.GroupBy(entry => entry.Source!.Line)
                .Select(group => (Line: group.Key, Text: group.First().Source!.Content));
        var visibleLines = sourceLines.Where(item => Math.Abs(item.Line - source.Line) <= around).OrderBy(item => item.Line).ToArray();
        var table = new Table().Border(TableBorder.Simple).Title(Markup.Escape($"{Path.GetFileName(source.Identifier)}:{source.Line}"));
        table.AddColumns(" ", "Line", "ROM", "Source");
        foreach (var item in visibleLines)
        {
            mappings.TryGetValue(item.Line, out var addresses);
            var atPc = addresses?.Contains(session.Cpu.ProgramCounter) == true;
            var atBreakpoint = addresses?.Any(session.Breakpoints.Contains) == true;
            table.AddRow(atPc ? "[yellow]>[/]" : atBreakpoint ? "[red]●[/]" : " ", item.Line.ToString(),
                addresses is null ? "" : string.Join(", ", addresses.Select(value => $"0x{value:X4}")),
                Markup.Escape(item.Text));
        }
        AnsiConsole.Write(table);
        return 0;
    }
}
