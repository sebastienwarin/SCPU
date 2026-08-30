# S-CPU Simulator Core

**SCPU.Simulator.Core** is the UI-independent execution model for the educational
16-bit S-CPU. It provides the processor registers, ROM, RAM, S0/S1 pipeline, instruction
execution and MMIO device bus.

Use this project when a .NET application needs direct, deterministic control over the
CPU. For program compilation, asynchronous execution, breakpoints, source mappings and
debug snapshots, use `SCPU.Simulator.Debugger` on top of Core.

## Add the project

Add a project reference:

```xml
<ItemGroup>
  <ProjectReference Include="../SCPU.Simulator.Core/SCPU.Simulator.Core.csproj" />
</ItemGroup>
```

Then import its namespace:

```csharp
using SCPU.Simulator.Core;
```

## Quick start

Load a binary ROM and run it with cancellation, a cycle limit and reliable HALT
detection:

```csharp
using SCPU.Architecture;
using SCPU.Simulator.Core;

byte[] binary = await File.ReadAllBytesAsync("program.bin");
var processor = new Processor();
processor.Load(binary);
processor.Reset();

var haltAddresses = InstructionUtils.DetectHaltAddresses(processor.ROM);
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
const long maximumCycles = 10_000_000;
long cycles = 0;

while (!processor.ShouldFetchIR || !haltAddresses.Contains(processor.ProgramCounter))
{
    timeout.Token.ThrowIfCancellationRequested();
    if (cycles++ >= maximumCycles)
        throw new TimeoutException("The program did not reach HALT within the cycle limit.");

    processor.Tick();
}

Console.WriteLine($"HALT reached after {cycles:N0} cycles");
Console.WriteLine($"PC = 0x{processor.ProgramCounter:X4}");
Console.WriteLine($"AC = 0x{processor.AccumulatorRegister:X4}");
Console.WriteLine($"IR = 0x{processor.InstructionRegister:X4}");
Console.WriteLine($"C  = {(processor.CarryFlag ? 1 : 0)}");
```

`Tick()` advances exactly one hardware phase:

* `S0` fetches the next instruction;
* `S1` executes it;
* indirect addressing may require additional S0/S1 phases.

`ShouldFetchIR` is the reliable instruction boundary. Do not replace it with a check for
`StepCounter == Step.S0`: an indirect instruction briefly returns to S0 after resolving
its address but before executing. Stopping at that intermediate state skips the pending
instruction.

The example is appropriate when an application deliberately owns the low-level CPU
loop. For most tools, prefer `DebugSession` or `SimulationRunner`; they already provide
HALT handling, breakpoints, counters, cancellation and serialized execution.

## Loading ROM data

Load a byte array:

```csharp
byte[] rom = await File.ReadAllBytesAsync("program.bin");
processor.Load(rom);
processor.Reset();
```

ROM bytes use the S-CPU binary format: each 16-bit word is stored **most significant
byte first**. An odd final byte becomes the high byte of the last word and its low byte
is padded with zero. Loading a new image clears the unused remainder of ROM.

`Reset()` preserves ROM, resets registers and flags, clears RAM and resets every
connected MMIO device.

## Processor state

| Property | Description |
| --- | --- |
| `ProgramCounter` | Address of the next ROM word |
| `StepCounter` | Current S0/S1 pipeline phase |
| `InstructionRegister` | Encoded current instruction |
| `AccumulatorRegister` | 16-bit accumulator |
| `CarryFlag` | Carry flag |
| `IndirectedFlag` | Indirect operand resolution is active |
| `CurrentInstruction` | Decoded native opcode |
| `CurrentAddressingMode` | Decoded addressing mode |
| `CurrentInstructionOperand` | Raw 11-bit operand |
| `DataBus` | Current bus value; MMIO reads may have side effects |
| `PeekDataBus` | Diagnostic bus value without consuming device input |
| `ROM` | 64K 16-bit program words |
| `RAM` | 2K 16-bit data words |

ROM and RAM arrays are intentionally exposed for simulators and test tools. Application
code is responsible for avoiding concurrent writes while the processor is ticking.

## Memory access

`LookupValue` reads an S-CPU virtual address:

```csharp
ushort firstRomWord = processor.LookupValue(0x00000);
ushort firstRamWord = processor.LookupValue(0x12000);
ushort deviceValue = processor.LookupValue(0x12801);
```

It returns zero for an invalid address or an unconnected MMIO device. Use the constants
and address helpers from `SCPU.Architecture` instead of duplicating memory-map values in
application code.

## Connecting MMIO devices

Attach an `IODevice` before resetting or running the processor:

```csharp
var output = new SimpleOutputDevice();
processor.Devices[DeviceId.Device0] = output;
processor.Reset();
```

A minimal device implementation looks like this:

```csharp
public sealed class SimpleOutputDevice : IODevice
{
    private readonly ushort[] _registers = new ushort[256];

    public override ushort this[byte address]
    {
        get => _registers[address];
        set => _registers[address] = value;
    }

    public override ushort Peek(byte address) => _registers[address];

    public override void Reset() => Array.Clear(_registers);
}
```

`Peek` must not consume input or mutate device state. Debuggers use it to inspect MMIO
safely. Reusable terminal and LED/display implementations are available in
`SCPU.Simulator.Devices`.

## Execution and threading

`Processor` is synchronous and deliberately does not create tasks, timers or threads.
It is not thread-safe: one owner must serialize calls to `Tick`, `Reset`, ROM/RAM access
and device mutation.

For an application runner:

* execute ticks away from the UI thread;
* use cancellation for pause and shutdown;
* never run two tick loops concurrently;
* publish immutable snapshots to the UI at a limited refresh rate.

`SCPU.Simulator.Debugger` already provides this execution model through its sequential
asynchronous runner and should be preferred for debugger applications.

## Related projects

* **SCPU.Architecture** — instruction definitions, addressing modes and memory map.
* **SCPU.Simulator.Devices** — reusable terminal and LED/display MMIO devices.
* **SCPU.Simulator.Debugger** — loading, sessions, breakpoints, snapshots and execution runner.
* **SCPU.Simulator.Desktop** — graphical simulator and debugger frontend.
* **SCPU.Simulator.CLI** — command-line simulator and debugger frontend.
