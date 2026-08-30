using CommunityToolkit.Mvvm.ComponentModel;

namespace SCPU.Simulator.Desktop.ViewModels;

public sealed partial class MemoryRowViewModel(uint address, ushort value, string label = "") : ObservableObject
{
    [ObservableProperty] private ushort _value = value;
    [ObservableProperty] private bool _changed;
    [ObservableProperty] private bool _isWatched;

    public uint RawAddress => address;
    public string Address => $"{address:X5}";
    public string Hex => $"{Value:X4}";
    public string Decimal => Value.ToString();
    public string Ascii => Value is >= 32 and <= 126 ? ((char)Value).ToString() : ".";
    public string Label => label;
    public string ChangeMarker => Changed ? "●" : string.Empty;
    public string WatchMarker => IsWatched ? "●" : string.Empty;

    public void Update(ushort newValue)
    {
        Changed = newValue != Value;
        if (newValue == Value) return;
        Value = newValue;
        OnPropertyChanged(nameof(Hex));
        OnPropertyChanged(nameof(Decimal));
        OnPropertyChanged(nameof(Ascii));
    }

    partial void OnChangedChanged(bool value) => OnPropertyChanged(nameof(ChangeMarker));
    partial void OnIsWatchedChanged(bool value) => OnPropertyChanged(nameof(WatchMarker));
}

public sealed partial class WatchRowViewModel(uint address, string label) : ObservableObject
{
    [ObservableProperty] private ushort _value;

    public uint RawAddress => address;
    public string Address => $"{address:X5}";
    public string Label => label;
    public string Hex => $"{Value:X4}";
    public string Decimal => Value.ToString();
    public string Ascii => Value is >= 32 and <= 126 ? ((char)Value).ToString() : ".";

    public void Update(ushort newValue)
    {
        if (newValue == Value) return;
        Value = newValue;
        OnPropertyChanged(nameof(Hex));
        OnPropertyChanged(nameof(Decimal));
        OnPropertyChanged(nameof(Ascii));
    }
}

public sealed partial class SourceRowViewModel(
    ushort addressValue, string address, string location, string content, string sourceIdentifier, int sourceLine) : ObservableObject
{
    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isBreakpoint;
    public ushort AddressValue => addressValue;
    public string Address => address;
    public string Location => location;
    public string Content => content;
    public string SourceIdentifier => sourceIdentifier;
    public int SourceLine => sourceLine;
    public bool ShowBreakpointDot => IsBreakpoint && !IsCurrent;
    partial void OnIsCurrentChanged(bool value) => OnPropertyChanged(nameof(ShowBreakpointDot));
    partial void OnIsBreakpointChanged(bool value) => OnPropertyChanged(nameof(ShowBreakpointDot));
}

public sealed partial class SourceCodeLineViewModel(
    int lineNumber, string content, string sourceIdentifier, IReadOnlyList<ushort> addresses,
    IReadOnlyList<KeyValuePair<string, uint>> referencedSymbols) : ObservableObject
{
    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isBreakpoint;
    [ObservableProperty] private string? _symbolToolTip;

    public int LineNumberValue => lineNumber;
    public string LineNumber => lineNumber == 0 ? string.Empty : lineNumber.ToString();
    public string Content => content;
    public string SourceIdentifier => sourceIdentifier;
    public IReadOnlyList<ushort> Addresses => addresses;
    public IReadOnlyList<KeyValuePair<string, uint>> ReferencedSymbols => referencedSymbols;
    public IReadOnlyList<AssemblySyntaxToken> SyntaxTokens { get; } = AssemblySyntaxHighlighter.Tokenize(content);
    public bool HasCode => Addresses.Count > 0;
    public bool HasWatchTargets => ReferencedSymbols.Count > 0;
    public string BreakpointActionLabel => IsBreakpoint ? "Remove breakpoint" : "Add breakpoint";
    public string WatchActionLabel => HasWatchTargets
        ? $"Watch {string.Join(", ", ReferencedSymbols.Select(symbol => symbol.Key))}"
        : "No RAM/MMIO symbol on this line";
    public bool ShowBreakpointDot => IsBreakpoint && !IsCurrent;
    public bool CanShowBreakpointHint => !IsBreakpoint && !IsCurrent;

    partial void OnIsCurrentChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBreakpointDot));
        OnPropertyChanged(nameof(CanShowBreakpointHint));
    }
    partial void OnIsBreakpointChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBreakpointDot));
        OnPropertyChanged(nameof(CanShowBreakpointHint));
        OnPropertyChanged(nameof(BreakpointActionLabel));
    }
}

