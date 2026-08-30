using System.ComponentModel;
using SCPU.Architecture;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public sealed class SymbolsCommand(DebugSession session) : Command<SymbolsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[FILTER]")]
        [Description("Case-insensitive part of a symbol name.")]
        public string? Filter { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var filter = settings.Filter;
        settings.Filter = null;
        var symbols = session.Symbols
            .Where(pair => string.IsNullOrWhiteSpace(filter) || pair.Key.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (symbols.Count == 0)
        {
            AnsiConsole.MarkupLine(session.Program is null
                ? "[yellow]No program loaded.[/]"
                : "[grey]No matching symbols.[/]");
            return 0;
        }

        var labels = session.Program?.AssemblyArtifact?.Labels;
        var constants = session.Program?.AssemblyArtifact?.Constants;
        var table = new Table().Border(TableBorder.Simple).AddColumns("Address", "Kind", "Symbol");
        foreach (var (name, address) in symbols)
        {
            var kind = labels?.ContainsKey(name) == true ? "label" : constants?.ContainsKey(name) == true ? "constant" : "symbol";
            table.AddRow(FormatAddress(address), kind, Markup.Escape(name));
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]{symbols.Count} symbol(s).[/]");
        return 0;
    }

    private static string FormatAddress(uint address) =>
        Addressing.TryTranslateVirtualAddress(address, Addressing.AddressView.PhysicalOffset, out _, out _)
            ? $"0x{address:X5}"
            : $"0x{address:X}";
}
