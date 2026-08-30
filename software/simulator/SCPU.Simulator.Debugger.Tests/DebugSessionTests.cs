using Microsoft.Extensions.Logging.Abstractions;
using SCPU.Architecture;
using SCPU.Simulator.Debugger;
using SCPU.Simulator.Core;

namespace SCPU.Simulator.Debugger.Tests;

public sealed class DebugSessionTests
{
    [Fact]
    public void Halt_detection_includes_both_self_loops_emitted_by_the_macro()
    {
        ushort[] rom = [0xF000, 0xF001];

        var addresses = InstructionUtils.DetectHaltAddresses(rom);

        Assert.Equal(new ushort[] { 0, 1 }, addresses.Order().ToArray());
    }

    [Fact]
    public void Halt_is_not_reported_while_an_indirect_instruction_is_pending()
    {
        var session = new DebugSession(new Processor());
        session.Load(new ProgramImage(new FileInfo("indirect-before-halt.bin"), ProgramFileType.Binary,
            [0x7F, 0x00, 0xF0, 0x01], [], new Dictionary<string, uint>(), new HashSet<ushort> { 1 }));

        session.StepCycle(); // Fetch indirect ADD at address 0.
        session.StepCycle(); // Resolve its address and return temporarily to S0.

        Assert.Equal(1, session.Cpu.ProgramCounter);
        Assert.Equal(Step.S0, session.Cpu.StepCounter);
        Assert.True(session.Cpu.IndirectedFlag);
        Assert.False(session.IsAtHalt);
    }

    [Fact]
    public void Synchronous_run_does_not_hit_a_breakpoint_during_indirect_execution()
    {
        var session = CreateSession([0x7F, 0x00, 0x00, 0x00]);
        session.Breakpoints.Add(1);

        var result = session.Run(until: null, maxTicks: 10);

        Assert.Equal(StopReason.Breakpoint, result.Reason);
        Assert.Equal(4, result.Ticks);
        Assert.True(session.Cpu.ShouldFetchIR);
        Assert.Equal(1, session.Cpu.ProgramCounter);
    }

    [Fact]
    public void Reset_clears_registers_counters_and_ram_but_keeps_rom()
    {
        var session = CreateSession([0xE0, 0x01]);
        session.Cpu.RAM[3] = 42;
        session.StepCycle();
        session.Reset();

        Assert.Equal(0, session.Cpu.ProgramCounter);
        Assert.Equal(0, session.CycleCount);
        Assert.Equal(0, session.Cpu.RAM[3]);
        Assert.Equal(0xE001, session.Cpu.ROM[0]);
    }

    [Fact]
    public void Cycle_and_instruction_steps_have_distinct_semantics()
    {
        var session = CreateSession([0xE0, 0x01]); // JCC immediate 1
        session.StepCycle();
        Assert.Equal(Step.S1, session.Cpu.StepCounter);
        Assert.Equal(1, session.CycleCount);

        session.Reset();
        session.StepInstruction();
        Assert.Equal(Step.S0, session.Cpu.StepCounter);
        Assert.Equal(2, session.CycleCount);
        Assert.Equal(1, session.InstructionCount);
    }

    [Fact]
    public void Source_step_runs_all_instructions_mapped_to_the_current_line()
    {
        var firstLine = new SourceLocation("program.asm", 4, "macro");
        var secondLine = new SourceLocation("program.asm", 5, "next");
        var session = new DebugSession(new Processor());
        session.Load(new ProgramImage(new FileInfo("program.bin"), ProgramFileType.Binary,
            [0xF0, 0x01, 0xF0, 0x02, 0xF0, 0x02],
            [new RomEntry(0, 0xF001, "JCC #0x001", "", firstLine),
             new RomEntry(1, 0xF002, "JCC #0x002", "", firstLine),
             new RomEntry(2, 0xF002, "JCC #0x002", "", secondLine)],
            new Dictionary<string, uint>(), new HashSet<ushort>()));

        session.StepSource();

        Assert.Equal(2, session.Cpu.ProgramCounter);
        Assert.Equal(2, session.InstructionCount);
    }

