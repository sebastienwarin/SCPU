using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SCPU.Architecture;
using SCPU.Assembler;
using SCPU.Assembler.Model;
using SCPU.Simulator.Core;

namespace SCPU.Simulator.Debugger.Tests;

/// <summary>
/// End-to-end checks for assembler macros.  The assembler and processor are both
/// exercised, but expected values are calculated in C# so that no S-CPU macro is
/// used as its own oracle.
/// </summary>
public sealed class AssemblerMacroConformanceTests
{
    private const ushort Sentinel = 0x6D3A;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<AssemblyResult>> Assemblies = new();

    private static readonly ushort[] BoundaryValues =
    [
        0x0000, 0x0001, 0x0002, 0x7FFE, 0x7FFF, 0x8000, 0x8001, 0xFFFD,
        0xFFFE, 0xFFFF, 0x00FF, 0x0100, 0x0FFF, 0x1000, 0x5555, 0xAAAA
    ];

    [Fact]
    public async Task State_macros_preserve_accumulator_and_carry_as_documented()
    {
        foreach (var value in new ushort[] { 0x0000, 0x0001, 0x7FFF, 0x8000, 0xFFFF })
        {
            foreach (var carryIn in new[] { false, true })
            {
                var nop = await ExecuteAsync("NOP", value, 0, carryIn);
                AssertResult("NOP", nop, value, carryIn);
                AssertScratchUnchanged(nop);
                var ldc = await ExecuteAsync("LDC inputB", Sentinel, value, carryIn);
                AssertResult("LDC", ldc, value, carryIn);
                AssertScratchUnchanged(ldc);
                var clc = await ExecuteAsync("CLC", value, 0, carryIn);
                AssertResult("CLC", clc, value, false);
                AssertScratchUnchanged(clc);
                var sec = await ExecuteAsync("SEC", value, 0, carryIn);
                AssertResult("SEC", sec, value, true);
                AssertScratchUnchanged(sec);
            }
        }
    }

    [Fact]
    public async Task Unary_arithmetic_and_shifts_match_their_bit_level_oracles()
    {
        foreach (var value in BoundaryValues)
        {
            var inc = await ExecuteAsync("INC", value);
            AssertResult("INC", inc, (ushort)(value + 1), value == ushort.MaxValue);

            var dec = await ExecuteAsync("DEC", value);
            AssertResult("DEC", dec, (ushort)(value - 1), value != 0);

            var neg = await ExecuteAsync("NEG", value);
            AssertResult("NEG", neg, (ushort)-value, value == 0);

            foreach (var carryIn in new[] { false, true })
            {
                AssertResult("NOT", await ExecuteAsync("NOT", value, 0, carryIn), (ushort)~value, carryIn);
                AssertResult("LSL", await ExecuteAsync("LSL", value, 0, carryIn), (ushort)(value << 1), (value & 0x8000) != 0);
                AssertResult("ROL", await ExecuteAsync("ROL", value, 0, carryIn),
                    (ushort)((value << 1) | (value >> 15)), false);
                AssertResult("ROR", await ExecuteAsync("ROR", value, 0, carryIn),
                    (ushort)((value >> 1) | (value << 15)), false);
                AssertResult("LSR", await ExecuteAsync("LSR", value, 0, carryIn), (ushort)(value >> 1), false);
            }
        }
    }

    [Fact]
    public async Task Binary_arithmetic_and_logic_cover_every_boundary_pair_and_both_input_flags()
    {
        foreach (var a in BoundaryValues)
        foreach (var b in BoundaryValues)
        foreach (var carryIn in new[] { false, true })
        {
            var subSigned = (int)a - b;
            AssertResult("SUB", await ExecuteAsync("SUB inputB", a, b, carryIn),
                (ushort)subSigned, subSigned < 0);

            AssertResult("AND", await ExecuteAsync("AND inputB", a, b, carryIn),
                (ushort)(a & b), false);
            AssertResult("NAND", await ExecuteAsync("NAND inputB", a, b, carryIn),
                (ushort)~(a & b), false);
            AssertResult("OR", await ExecuteAsync("OR inputB", a, b, carryIn),
                (ushort)(a | b), carryIn);
            AssertResult("XOR", await ExecuteAsync("XOR inputB", a, b, carryIn),
                (ushort)(a ^ b), false);
        }
    }

