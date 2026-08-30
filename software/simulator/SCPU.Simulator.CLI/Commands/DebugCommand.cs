using System.ComponentModel;
using System.Globalization;
using SCPU.Architecture;
using SCPU.Simulator.CLI.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace SCPU.Simulator.CLI.Commands;

/// <summary>Runs the keyboard-driven terminal debugger.</summary>
public sealed class DebugCommand(
    DebugSession session,
    SimulationRunner runner,
    BufferedTerminalDevice terminal,
    LedPanelDevice ledPanel,
    InteractiveConsoleState consoleState) : AsyncCommand<DebugCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--refresh <HZ>")]
        [DefaultValue(15)]
        public int RefreshRate { get; set; } = 15;

        [CommandOption("-f|--frequency <FREQUENCY>")]
        [DefaultValue("2MHz")]
        [Description("Simulation frequency from 5Hz to 4MHz, or 'max'.")]
        public string Frequency { get; set; } = "2MHz";

        public override ValidationResult Validate()
        {
            if (RefreshRate is < 1 or > 30) return ValidationResult.Error("--refresh must be between 1 and 30 Hz.");
            return TryParseFrequency(Frequency, out _)
                ? ValidationResult.Success()
                : ValidationResult.Error("--frequency must be between 5Hz and 4MHz, or 'max'.");
        }
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (session.Program is null) throw new InvalidOperationException("Load a program before starting the debugger.");
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            throw new InvalidOperationException("The interactive debugger requires a terminal.");
        if (SafeWindowHeight() < 28)
            throw new InvalidOperationException("The interactive debugger requires a terminal at least 28 rows high.");
        if (SafeWindowWidth() < 90)
            throw new InvalidOperationException("The interactive debugger requires a terminal at least 90 columns wide.");

        var refreshRate = settings.RefreshRate;
        TryParseFrequency(settings.Frequency, out var targetFrequency);
        settings.RefreshRate = 15;
        settings.Frequency = "2MHz";
        runner.RefreshFrequency = refreshRate;
        runner.TargetFrequency = targetFrequency;
        consoleState.IsActive = true;

        try
        {
            // Spectre owns both the alternate screen and its cursor lifecycle.
            AnsiConsole.AlternateScreen(() =>
                RunInteractiveAsync(refreshRate, cancellationToken).GetAwaiter().GetResult());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            await runner.PauseAsync();
            consoleState.IsActive = false;
        }
        return cancellationToken.IsCancellationRequested ? 130 : 0;
    }

    private async Task RunInteractiveAsync(int refreshRate, CancellationToken cancellationToken)
    {
        var exit = false;
        var width = SafeWindowWidth();
        var height = SafeWindowHeight();
        
        // Hide cursor for cleaner TUI experience
        Console.Out.Write("\u001b[?25l");
        Console.Out.Flush();
        
        try
        {
            DrawFrame(clear: true);

            while (!exit && !cancellationToken.IsCancellationRequested)
            {
                var redraw = false;
                while (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    var result = await HandleKeyAsync(key, cancellationToken);
                    exit = result == DebugInputResult.Exit;
                    redraw |= result == DebugInputResult.ClearDisplay;
                    if (exit) break;
                }

                var resized = width != SafeWindowWidth() || height != SafeWindowHeight();
                if (resized)
                {
                    width = SafeWindowWidth();
                    height = SafeWindowHeight();
                }

                // Always draw, don't rely on fingerprint which can have race conditions
                // The DrawFrame implementation is efficient enough to run every frame
                if (!exit)
                    DrawFrame(clear: resized);

                if (!exit)
                    await Task.Delay(TimeSpan.FromSeconds(1d / refreshRate), cancellationToken);
            }
        }
        finally
        {
            // Show cursor again when exiting
            Console.Out.Write("\u001b[?25h");
            Console.Out.Flush();
        }
    }

    private void DrawFrame(bool clear = false)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var renderer = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(writer)
        });
        // Leave the terminal's last column untouched: writing into it enables
        // automatic wrapping on Windows Terminal and shifts the whole frame.
        renderer.Profile.Width = SafeRenderWidth();
        renderer.Write(BuildView());

        var frame = writer.ToString().TrimEnd('\r', '\n');
        // Overwrite in place to avoid the visible flash caused by clearing
        // before every frame. The suffix removes anything left by a taller frame.
        var prefix = clear ? "\u001b[H\u001b[2J" : "\u001b[H";
        Console.Out.Write($"{prefix}{frame}\u001b[J");
        Console.Out.Flush();    
    }

    private async Task<DebugInputResult> HandleKeyAsync(ConsoleKeyInfo key, CancellationToken cancellationToken)
    {
        switch (key.Key)
        {
            case ConsoleKey.F5 when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                await runner.PauseAsync();
                await session.ReloadAsync(cancellationToken);
                return DebugInputResult.ClearDisplay;
            case ConsoleKey.F5:
            case ConsoleKey.Spacebar:
                if (runner.IsRunning) await runner.PauseAsync();
                else await runner.RunAsync(cancellationToken);
                break;
            case ConsoleKey.F8:
                await runner.StepCycleAsync(cancellationToken);
                break;
            case ConsoleKey.F9:
                await runner.StepInstructionAsync(cancellationToken);
                break;
            case ConsoleKey.F10:
                await runner.StepSourceAsync(cancellationToken);
                break;
            case ConsoleKey.B:
                await runner.PauseAsync();
                var address = session.Cpu.ProgramCounter;
                if (!session.Breakpoints.Add(address)) session.Breakpoints.Remove(address);
                break;
            case ConsoleKey.R:
                await runner.ResetAsync(cancellationToken);
                return DebugInputResult.ClearDisplay;
            case ConsoleKey.OemPlus:
            case ConsoleKey.Add:
                AdjustFrequency(1);
                break;
            case ConsoleKey.OemMinus:
            case ConsoleKey.Subtract:
                AdjustFrequency(-1);
                break;
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                return DebugInputResult.Exit;
        }
        return DebugInputResult.Continue;
    }

    private IRenderable BuildView()
    {
        var snapshot = session.Snapshot();
        var height = SafeWindowHeight();
        var watchRows = height >= 42 ? 6 : height >= 34 ? 4 : 1;
        // Borders, CPU, devices and the key bar consume 24 rows in the
        // narrowest supported layout. Never let Spectre scroll the live area.
        var codeRows = Math.Clamp(height - 24 - watchRows, 3, 11);
        var code = BuildDisassembly(snapshot.ProgramCounter, codeRows);
        var status = BuildStatus(snapshot);
        var watches = BuildWatches(watchRows);
        var stack = BuildStack(watchRows);
        var peripherals = new Markup(
            $"[yellow]LED[/] 0x{ledPanel.Leds:X4}   [cyan]TTY[/] {Markup.Escape(LastLine(terminal.Output))}");
        var footer = new Markup(
            "[bold]F5[/] Run/Pause  [bold]F8[/] Cycle  [bold]F9[/] Instruction  [bold]F10[/] Source  " +
            "[bold]B[/] Breakpoint  [bold]-/+[/] Speed  [bold]R[/] Reset  [bold]Ctrl+F5[/] Reload  [bold]Q[/] Quit");

        var availableWidth = SafeRenderWidth();
        var watchWidth = availableWidth * 3 / 5;
        var stackWidth = availableWidth - watchWidth;
        var watchAndStack = new Grid()
            .AddColumn(new GridColumn { Width = watchWidth })
            .AddColumn(new GridColumn { Width = stackWidth })
            .AddRow(
                new Panel(watches).Header("Watches").Expand(),
                new Panel(stack).Header("Stack").Expand());

        return new Rows(
            new Panel(code).Header("Code").Expand(),
            new Panel(status).Header("CPU").Expand(),
            watchAndStack,
            new Panel(peripherals).Header("Devices").Expand(),
            new Panel(footer).Expand());
    }

    private IRenderable BuildDisassembly(ushort pc, int rowCount)
    {
        var start = Math.Max(0, pc - rowCount / 2);
        var contentWidth = SafeRenderWidth() - 4; // Panel border and horizontal padding.
        const int markerWidth = 1;
        const int addressWidth = 6;
        const int wordWidth = 6;
        const int instructionWidth = 14;
        const int labelWidth = 16;
        const int separatorsWidth = 6;
        var locationWidth = Math.Clamp(contentWidth / 4, 18, 38);
        var sourceWidth = contentWidth - markerWidth - addressWidth - wordWidth -
            instructionWidth - labelWidth - locationWidth - separatorsWidth;
        var rows = new List<IRenderable>(rowCount);
        SourceLocation? previousSource = null;

        for (var address = start; address < Math.Min(session.Cpu.ROM.Length, start + rowCount); address++)
        {
            var entry = session.Program!.Rom.Count > address ? session.Program.Rom[address] : null;
            var marker = address == pc ? "[yellow]▶[/]" : session.Breakpoints.Contains((ushort)address) ? "[red]●[/]" : " ";
            var label = entry?.Label ?? "";
            var entrySource = entry?.Source;
            var isContinuation = entrySource is not null && previousSource is not null &&
                entrySource.Line == previousSource.Line &&
                string.Equals(entrySource.Identifier, previousSource.Identifier, StringComparison.OrdinalIgnoreCase);
            var source = isContinuation ? "" : SourceText(entry);
            var location = isContinuation ? "" : SourceLocation(entry);
            previousSource = entrySource;

            rows.Add(new Markup(string.Join(" ",
                marker,
                StyledCell($"0x{address:X4}", addressWidth, "cyan"),
                StyledCell($"0x{session.Cpu.ROM[address]:X4}", wordWidth, "blue"),
                FormatInstruction(session.Cpu.ROM[address], instructionWidth, entry?.IsData == true),
                StyledCell(label, labelWidth, "cyan"),
                FormatSource(source, sourceWidth),
                StyledCell(location, locationWidth, "grey"))));
        }
        while (rows.Count < rowCount) rows.Add(new Text(new string(' ', contentWidth)));
        return new Rows(rows.ToArray());
    }

    private Table BuildStatus(CpuSnapshot snapshot)
    {
        var table = new Table().NoBorder().HideHeaders().AddColumns("", "");
        table.AddRow("State", $"[cyan]{snapshot.State}[/]");
        table.AddRow("PC", $"0x{snapshot.ProgramCounter:X4}");
        table.AddRow("ACC", $"0x{snapshot.Accumulator:X4}");
        table.AddRow("IR", $"0x{snapshot.InstructionRegister:X4}");
        table.AddRow("Flags", $"C={(snapshot.Carry ? 1 : 0)} IND={(snapshot.Indirected ? 1 : 0)}");
        table.AddRow("Step", snapshot.Step.ToString());
        table.AddRow("Bus", $"0x{snapshot.DataBus:X4}");
        table.AddRow("Cycles", snapshot.Cycles.ToString());
        table.AddRow("Instructions", snapshot.Instructions.ToString());
        var measured = runner.ActualFrequency > 0 ? $" / {FormatFrequency(runner.ActualFrequency)} actual" : "";
        table.AddRow("Frequency", $"[cyan]{FormatFrequency(runner.TargetFrequency)}[/]{measured}");
        table.AddRow("Stop", snapshot.StopReason?.ToString() ?? "");
        return table;
    }

    private Table BuildWatches(int rowCount)
    {
        var table = new Table().NoBorder().HideHeaders().AddColumns("", "", "");
        foreach (var watch in session.Watches.Take(rowCount))
            table.AddRow(Markup.Escape(watch.Expression), $"[cyan]0x{watch.Address:X5}[/]", $"[yellow]0x{session.ReadMemory(watch.Address):X4}[/]");
        if (session.Watches.Count == 0) table.AddRow("[grey]No watches[/]", "", "");
        PadRows(table, rowCount, 3);
        return table;
    }

    private Table BuildStack(int rowCount)
    {
        var table = new Table().NoBorder().HideHeaders().AddColumns("", "");
        foreach (var entry in session.GetStack(rowCount).Entries)
            table.AddRow($"{(entry.IsFramePointer ? "[cyan]FP[/] " : "")}[cyan]0x{entry.Address:X5}[/]", $"[yellow]0x{entry.Value:X4}[/]");
        if (table.Rows.Count == 0) table.AddRow("[grey]Empty[/]", "");
        PadRows(table, rowCount, 2);
        return table;
    }

    private void AdjustFrequency(int direction)
    {
        int[] frequencies = [5, 10, 50, 100, 500, 1_000, 10_000, 100_000, 500_000, 1_000_000, 2_000_000, 4_000_000, 0];
        var index = Array.IndexOf(frequencies, runner.TargetFrequency);
        if (index < 0) index = Array.FindIndex(frequencies, value => value == 0 || value >= runner.TargetFrequency);
        runner.TargetFrequency = frequencies[Math.Clamp(index + direction, 0, frequencies.Length - 1)];
    }

    internal static bool TryParseFrequency(string text, out int frequency)
    {
        frequency = 0;
        var value = text.Trim().Replace(" ", "").Replace("_", "");
        if (value.Equals("max", StringComparison.OrdinalIgnoreCase) || value == "0") return true;
        var multiplier = 1d;
        if (value.EndsWith("mhz", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_000_000; value = value[..^3]; }
        else if (value.EndsWith("khz", StringComparison.OrdinalIgnoreCase)) { multiplier = 1_000; value = value[..^3]; }
        else if (value.EndsWith("hz", StringComparison.OrdinalIgnoreCase)) value = value[..^2];
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return false;
        var result = parsed * multiplier;
        if (result is < 5 or > 4_000_000 || result != Math.Truncate(result)) return false;
        frequency = (int)result;
        return true;
    }

    private static string FormatFrequency(double frequency) => frequency <= 0 ? "Max" : frequency switch
    {
        >= 1_000_000 => $"{frequency / 1_000_000:0.##} MHz",
        >= 1_000 => $"{frequency / 1_000:0.##} kHz",
        _ => $"{frequency:0} Hz"
    };

    private static int SafeWindowHeight()
    {
        try { return Console.WindowHeight; }
        catch { return 40; }
    }

    private static int SafeWindowWidth()
    {
        try { return Console.WindowWidth; }
        catch { return 120; }
    }

    private static int SafeRenderWidth() => Math.Max(40, SafeWindowWidth() - 2);

    private static string FormatInstruction(ushort word, int width, bool isData = false)
    {
        if (isData)
            return StyledCell(InstructionFormatter.FormatDataInstruction(word), width, "grey");
        var text = FitCell(InstructionFormatter.FormatWithVirtualAddresses(word), width);
        var separator = text.IndexOf(' ');
        return separator < 0
            ? $"[green]{Markup.Escape(text)}[/]"
            : $"[green]{Markup.Escape(text[..separator])}[/] [yellow]{Markup.Escape(text[(separator + 1)..])}[/]";
    }

    private static string SourceText(RomEntry? entry)
    {
        if (entry?.Source is null) return "";
        var text = entry.Source.Content.Replace("\r", "").Replace("\n", " ").Replace('\t', ' ').Trim();
        var primaryLabel = entry.Label?.Split(',')[0].Trim();
        if (!string.IsNullOrEmpty(primaryLabel) && text.StartsWith($"{primaryLabel}:", StringComparison.Ordinal))
            text = text[(primaryLabel.Length + 1)..].TrimStart();
        return text;
    }

    private static string SourceLocation(RomEntry? entry)
    {
        if (entry?.Source is null) return "";
        var fileName = Path.GetFileName(entry.Source.Identifier);
        var parts = fileName.Split('.');
        if (parts.Length > 2) fileName = string.Join('.', parts[^2..]);
        return $"{fileName}:{entry.Source.Line}";
    }

    private static string FormatSource(string text, int width)
    {
        text = FitCell(text, width);
        var comment = text.IndexOf(';');
        return comment < 0
            ? Markup.Escape(text)
            : $"{Markup.Escape(text[..comment])}[grey]{Markup.Escape(text[comment..])}[/]";
    }

    private static string StyledCell(string text, int width, string style) =>
        $"[{style}]{Markup.Escape(FitCell(text, width))}[/]";

    private static string FitCell(string text, int width)
    {
        if (text.Length > width)
            text = width == 1 ? "…" : $"{text[..(width - 1)]}…";
        return text.PadRight(width);
    }

    private static void PadRows(Table table, int count, int columns)
    {
        while (table.Rows.Count < count) table.AddRow(Enumerable.Repeat("", columns).ToArray());
    }

    private static string LastLine(string value)
    {
        var line = value.Replace("\r", "").Split('\n').LastOrDefault(item => item.Length > 0) ?? "";
        return line.Length <= 60 ? line : $"…{line[^59..]}";
    }

    private enum DebugInputResult
    {
        Continue,
        ClearDisplay,
        Exit
    }
}