    [Fact]
    public void Source_locations_resolve_by_file_name_and_to_every_emitted_word()
    {
        var source = new SourceLocation(Path.Combine("src", "program.asm"), 7, "macro");
        var session = new DebugSession(new Processor());
        session.Load(new ProgramImage(new FileInfo("program.asm"), ProgramFileType.Assembly,
            [0x00, 0x00, 0x00, 0x00],
            [new RomEntry(0, 0, "", "", source), new RomEntry(1, 0, "", "", source)],
            new Dictionary<string, uint>(), new HashSet<ushort>())
        {
            SourceDocuments = new Dictionary<string, string> { [source.Identifier] = "\n\n\n\n\n\nmacro" }
        });

        Assert.Equal(new ushort[] { 0, 1 }, session.ResolveSourceAddresses("program.asm:7"));
        Assert.Equal(new ushort[] { 0, 1 }, session.ResolveSourceAddresses(":7"));
        Assert.True(DebugSession.IsSourceLocation("program.asm:7"));
        Assert.False(DebugSession.IsSourceLocation("label"));
    }

    [Fact]
    public void Debugger_memory_write_uses_the_shared_memory_map()
    {
        var session = CreateSession([0x00, 0x00]);

        session.WriteMemory(0x0000, 0x1234);
        session.WriteMemory(0x12100, 0x5678);

        Assert.Equal(0x1234, session.Cpu.ROM[0]);
        Assert.Equal(0x5678, session.Cpu.LookupValue(0x12100));
        Assert.Equal(0x1234, session.ReadMemory(0x0000));
        Assert.Equal(0x5678, session.ReadMemory(0x12100));
        Assert.Throws<InvalidOperationException>(() => session.WriteMemory(0x12A01, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ReadMemory(0x20000));
    }

    [Fact]
    public void Loading_the_same_file_keeps_breakpoints_but_a_different_file_clears_them()
    {
        var session = new DebugSession(new Processor());
        session.Load(CreateImage("first.bin"));
        session.Breakpoints.Add(0x12);

        session.Load(CreateImage(Path.Combine(".", "first.bin")));
        Assert.Contains((ushort)0x12, session.Breakpoints);

        session.Load(CreateImage("second.bin"));
        Assert.Empty(session.Breakpoints);
    }

    [Fact]
    public void Watches_resolve_symbols_and_are_unique_by_address()
    {
        var session = new DebugSession(new Processor());
        session.Load(new ProgramImage(new FileInfo("watch.bin"), ProgramFileType.Binary, [0x00, 0x00], [],
            new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase) { ["counter"] = 0x12100 },
            new HashSet<ushort>()));
        session.WriteMemory(0x12100, 42);

        var symbolic = session.AddWatch("counter");
        var numeric = session.AddWatch("0x12100");

        Assert.Same(symbolic, numeric);
        Assert.Equal(0x12100u, symbolic.Address);
        Assert.Equal((ushort)42, session.ReadMemory(symbolic.Address));
        Assert.Single(session.Watches);
    }

    [Fact]
    public void Loading_another_file_clears_watches_but_reload_semantics_keep_them()
    {
        var session = new DebugSession(new Processor());
        session.Load(CreateImage("first.bin"));
        session.AddWatch("0x12100");

        session.Load(CreateImage(Path.Combine(".", "first.bin")));
        Assert.Single(session.Watches);

        session.Load(CreateImage("second.bin"));
        Assert.Empty(session.Watches);
    }

    [Fact]
    public async Task Runner_stops_on_breakpoint_and_does_not_start_a_second_loop()
    {
        var session = CreateSession([0xE0, 0x00]);
        session.Breakpoints.Add(0);
        await using var runner = new SimulationRunner(session, NullLogger<SimulationRunner>.Instance) { TargetFrequency = 0 };

        await Task.WhenAll(runner.RunAsync(), runner.RunAsync());
        await WaitUntilAsync(() => !runner.IsRunning);

        Assert.Equal(SimulatorState.Breakpoint, session.State);
        Assert.Equal(2, session.CycleCount);
    }