public sealed record AssemblySyntaxToken(string Text, string Color);

internal static class AssemblySyntaxHighlighter
{
    private const string Default = "#D7E0EE";
    private const string Instruction = "#39D5E8";
    private const string Symbol = "#67D9A8";
    private const string Literal = "#F3C969";
    private const string Directive = "#C792EA";
    private const string Comment = "#718096";

    private static readonly HashSet<string> Instructions = new(StringComparer.OrdinalIgnoreCase)
    {
        "NOR", "ADD", "STA", "JCC", "CLR", "LDA", "MOV", "JCS", "JZ", "JNZ", "JMP",
        "NOT", "AND", "NAND", "OR", "XOR", "LSL", "LSR", "ROL", "ROR", "INC", "DEC",
        "NEG", "SUB", "ADC", "SBC", "LDC", "CLC", "SEC", "PUSH", "POP", "LDS", "STS",
        "CALL", "RET", "RST", "HALT"
    };

    public static IReadOnlyList<AssemblySyntaxToken> Tokenize(string line)
    {
        var tokens = new List<AssemblySyntaxToken>();
        for (var index = 0; index < line.Length;)
        {
            if (line[index] == ';')
            {
                tokens.Add(new(line[index..], Comment));
                break;
            }

            if (line[index] is '\'' or '"')
            {
                var end = FindStringEnd(line, index, line[index]);
                tokens.Add(new(line[index..end], Symbol));
                index = end;
                continue;
            }

            if (char.IsWhiteSpace(line[index]))
            {
                var end = index + 1;
                while (end < line.Length && char.IsWhiteSpace(line[end])) end++;
                tokens.Add(new(line[index..end], Default));
                index = end;
                continue;
            }

            var start = index;
            if (line[index] == '#' && index + 1 < line.Length && char.IsLetter(line[index + 1]))
            {
                index += 2;
                while (index < line.Length && IsIdentifierCharacter(line[index])) index++;
                tokens.Add(new(line[start..index], Directive));
                continue;
            }

            if (char.IsDigit(line[index]) || line[index] == '#' && index + 1 < line.Length && char.IsDigit(line[index + 1]))
            {
                index++;
                while (index < line.Length && (char.IsLetterOrDigit(line[index]) || line[index] == '_')) index++;
                tokens.Add(new(line[start..index], Literal));
                continue;
            }

            if (char.IsLetter(line[index]) || line[index] is '_' or '.' or '$')
            {
                index++;
                while (index < line.Length && IsIdentifierCharacter(line[index])) index++;
                var word = line[start..index];
                var next = index;
                while (next < line.Length && char.IsWhiteSpace(line[next])) next++;
                var color = Instructions.Contains(word) ? Instruction
                    : next < line.Length && line[next] == ':' ? Symbol
                    : word == "$" ? Literal : Default;
                tokens.Add(new(word, color));
                continue;
            }

            tokens.Add(new(line[index].ToString(), line[index] is '#' or '@' ? Literal : Default));
            index++;
        }
        return tokens;
    }

    private static int FindStringEnd(string line, int start, char quote)
    {
        var escaped = false;
        for (var index = start + 1; index < line.Length; index++)
        {
            if (!escaped && line[index] == quote) return index + 1;
            escaped = !escaped && line[index] == '\\';
            if (line[index] != '\\') escaped = false;
        }
        return line.Length;
    }

    private static bool IsIdentifierCharacter(char value) => char.IsLetterOrDigit(value) || value is '_' or '.' or '$';
}
public sealed partial class SymbolRowViewModel(string name, uint address, string region, ushort value) : ObservableObject
{
    [ObservableProperty] private ushort _value = value;
    public string Name => name;
    public uint RawAddress => address;
    public string Address => $"{address:X5}";
    public string Region => region;
    public string DisplayValue => $"{Value:X4}";

    public void Update(ushort value)
    {
        if (Value == value) return;
        Value = value;
        OnPropertyChanged(nameof(DisplayValue));
    }
}
public sealed record BreakpointRowViewModel(
    ushort AddressValue, string Address, string Instruction, string Label, bool IsCurrentStop);
public sealed record SpeedOption(string Label, int Frequency)
{
    public override string ToString() => Label;
}

public sealed record LedIndicatorViewModel(int Index, bool IsOn, string State);
