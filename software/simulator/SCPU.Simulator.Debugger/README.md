# S-CPU Simulator Debugger

UI-independent orchestration layer for S-CPU debugging.

It owns program loading, source and symbol metadata, debug sessions, execution counters, breakpoints, immutable CPU snapshots and the cancellable sequential simulation runner. It depends on the S-CPU assembler, compiler and simulator core, but has no Avalonia, Consolonia or Spectre.Console dependency.

`ProgramImage` retains the original source, generated assembly and complete assembler
artifact when they exist. Frontends can therefore present source, macro-instructions
and emitted machine instructions without rebuilding incomplete debug information.
Its debugger symbol table merges resolved constants with labels, allowing frontends to
resolve RAM/MMIO names such as registers and device ports. Mapped file-backed include
documents are retained separately for source navigation without duplicating assembler
preprocessing logic.

The runner reports cumulative wall-clock execution time while the CPU is running;
paused time is excluded and reset clears the measurement. A debug session retains
address breakpoints when the same file is reloaded and clears them when a different
file is loaded.

`AddSCPUDebugger` accepts an optional processor factory. The application composition root remains responsible for choosing and attaching MMIO devices.

Frontend-only concerns such as file pickers, dialogs, layout, keyboard gestures and UI dispatching do not belong in this project.

`ProgramExporter` exposes all assembler output formats from a loaded `ProgramImage`,
so every frontend reuses the same Binary, Intel HEX, Logisim, annotated, Verilog,
Gowin and symbol exporters.

## Add the project

Reference the debugger and, when needed, the reusable device library:

```xml
<ItemGroup>
  <ProjectReference Include="../SCPU.Simulator.Debugger/SCPU.Simulator.Debugger.csproj" />
  <ProjectReference Include="../SCPU.Simulator.Devices/SCPU.Simulator.Devices.csproj" />
</ItemGroup>
```

## Configure a debugger

The composition root chooses which MMIO devices are connected:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SCPU.Simulator.Core;
using SCPU.Simulator.Debugger;
using SCPU.Simulator.Devices;

var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<LedPanelDevice>();
services.AddSingleton<BufferedTerminalDevice>();
services.AddSCPUDebugger(provider =>
{
    var cpu = new Processor();
    cpu.Devices.Add(DeviceId.Device0, provider.GetRequiredService<LedPanelDevice>());
    cpu.Devices.Add(DeviceId.Device1, provider.GetRequiredService<BufferedTerminalDevice>());
    return cpu;
});

await using var provider = services.BuildServiceProvider();
var session = provider.GetRequiredService<DebugSession>();
await session.LoadAsync(new FileInfo("program.scode"));
```

`LoadAsync` accepts binary ROM, assembly and S-Code files. Assembly and S-Code inputs
retain their symbols, source mappings and complete assembler artifact in `ProgramImage`.

## Synchronous execution

For a console command, worker or test, use the bounded synchronous session API:

```csharp
ushort entry = checked((ushort)session.ResolveAddress("ENTRY_POINT"));
session.Breakpoints.Add(entry);

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
ExecutionResult result = await Task.Run(
    () => session.Run(until: null, maxTicks: 10_000_000, cancellation.Token),
    cancellation.Token);

Console.WriteLine($"{result.Reason} at 0x{result.ProgramCounter:X4}");
Console.WriteLine($"{session.CycleCount:N0} cycles, {session.InstructionCount:N0} instructions");
```

Only one owner may execute or mutate a `DebugSession` at a time. The CLI follows this
model: its commands call the synchronous session API from one command thread.

## Asynchronous frontend runner

Desktop and other interactive frontends should use `SimulationRunner`. It serializes
run, pause, reset and step operations, executes away from the caller, and publishes
immutable snapshots at a limited rate:

```csharp
var runner = provider.GetRequiredService<SimulationRunner>();
runner.TargetFrequency = 2_000_000; // Hardware cycles per second; 0 means maximum speed.
runner.RefreshFrequency = 20;       // Snapshot rate, independent from CPU frequency.
runner.StopOnHalt = true;

var stopped = new TaskCompletionSource<CpuSnapshot>(
    TaskCreationOptions.RunContinuationsAsynchronously);

void OnSnapshot(object? sender, CpuSnapshot snapshot)
{
    // A GUI must marshal presentation changes to its UI thread here.
    if (snapshot.State is not SimulatorState.Running)
        stopped.TrySetResult(snapshot);
}

runner.SnapshotAvailable += OnSnapshot;
try
{
    await runner.RunAsync(cancellation.Token);
    CpuSnapshot final = await stopped.Task.WaitAsync(cancellation.Token);
    Console.WriteLine($"Stopped: {final.StopReason}, PC=0x{final.ProgramCounter:X4}");
}
finally
{
    runner.SnapshotAvailable -= OnSnapshot;
    await runner.PauseAsync();
}
```

The Desktop frontend uses this runner for continuous execution and stepping. The CLI
uses `DebugSession.Run` because its commands are synchronous and already serialized.
Both paths use `Processor.ShouldFetchIR` as the instruction boundary, so HALTs,
breakpoints and target addresses cannot stop halfway through an indirect instruction.
