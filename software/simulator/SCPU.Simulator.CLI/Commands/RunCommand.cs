using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public sealed class RunCommand(DebugSession session) : Command<RunCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-u|--until <ADDRESS>")]
        [Description("Stop at an address or symbol.")]
        public string? Until { get; set; }

        [CommandOption("-m|--max-ticks <COUNT>")]
        [Description("Safety limit for this run (default: 10,000,000).")]
        public long? MaxTicks { get; set; }

        public override ValidationResult Validate() => MaxTicks is <= 0
            ? ValidationResult.Error("--max-ticks must be greater than zero.")
            : ValidationResult.Success();
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        uint? target = settings.Until is null ? null : session.ResolveAddress(settings.Until);
        long? maxTicks = settings.MaxTicks;
        settings.Until = null;
        settings.MaxTicks = null;
        var result = session.Run(target, maxTicks, cancellationToken);
        RenderResult(result);
        RegsCommand.Render(session);
        return result.Reason == StopReason.Cancelled ? 130 : 0;
    }

    internal static void RenderResult(ExecutionResult result)
    {
        var (color, message) = result.Reason switch
        {
            StopReason.Halt => ("green", "HALT reached"),
            StopReason.Breakpoint => ("yellow", "breakpoint hit"),
            StopReason.Address => ("cyan", "target reached"),
            StopReason.TickLimit => ("yellow", "tick limit reached"),
            StopReason.Cancelled => ("red", "execution cancelled"),
            _ => ("grey", "step completed")
        };
        AnsiConsole.MarkupLine($"[{color}]{message}[/] at 0x{result.ProgramCounter:X4} ({result.Ticks} tick(s)).");
    }
}