    [Fact]
    public async Task Adc_and_sbc_match_wide_oracles_for_boundaries_and_random_pairs()
    {
        var pairs = new List<(ushort A, ushort B)>();
        foreach (var a in BoundaryValues)
        foreach (var b in BoundaryValues)
            pairs.Add((a, b));

        var random = new Random(0x5C0);
        for (var i = 0; i < 1_024; i++)
            pairs.Add(((ushort)random.Next(0x10000), (ushort)random.Next(0x10000)));

        foreach (var (a, b) in pairs)
        foreach (var carryIn in new[] { false, true })
        {
            var adcWide = (uint)a + b + (carryIn ? 1u : 0u);
            AssertResult("ADC", await ExecuteAsync("ADC inputB", a, b, carryIn),
                (ushort)adcWide, adcWide > ushort.MaxValue);

            var sbcSigned = (int)a - b - (carryIn ? 1 : 0);
            AssertResult("SBC", await ExecuteAsync("SBC inputB", a, b, carryIn),
                (ushort)sbcSigned, sbcSigned < 0);
        }
    }

    [Fact]
    public async Task Required_regression_vectors_are_present_and_match_the_oracles()
    {
        var xorVectors = new (ushort A, ushort B, ushort Expected)[]
        {
            (0x0000, 0x0000, 0x0000), (0x0000, 0xFFFF, 0xFFFF),
            (0xFFFF, 0xFFFF, 0x0000), (0xAAAA, 0x5555, 0xFFFF),
            (0x8000, 0x0001, 0x8001), (0x1234, 0xABCD, 0xB9F9)
        };
        foreach (var vector in xorVectors)
            AssertResult("XOR regression", await ExecuteAsync("XOR inputB", vector.A, vector.B), vector.Expected, false);

        var adcVectors = new (ushort A, ushort B, bool Carry)[]
        {
            (0xFFFF, 0x0000, true), (0xFFFF, 0x0001, true), (0xFFFF, 0x1234, true),
            (0xFFFE, 0x0001, true), (0x0000, 0xFFFF, true), (0x8000, 0x8000, false)
        };
        foreach (var vector in adcVectors)
        {
            var wide = (uint)vector.A + vector.B + (vector.Carry ? 1u : 0u);
            AssertResult("ADC regression", await ExecuteAsync("ADC inputB", vector.A, vector.B, vector.Carry),
                (ushort)wide, wide > ushort.MaxValue);
        }

        var sbcVectors = new (ushort A, ushort B)[]
        {
            (0x0000, 0x0000), (0x0000, 0x0001), (0x0000, 0x1234),
            (0x0001, 0x0000), (0xFFFF, 0xFFFF), (0x8000, 0x7FFF)
        };
        foreach (var vector in sbcVectors)
        {
            var signed = (int)vector.A - vector.B - 1;
            AssertResult("SBC regression", await ExecuteAsync("SBC inputB", vector.A, vector.B, true),
                (ushort)signed, signed < 0);
        }
    }

    [Fact]
    public async Task Scratch_register_contracts_and_operand_memory_are_observable()
    {
        AssertScratchUnchanged(await ExecuteAsync("NOT", 0x1234, carryIn: true));
        AssertScratchUnchanged(await ExecuteAsync("OR inputB", 0x1234, 0xABCD, true));

        var and = await ExecuteAsync("AND inputB", 0x1234, 0xABCD, true);
        Assert.Equal(unchecked((ushort)~0x1234), and.Rpar);
        Assert.Equal(Sentinel, and.RparPlusOne);

        var nand = await ExecuteAsync("NAND inputB", 0x1234, 0xABCD, true);
        Assert.Equal(unchecked((ushort)~0x1234), nand.Rpar);
        Assert.Equal(Sentinel, nand.RparPlusOne);

        var xor = await ExecuteAsync("XOR inputB", 0x1234, 0xABCD, true);
        var nor = unchecked((ushort)~(0x1234 | 0xABCD));
        Assert.Equal((ushort)~(0x1234 | nor), xor.Rpar);
        Assert.Equal(nor, xor.RparPlusOne);

        var adcAlias = await ExecuteAsync("ADC RPAR", 0x1111, 0x2222, true, operandInRpar: true);
        AssertResult("ADC RPAR alias", adcAlias, 0x3334, false);
        Assert.Equal((ushort)0x3334, adcAlias.Rpar);

        var sbcAlias = await ExecuteAsync("SBC RPAR", 0x1111, 0x2222, true, operandInRpar: true);
        AssertResult("SBC RPAR alias", sbcAlias, 0xEEEE, true);
        Assert.Equal((ushort)0xEEEE, sbcAlias.Rpar);

        var lsl = await ExecuteAsync("LSL", 0x8001);
        Assert.Equal((ushort)0x8001, lsl.Rpar);
        var rol = await ExecuteAsync("ROL", 0x8001);
        Assert.Equal((ushort)0x8001, rol.Rpar);
        var ror = await ExecuteAsync("ROR", 0x8001);
        Assert.NotEqual(Sentinel, ror.Rpar);
        var lsr = await ExecuteAsync("LSR", 0x8001);
        Assert.Equal(unchecked((ushort)~0xC000), lsr.Rpar);

        // AND/NAND forbid operand RPAR, and XOR forbids RPAR/RPAR+1: those
        // aliases are the documented clobber conflicts. ADC/SBC aliases are
        // supported and are exercised above.
    }