    [Fact]
    public async Task Every_step_mode_can_continue_from_a_breakpoint_stop()
    {
        var session = CreateSession([0xE0, 0x00]);
        session.Breakpoints.Add(0);
        await using var runner = new SimulationRunner(session, NullLogger<SimulationRunner>.Instance)
        {
            TargetFrequency = 0,
            StopOnHalt = false
        };

        await runner.RunAsync();
        await WaitUntilAsync(() => session.State == SimulatorState.Breakpoint);
        var before = session.CycleCount;
        await runner.StepCycleAsync();
        Assert.True(session.CycleCount > before);

        await runner.RunAsync();
        await WaitUntilAsync(() => session.State == SimulatorState.Breakpoint);
        before = session.CycleCount;
        await runner.StepInstructionAsync();
        Assert.True(session.CycleCount > before);

        await runner.RunAsync();
        await WaitUntilAsync(() => session.State == SimulatorState.Breakpoint);
        before = session.CycleCount;
        await runner.StepSourceAsync();
        Assert.True(session.CycleCount > before);
        Assert.Equal(SimulatorState.Paused, session.State);
    }

    [Fact]
    public async Task Runner_stops_before_executing_a_detected_halt()
    {
        var session = new DebugSession(new Processor());
        session.Load(new ProgramImage(new FileInfo("halt.bin"), ProgramFileType.Binary, [0xE0, 0x00], [],
            new Dictionary<string, uint>(), new HashSet<ushort> { 0 }));
        await using var runner = new SimulationRunner(session, NullLogger<SimulationRunner>.Instance) { TargetFrequency = 0 };

        await runner.RunAsync();
        await WaitUntilAsync(() => !runner.IsRunning);

        Assert.Equal(SimulatorState.Halted, session.State);
        Assert.Equal(0, session.CycleCount);
    }

    [Fact]
    public async Task Pause_is_cancellable_and_deterministic()
    {
        var session = CreateSession([0xE0, 0x00]);
        await using var runner = new SimulationRunner(session, NullLogger<SimulationRunner>.Instance) { TargetFrequency = 0, StopOnHalt = false };
        await runner.RunAsync();
        await WaitUntilAsync(() => session.CycleCount > 100);
        await runner.PauseAsync();
        var stoppedAt = session.CycleCount;
        await Task.Delay(20);

        Assert.False(runner.IsRunning);
        Assert.Equal(stoppedAt, session.CycleCount);
        Assert.Equal(SimulatorState.Paused, session.State);
    }

    [Fact]
    public async Task Restart_resets_the_session_before_starting_a_new_run()
    {
        var session = CreateSession([0xE0, 0x00]);
        session.Cpu.RAM[4] = 0x1234;
        session.StepCycle();
        await using var runner = new SimulationRunner(session, NullLogger<SimulationRunner>.Instance)
        {
            TargetFrequency = 5,
            StopOnHalt = false
        };

        await runner.RestartAsync();
        await runner.PauseAsync();

        Assert.Equal(0, session.Cpu.RAM[4]);
        Assert.True(session.CycleCount <= 1);
        Assert.Equal(SimulatorState.Paused, session.State);
    }

    [Fact]
    public async Task Execution_time_accumulates_only_while_running_and_reset_clears_it()
    {
        var session = CreateSession([0xE0, 0x00]);
        CpuSnapshot? lastSnapshot = null;
        await using var runner = new SimulationRunner(session, NullLogger<SimulationRunner>.Instance)
        {
            TargetFrequency = 100,
            StopOnHalt = false
        };
        runner.SnapshotAvailable += (_, snapshot) => lastSnapshot = snapshot;

        await runner.RunAsync();
        await WaitUntilAsync(() => session.CycleCount >= 3);
        await runner.PauseAsync();

        var elapsed = Assert.IsType<CpuSnapshot>(lastSnapshot).ExecutionTime;
        Assert.True(elapsed > TimeSpan.Zero);
        await Task.Delay(20);
        Assert.Equal(elapsed, lastSnapshot.ExecutionTime);

        await runner.ResetAsync();
        Assert.Equal(TimeSpan.Zero, lastSnapshot.ExecutionTime);
    }

    private static DebugSession CreateSession(byte[] binary)
    {
        var session = new DebugSession(new Processor());
        session.Load(new ProgramImage(new FileInfo("test.bin"), ProgramFileType.Binary, binary, [],
            new Dictionary<string, uint>(), new HashSet<ushort>()));
        return session;
    }

    private static ProgramImage CreateImage(string path) =>
        new(new FileInfo(path), ProgramFileType.Binary, [0x00, 0x00], [],
            new Dictionary<string, uint>(), new HashSet<ushort>());

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition()) await Task.Delay(1, timeout.Token);
    }
}
