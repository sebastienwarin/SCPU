using SCPU.Simulator.CLI.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SCPU.Simulator.CLI.Commands
{
    public sealed class ShellCommand(CommandApp app) : Command
    {
        private readonly CommandApp _app = app;

        protected override int Execute(CommandContext context, CancellationToken cancellationToken)
        {
            // Load history from file
            var histFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".scpu_history");
            if (File.Exists(histFile))
            {
                foreach (var line in File.ReadAllLines(histFile))
                {
                    ReadLine.AddHistory(line);
                }
            }

            // Attach completion
            ReadLine.AutoCompletionHandler = new CommandAndFileCompletion();

            // Shell
            AnsiConsole.MarkupLine("[bold]S-CPU Simulator CLI[/] — type [green]help[/] or [green]exit[/].");
            while (true)
            {
                AnsiConsole.Write(new Markup("scpu> "));

                // Read line
                string? line = ReadLine.Read(""); // uses history + ↑/↓
                if (line is null) continue;
                line = line.Trim();
                if (line.Length == 0) continue;
                if (line.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
                if (line.Equals("help", StringComparison.OrdinalIgnoreCase)) line = "--help";
                if (line.Equals("shell", StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine($"[red]Error[/]: shell already active.");
                    continue;
                }

                // Run command
                try
                {
                    _app.Run(SplitArgs(line), cancellationToken);
                    ReadLine.AddHistory(line); // persist in-memory
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error[/]: {Markup.Escape(ex.Message)}");
                }

                // Save history
                try
                {
                    File.WriteAllLines(histFile, ReadLine.GetHistory());
                } catch { }
            }

            return 0;
        }

        private static string[] SplitArgs(string commandLine)
        {
            var args = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool inQuotes = false;
            foreach (char c in commandLine)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (cur.Length > 0) { args.Add(cur.ToString()); cur.Clear(); }
                }
                else cur.Append(c);
            }
            if (cur.Length > 0) args.Add(cur.ToString());
            return [.. args];
        }
    }
}
