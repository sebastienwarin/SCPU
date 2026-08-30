namespace SCPU.Simulator.Desktop;

public sealed record LaunchOptions(string? FilePath, int? Frequency, bool? FollowProgramCounter)
{
    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        string? filePath = null;
        int? frequency = null;
        bool? followPc = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--file":
                    filePath = RequireValue(args, ref index, argument);
                    break;
                case "--frequency":
                    frequency = ParseFrequency(RequireValue(args, ref index, argument));
                    break;
                case "--follow-pc":
                    followPc = true;
                    break;
                case "--no-follow-pc":
                    followPc = false;
                    break;
                default:
                    if (argument.StartsWith('-')) throw new ArgumentException($"Unknown option '{argument}'.");
                    if (filePath is not null) throw new ArgumentException("Only one program file can be specified.");
                    filePath = argument;
                    break;
            }
        }

        return new LaunchOptions(filePath is null ? null : Path.GetFullPath(filePath), frequency, followPc);
    }

    private static string RequireValue(IReadOnlyList<string> args, ref int index, string option) =>
        ++index < args.Count ? args[index] : throw new ArgumentException($"Option '{option}' requires a value.");

    private static int ParseFrequency(string value)
    {
        if (value.Equals("max", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("maximum", StringComparison.OrdinalIgnoreCase)) return 0;
        if (int.TryParse(value, out var frequency) && frequency > 0) return frequency;
        throw new ArgumentException($"Invalid frequency '{value}'. Use a positive value in Hz or 'max'.");
    }
}
