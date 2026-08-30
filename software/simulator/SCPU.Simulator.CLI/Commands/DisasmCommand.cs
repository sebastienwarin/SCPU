using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public sealed class DisasmCommand(DebugSession session) : Command<DisasmCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[ADDRESS]")] public string? Address { get; set; }
        [CommandOption("-c|--count <COUNT>")] [DefaultValue(12)] public int Count { get; set; } = 12;
        [CommandOption("--no-source")] [Description("Hide source file and source text columns.")]
        public bool NoSource { get; set; }

        public override ValidationResult Validate() => Count is < 1 or > 256
            ? ValidationResult.Error("--count must be between 1 and 256.")
            : ValidationResult.Success();
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var start = settings.Address is null ? session.Cpu.ProgramCounter : session.ResolveAddress(settings.Address);
        var count = settings.Count;
        var showSource = !settings.NoSource;
        settings.Address = null;
        settings.Count = 12;
        settings.NoSource = false;

        return Render(session, start, count, showSource);
    }

    internal static int Render(DebugSession session, uint start, int count, bool showSource = true)
    {
        if (start > ushort.MaxValue || start + (uint)count > session.Cpu.ROM.Length)
            throw new ArgumentOutOfRangeException(nameof(start), "Disassembly range is outside ROM.");

        var entries = session.Program?.Rom;
        var labels = (session.Program?.AssemblyArtifact?.Labels ?? new Dictionary<string, uint>())
            .GroupBy(pair => pair.Value)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(pair => pair.Key)));
        var table = new Table().Border(TableBorder.Simple);
        table.AddColumn(new TableColumn(" ").NoWrap());
        table.AddColumn(new TableColumn("Address").NoWrap());
        table.AddColumn(new TableColumn("Word").NoWrap());
        table.AddColumn(new TableColumn("Instruction").NoWrap());
        table.AddColumn(showSource ? "Symbol / source" : "Symbol");

        SourceLocation? previousSource = null;
        for (var address = start; address < start + count; address++)
        {
            var word = session.Cpu.ROM[address];
            var entry = entries is not null && address < entries.Count ? entries[(int)address] : null;
            var source = entry?.Source;
            var showThisSource = source is not null && !SameSource(previousSource, source);
            labels.TryGetValue(address, out var label);
            var annotation = FormatAnnotation(label, showThisSource ? source : null, showSource);
            var row = new List<string>
            {
                $"{(address == session.Cpu.ProgramCounter ? "[yellow]>[/]" : " ")}" +
                $"{(session.Breakpoints.Contains((ushort)address) ? "[red]●[/]" : " ")}",
                $"0x{address:X4}",
                $"0x{word:X4}",
                entry?.IsData == true
                    ? $"[grey]{Markup.Escape(InstructionFormatter.FormatDataInstruction(word))}[/]"
                    : Markup.Escape(InstructionFormatter.FormatWithVirtualAddresses(word)),
                annotation
            };
            table.AddRow(row.ToArray());
            previousSource = source;
        }

        AnsiConsole.Write(table);
        if (showSource && session.Program?.Type == ProgramFileType.SCode)
            AnsiConsole.MarkupLine("[grey]Source locations refer to generated assembly; S-Code line mappings are not emitted by the compiler yet.[/]");
        return 0;
    }

    private static bool SameSource(SourceLocation? left, SourceLocation right) =>
        left is not null && left.Line == right.Line &&
        string.Equals(left.Identifier, right.Identifier, StringComparison.OrdinalIgnoreCase);

    private static string FormatLocation(SourceLocation source)
    {
        var identifier = Path.GetFileName(source.Identifier);
        if (string.IsNullOrEmpty(identifier)) identifier = source.Identifier;
        return $"{identifier}:{source.Line}";
    }

    private static string FormatAnnotation(string? label, SourceLocation? source, bool showSource)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(label)) parts.Add($"[cyan]{Markup.Escape(label)}[/]");
        if (showSource && source is not null)
            parts.Add($"[grey]{Markup.Escape(FormatLocation(source))}[/] {Markup.Escape(source.Content.Trim())}");
        return string.Join(Environment.NewLine, parts);
    }
}
