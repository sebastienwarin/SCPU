using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands;

public sealed class StepCommand(DebugSession session) : Command<StepCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[COUNT]")]
        [DefaultValue(1)]
        public int Count { get; set; } = 1;

        [CommandOption("--ticks")]
        [Description("Advance micro-steps instead of complete instructions.")]
        public bool Ticks { get; set; }

        [CommandOption("--source")]
        [Description("Advance to the next mapped assembly source line.")]
        public bool Source { get; set; }

        public override ValidationResult Validate()
        {
            if (Count <= 0) return ValidationResult.Error("COUNT must be greater than zero.");
            if (Ticks && Source) return ValidationResult.Error("--ticks and --source are mutually exclusive.");
            return ValidationResult.Success();
        }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        int count = settings.Count;
        bool ticks = settings.Ticks;
        bool source = settings.Source;
        settings.Count = 1;
        settings.Ticks = false;
        settings.Source = false;
        var result = source
            ? session.StepSourceLines(count, cancellationToken)
            : ticks ? session.Tick(count, cancellationToken) : session.StepInstructions(count, cancellationToken);
        RunCommand.RenderResult(result);
        RegsCommand.Render(session);
        return result.Reason == StopReason.Cancelled ? 130 : 0;
    }
}