    private static void AssertScratchUnchanged(Execution execution)
    {
        Assert.Equal(Sentinel, execution.Rpar);
        Assert.Equal(Sentinel, execution.RparPlusOne);
    }

    private static void AssertResult(string operation, Execution actual, ushort expectedValue, bool expectedCarry)
    {
        Assert.True(actual.Accumulator == expectedValue,
            $"{operation} value mismatch: expected 0x{expectedValue:X4}, got 0x{actual.Accumulator:X4}; " +
            $"CF expected {(expectedCarry ? 1 : 0)}, got {(actual.Carry ? 1 : 0)}.");
        Assert.True(actual.Carry == expectedCarry,
            $"{operation} CF mismatch while value matched (0x{expectedValue:X4}): " +
            $"expected {(expectedCarry ? 1 : 0)}, got {(actual.Carry ? 1 : 0)}.");
        Assert.Equal(actual.InputBInitial, actual.InputBAfter);
        Assert.Equal(Sentinel, actual.GuardAfter);
    }

    private static async Task<Execution> ExecuteAsync(
        string operation, ushort a, ushort b = 0, bool carryIn = false, bool operandInRpar = false)
    {
        var carrySetup = carryIn ? "SEC" : "CLC";
        var source = $$"""
            #bank userpage
            inputA: #res 1
            inputB: #res 1
            guard:  #res 1
            #bank prg
            start:
                LDA inputA
                {{carrySetup}}
                {{operation}}
            done:
                HALT
            """;

        var assembled = await Assemblies.GetOrAdd(source, AssembleAsync);

        var cpu = new Processor();
        cpu.Load(assembled.Binary);
        WriteRam(cpu, assembled.Labels["inputA"], a);
        WriteRam(cpu, assembled.Labels["inputB"], b);
        WriteRam(cpu, assembled.Labels["guard"], Sentinel);
        WriteRam(cpu, ReservedAddresses.ParameterRegister, operandInRpar ? b : Sentinel);
        WriteRam(cpu, ReservedAddresses.ParameterRegister + 1, Sentinel);

        var haltAddress = (ushort)assembled.Labels["done"];
        var ticks = 0;
        // Stop after fetching HALT. This includes the S0 side effect that clears CF
        // after a JCC-based macro such as CLC, JCS, ADC, or SBC.
        while ((cpu.StepCounter != Step.S1 || cpu.ProgramCounter != haltAddress + 1) && ticks++ < 1_000)
            cpu.Tick();
        Assert.True(ticks < 1_000, $"{operation} did not halt.");

        return new Execution(
            cpu.AccumulatorRegister,
            cpu.CarryFlag,
            ReadRam(cpu, ReservedAddresses.ParameterRegister),
            ReadRam(cpu, ReservedAddresses.ParameterRegister + 1),
            b,
            ReadRam(cpu, assembled.Labels["inputB"]),
            ReadRam(cpu, assembled.Labels["guard"]));
    }

    private static async Task<AssemblyResult> AssembleAsync(string source)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.ClearProviders());
        services.AddAssembler();
        await using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<SCPU.Assembler.Assembler>().AssembleAsync(new AssemblyRequest
        {
            Source = SourceDocument.FromInline(source, "macro-conformance.asm")
        });
    }

    private static void WriteRam(Processor cpu, uint virtualAddress, ushort value) =>
        cpu.RAM[virtualAddress - MemoryMap.Ram.Start] = value;

    private static ushort ReadRam(Processor cpu, uint virtualAddress) =>
        cpu.RAM[virtualAddress - MemoryMap.Ram.Start];

    private sealed record Execution(
        ushort Accumulator,
        bool Carry,
        ushort Rpar,
        ushort RparPlusOne,
        ushort InputBInitial,
        ushort InputBAfter,
        ushort GuardAfter);
}
