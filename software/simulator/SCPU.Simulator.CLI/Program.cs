using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SCPU.Simulator.CLI.Commands;
using SCPU.Simulator.CLI.Infrastructure;
using SCPU.Simulator.Core;
using Spectre.Console.Cli;
using Spectre.Console;

namespace SCPU.Simulator.CLI
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            ConfigureConsoleEncoding();

            var services = new ServiceCollection();
            var interactiveConsole = new InteractiveConsoleState();
            services.AddSingleton(interactiveConsole);

            // S-CPU libs
            services.AddSingleton(provider =>
            {
                var device = new LedPanelDevice();
                var state = provider.GetRequiredService<InteractiveConsoleState>();
                device.RegisterChanged += (_, change) =>
                {
                    if (state.IsActive) return;
                    AnsiConsole.MarkupLine(change.Address == 2
                        ? $"[yellow]LED[/] = 0x{change.Value:X4}"
                        : $"[blue]DISPLAY{change.Address}[/] = 0x{change.Value:X4}");
                };
                return device;
            });
            services.AddSingleton(provider =>
            {
                var device = new BufferedTerminalDevice();
                var state = provider.GetRequiredService<InteractiveConsoleState>();
                device.OutputProduced += (_, character) => { if (!state.IsActive) Console.Out.Write(character); };
                return device;
            });
            services.AddSCPUDebugger(provider =>
            {
                var cpu = new Processor();
                cpu.Devices.Add(DeviceId.Device0, provider.GetRequiredService<LedPanelDevice>());
                cpu.Devices.Add(DeviceId.Device1, provider.GetRequiredService<BufferedTerminalDevice>());
                return cpu;
            });

            // Logging
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddProvider(new AnsiConsoleLoggerProvider(interactiveConsole));
                b.SetMinimumLevel(LogLevel.Information);
            });

            // Create Spectre app
            var registrar = new TypeRegistrar(services);
            var app = new CommandApp(registrar);
            services.AddSingleton(_ => app);

            // Configure commands
            app.Configure(cfg =>
            {
                cfg.SetApplicationName("scpu");
                cfg.SetApplicationVersion(BuildInfo.Version);
                cfg.ValidateExamples();

                // Interactive shell (REPL-style)
                cfg.AddCommand<ShellCommand>("shell")
                    .WithDescription("Interactive shell.");

                // Top-level commands
                cfg.AddCommand<LoadCommand>("load")
                   .WithDescription("Load a ROM or compile/assemble a source file (S-Code/ASM).")
                   .WithExample(["load", "rom.bin"])
                   .WithExample(["load", "program.asm"])
                   .WithExample(["load", @".\samples\scode\HelloWorld.scode"]);
                cfg.AddCommand<ReloadCommand>("reload")
                   .WithDescription("Reload last loaded image.");

                cfg.AddCommand<RunCommand>("run")
                   .WithDescription("Run until HALT, breakpoint, address or tick limit.")
                   .WithExample(["run"])
                   .WithExample(["run", "--until", "0x4FA"]);
                cfg.AddCommand<ResetCommand>("reset")
                   .WithDescription("Reset CPU (keep ROM).");
                cfg.AddCommand<StepCommand>("step")
                   .WithDescription("Execute one or more instructions (or hardware ticks).")
                   .WithExample(["step"])
                   .WithExample(["step", "10"])
                   .WithExample(["step", "--ticks", "1"]);

                cfg.AddCommand<MemCommand>("mem")
                    .WithDescription("Read/write memory.")
                    .WithExample(["mem", "0x12100"]);

                cfg.AddCommand<RegsCommand>("regs").WithDescription("Show registers and flags.");
                cfg.AddCommand<StackCommand>("stack").WithDescription("Dump stack.");
                cfg.AddCommand<DisasmCommand>("disasm").WithDescription("Disassemble ROM around an address or symbol.");
                cfg.AddCommand<SourceCommand>("source").WithDescription("Show source around the current PC, a symbol or file:line.");
                cfg.AddCommand<ContextCommand>("context").WithAlias("ctx").WithDescription("Show the current debugging context.");
                cfg.AddCommand<DebugCommand>("debug").WithDescription("Open the interactive live debugger.");
                cfg.AddCommand<SymbolsCommand>("symbols").WithAlias("symbol")
                   .WithDescription("List labels, constants and their addresses.");
                cfg.AddBranch("break", br =>
                {
                    br.SetDescription("Breakpoint management.");
                    br.AddCommand<BreakAddCommand>("add");
                    br.AddCommand<BreakDeleteCommand>("delete").WithAlias("del");
                    br.AddCommand<BreakListCommand>("list");
                    br.AddCommand<BreakClearCommand>("clear");
                });
                cfg.AddBranch("watch", watch =>
                {
                    watch.SetDescription("Persistent memory value watches.");
                    watch.AddCommand<WatchAddCommand>("add")
                        .WithDescription("Add one or more addresses, a comma list, or an inclusive address range.")
                        .WithExample(["watch", "add", "counter", "stack_top"])
                        .WithExample(["watch", "add", "0x12100..0x1210A"])
                        .WithExample(["watch", "add", "0x12100", "--to", "0x1210A"]);
                    watch.AddCommand<WatchDeleteCommand>("delete").WithAlias("del");
                    watch.AddCommand<WatchListCommand>("list");
                    watch.AddCommand<WatchClearCommand>("clear");
                });
                cfg.AddBranch("assert", assertion =>
                {
                    assertion.SetDescription("Verify program state; returns exit code 1 on failure.");
                    assertion.AddCommand<AssertRegisterCommand>("reg");
                    assertion.AddCommand<AssertMemoryCommand>("mem");
                    assertion.AddCommand<AssertPcCommand>("pc");
                    assertion.AddCommand<AssertLedCommand>("led");
                    assertion.AddCommand<AssertTtyCommand>("tty");
                });
                cfg.AddBranch("tty", tty =>
                {
                    tty.SetDescription("Terminal input/output buffers (MMIO device #1).");
                    tty.AddCommand<TerminalInputCommand>("input");
                    tty.AddCommand<TerminalStatusCommand>("status");
                    tty.AddCommand<TerminalClearCommand>("clear");
                });
            });

            // Run shell command by default
            if (args.Length == 0)
            {
                return app.Run(["shell"]);
            }

            var segments = SplitCommands(args);
            if (segments.Count == 0) return app.Run(["--help"]);

            foreach (var seg in segments)
            {
                var code = app.Run(seg);
                if (code != 0)
                {
                    return code;
                }
            }
            return 0;
        }

        private static void ConfigureConsoleEncoding()
        {
            try
            {
                Console.InputEncoding = new UTF8Encoding(false);
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch
            {
                // Some consoles (especially legacy Windows terminals) may reject UTF-8.
                // The debugger will fall back to ASCII markers in that case.
            }
        }

        private static List<string[]> SplitCommands(IEnumerable<string> args)
        {
            var segments = new List<string[]>();
            var current = new List<string>();
            foreach (string argument in args)
            {
                if (argument == "--")
                {
                    if (current.Count > 0) segments.Add([.. current]);
                    current.Clear();
                }
                else
                {
                    current.Add(argument);
                }
            }
            if (current.Count > 0) segments.Add([.. current]);
            return segments;
        }
    }
}
