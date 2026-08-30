using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SCPU.Architecture;
using SCPU.Assembler;
using SCPU.Assembler.Model;
using SCPU.Simulator.Core;

namespace SCode.Compiler.Tests.Support
{
    /// <summary>
    /// End-to-end executor: S-Code -> compile -> assemble -> load -> simulate until HALT.
    /// Ensures MMIO 0x2800 == 0 (no failed S-Code asserts). Returns the Processor.
    /// </summary>
    public static class SCodeRunner
    {
        // Internal helpers injected into each test program:
        private static readonly string InternalTestMethod = @"
            void assert(bool value) {
              if(!value) {
                asm(""
                    LDA #(IODEV)
                    STA R0
                    MOV @(R0), #1
                  "");
                exit();
              }
            }
            void exit() {
              asm(""HALT"");
            }
            ";

        public static async Task<Processor> ExecuteCodeAsync(string src, int maxCycles = 2_000_000)
        {
            // Build services to get S-Code compiler & S-CPU assembler
            var (compiler, assembler) = BuildServices();

            // Append internal test method
            src += InternalTestMethod;

            // Compile & Assemble
            var compilation = await compiler.CompileAsync(new CompileRequest { Source = SourceDocument.FromInline(src) });
            var asm = await assembler.AssembleAsync(new AssemblyRequest { Source = compilation.GeneratedAssembly });

            // Load into S-CPU processor
            var cpu = new Processor();
            cpu.Devices.Add(DeviceId.Device0, new TestDevice());
            cpu.Load(asm.Binary);

            // Run code until halt addresses or reach the max cycle count
            int cycles = 0;
            var haltAddresses = InstructionUtils.DetectHaltAddresses(cpu.ROM);
            // Stop only at a real instruction boundary. An indirect instruction briefly
            // returns to S0 after address resolution, while its execution is still pending.
            while ((!cpu.ShouldFetchIR || !haltAddresses.Contains(cpu.ProgramCounter)) && cycles++ < maxCycles)
            {
                cpu.Tick();
            }

            // Check max cycle
            Assert.True(cycles < maxCycles, $"Program didn't halt within maxCycles.\n" + FormatCpuSnapshot(cpu, cycles));

            // Check no S-Code assert triggered (MMIO[0x12800] must remain 0)
            var mmio = cpu.LookupValue(MemoryMap.Mmio.Start);
            Assert.True(mmio == 0,
                $"S-Code assertion failed: MMIO[0x12800]={mmio} (expected 0).\n" + FormatCpuSnapshot(cpu, cycles));

            // Return processor
            return cpu;
        }

        /// <summary>Compiles the source and returns the generated assembly text (no simulation).</summary>
        public static async Task<string> CompileToAssemblyAsync(string src)
        {
            var (compiler, _) = BuildServices();
            var compilation = await compiler.CompileAsync(new CompileRequest { Source = SourceDocument.FromInline(src) });
            return await compilation.GeneratedAssembly.ReadAllTextAsync();
        }

        private static (Compiler, Assembler) BuildServices()
        {
            var services = new ServiceCollection();
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddXUnit();
            });
            services.AddCompiler();
            services.AddAssembler();
            var provider = services.BuildServiceProvider();
            return (provider.GetRequiredService<Compiler>(), provider.GetRequiredService<Assembler>());
        }

        private static string FormatCpuSnapshot(Processor cpu, int cyclesExecuted, int romContext = 4, int userDumpWords = 8)
        {
            int pc = cpu.ProgramCounter;
            int romLen = cpu.ROM.Length;

            int romStart = Math.Max(0, pc - romContext);
            int romEnd = Math.Min(romLen - 1, pc + romContext);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== CPU Snapshot ===");
            sb.AppendLine($"Cycles       : {cyclesExecuted}");
            sb.AppendLine($"PC           : 0x{pc:X4}");
            sb.AppendLine($"Step         : {cpu.StepCounter}");
            sb.AppendLine($"ACC          : 0x{cpu.AccumulatorRegister:X4}");
            sb.AppendLine($"SP           : 0x{cpu.LookupValue(ReservedAddresses.StackPointer):X4}");
            sb.AppendLine($"FP           : 0x{cpu.LookupValue(ReservedAddresses.FramePointer):X4}");
            sb.AppendLine($"MMIO[0x12800]: 0x{cpu.LookupValue(MemoryMap.Mmio.Start):X4}");
            sb.AppendLine($"ROM window   : [0x{romStart:X4} .. 0x{romEnd:X4}] (centered on PC)");
            for (int addr = romStart; addr <= romEnd; addr++)
            {
                string mark = addr == pc ? ">>" : "  ";
                sb.AppendLine($"{mark} 0x{addr:X4}: 0x{cpu.ROM[addr]:X4}");
            }
            uint ramBase = MemoryMap.UserPage.Start;
            sb.AppendLine($"User RAM     : dump {userDumpWords} words @ 0x{ramBase:X4}");
            for (int i = 0; i < userDumpWords; i++)
            {
                ushort addr = (ushort)(ramBase + i);
                sb.AppendLine($"  0x{addr:X4}: 0x{cpu.LookupValue(addr):X4}");
            }

            return sb.ToString();
        }
    }
}
