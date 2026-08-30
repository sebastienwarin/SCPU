using SCPU.Architecture;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

internal static class Assertion
{
    public static int Numeric(string subject, uint actual, string op, uint expected)
    {
        var success = op switch
        {
            "=" or "==" or "eq" => actual == expected,
            "!=" or "ne" => actual != expected,
            "<" => actual < expected,
            "<=" => actual <= expected,
            ">" => actual > expected,
            ">=" => actual >= expected,
            _ => throw new ArgumentException($"Unknown assertion operator '{op}'.")
        };
        AnsiConsole.MarkupLine(success
            ? $"[green]PASS[/] {Markup.Escape(subject)} = 0x{actual:X} ({actual})"
            : $"[red]FAIL[/] {Markup.Escape(subject)}: actual 0x{actual:X} ({actual}), expected {Markup.Escape(op)} 0x{expected:X} ({expected})");
        return success ? 0 : 1;
    }

    public static uint Value(DebugSession session, string text) => session.ResolveAddress(text);
}

public sealed class AssertRegisterCommand(DebugSession session) : Command<AssertRegisterCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<REGISTER>")] public string Register { get; init; } = default!;
        [CommandArgument(1, "<OPERATOR>")] public string Operator { get; init; } = default!;
        [CommandArgument(2, "<VALUE>")] public string Value { get; init; } = default!;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var name = settings.Register.ToLowerInvariant();
        uint actual = name switch
        {
            "a" or "acc" => session.Cpu.AccumulatorRegister,
            "ir" => session.Cpu.InstructionRegister,
            "pc" => session.Cpu.ProgramCounter,
            "sp" => session.Cpu.LookupValue(ReservedAddresses.StackPointer),
            "fp" => session.Cpu.LookupValue(ReservedAddresses.FramePointer),
            "c" or "carry" => session.Cpu.CarryFlag ? 1u : 0u,
            "ind" or "indirected" => session.Cpu.IndirectedFlag ? 1u : 0u,
            "step" => (uint)session.Cpu.StepCounter,
            "ticks" or "cycles" => checked((uint)session.CycleCount),
            "instructions" => checked((uint)session.InstructionCount),
            _ => throw new ArgumentException($"Unknown register or counter '{settings.Register}'.")
        };
        return Assertion.Numeric(settings.Register, actual, settings.Operator, Assertion.Value(session, settings.Value));
    }
}

public sealed class AssertMemoryCommand(DebugSession session) : Command<AssertMemoryCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<ADDRESS>")] public string Address { get; init; } = default!;
        [CommandArgument(1, "<OPERATOR>")] public string Operator { get; init; } = default!;
        [CommandArgument(2, "<VALUE>")] public string Value { get; init; } = default!;
    }
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var address = session.ResolveAddress(settings.Address);
        return Assertion.Numeric($"mem[{settings.Address}]", session.ReadMemory(address), settings.Operator,
            Assertion.Value(session, settings.Value));
    }
}

public sealed class AssertPcCommand(DebugSession session) : Command<AssertPcCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<OPERATOR>")] public string Operator { get; init; } = default!;
        [CommandArgument(1, "<VALUE>")] public string Value { get; init; } = default!;
    }
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        Assertion.Numeric("PC", session.Cpu.ProgramCounter, settings.Operator, Assertion.Value(session, settings.Value));
}

public sealed class AssertLedCommand(LedPanelDevice led, DebugSession session) : Command<AssertLedCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<OPERATOR>")] public string Operator { get; init; } = default!;
        [CommandArgument(1, "<VALUE>")] public string Value { get; init; } = default!;
    }
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        Assertion.Numeric("LED", led.Leds, settings.Operator, Assertion.Value(session, settings.Value));
}

public sealed class AssertTtyCommand(BufferedTerminalDevice terminal) : Command<AssertTtyCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<OPERATOR>")] public string Operator { get; init; } = default!;
        [CommandArgument(1, "<TEXT>")] public string Text { get; init; } = default!;
    }
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var output = terminal.Output;
        var success = settings.Operator.ToLowerInvariant() switch
        {
            "=" or "==" or "eq" => output == settings.Text,
            "!=" or "ne" => output != settings.Text,
            "contains" => output.Contains(settings.Text, StringComparison.Ordinal),
            "not-contains" => !output.Contains(settings.Text, StringComparison.Ordinal),
            _ => throw new ArgumentException($"Unknown TTY assertion operator '{settings.Operator}'.")
        };
        AnsiConsole.MarkupLine(success
            ? $"[green]PASS[/] TTY {Markup.Escape(settings.Operator)} \"{Markup.Escape(settings.Text)}\""
            : $"[red]FAIL[/] TTY output is \"{Markup.Escape(output)}\"");
        return success ? 0 : 1;
    }
}
