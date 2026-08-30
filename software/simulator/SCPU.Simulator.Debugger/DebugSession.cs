using SCPU.Architecture;
using SCPU.Simulator.Core;

namespace SCPU.Simulator.Debugger;

/// <summary>Owns one loaded program and its mutable debugger state.</summary>
public sealed class DebugSession
{
    public const long DefaultRunTickLimit = 10_000_000;
    private readonly ProgramLoader? _loader;

    /// <summary>Creates a session around a processor.</summary>
    /// <param name="processor">Processor controlled by the session.</param>
    /// <param name="loader">Optional source and ROM loader.</param>
    public DebugSession(Processor processor, ProgramLoader? loader = null)
    {
        Cpu = processor;
        _loader = loader;
    }

    public Processor Cpu { get; }
    public ProgramImage? Program { get; private set; }
    public long CycleCount { get; private set; }
    public long InstructionCount { get; private set; }
    public SimulatorState State { get; internal set; } = SimulatorState.Ready;
    public StopReason? LastStopReason { get; internal set; }
    public string? Fault { get; internal set; }
    public HashSet<ushort> Breakpoints { get; } = [];
    public List<Watch> Watches { get; } = [];
    public long TickCount => CycleCount;
    public FileInfo? LoadedFile => Program?.File;
    public IReadOnlyDictionary<string, uint> Symbols => Program?.Symbols ?? EmptySymbols;
    public IReadOnlySet<ushort> HaltAddresses => Program?.HaltAddresses ?? EmptyHaltAddresses;
    private static readonly IReadOnlyDictionary<string, uint> EmptySymbols = new Dictionary<string, uint>();
    private static readonly IReadOnlySet<ushort> EmptyHaltAddresses = new HashSet<ushort>();

    /// <summary>Loads, compiles or assembles a program file and resets the session.</summary>
    public async Task LoadAsync(FileInfo file, CancellationToken cancellationToken = default)
    {
        if (_loader is null) throw new InvalidOperationException("This debug session has no program loader.");
        Load(await _loader.LoadAsync(file.FullName, cancellationToken));
    }

    /// <summary>Loads the current file again.</summary>
    public Task ReloadAsync(CancellationToken cancellationToken = default) => LoadedFile is null
        ? throw new InvalidOperationException("No program has been loaded.")
        : LoadAsync(LoadedFile, cancellationToken);

    /// <summary>Installs a prepared program image and resets the processor.</summary>
    public void Load(ProgramImage image)
    {
        if (Program is not null && !PathsReferToSameFile(Program.File.FullName, image.File.FullName))
        {
            Breakpoints.Clear();
            Watches.Clear();
        }
        Program = image;
        Cpu.Load(image.Binary);
        Reset();
    }

    /// <summary>Resets CPU state and execution counters without clearing the loaded ROM.</summary>
    public void Reset()
    {
        Cpu.Reset();
        CycleCount = 0;
        InstructionCount = 0;
        State = SimulatorState.Ready;
        LastStopReason = null;
        Fault = null;
    }

    /// <summary>Executes one S0 or S1 hardware cycle.</summary>
    public void StepCycle()
    {
        Cpu.Tick();
        CycleCount++;
        if (Cpu.ShouldFetchIR)
            InstructionCount++;
    }

    /// <summary>Executes one complete instruction, including indirect cycles.</summary>
    public void StepInstruction(CancellationToken cancellationToken = default)
    {
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            StepCycle();
        } while (Cpu.StepCounter != Step.S0 || Cpu.IndirectedFlag);
    }

    /// <summary>Executes instructions until the mapped source line changes.</summary>
    public void StepSource(CancellationToken cancellationToken = default)
    {
        var source = Program?.Rom.FirstOrDefault(entry => entry.Address == Cpu.ProgramCounter)?.Source;
        var visited = new HashSet<ushort> { Cpu.ProgramCounter };
        StepInstruction(cancellationToken);
        if (source is null) return;

        while (!IsAtHalt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = Program?.Rom.FirstOrDefault(entry => entry.Address == Cpu.ProgramCounter)?.Source;
            if (current is null || current.Identifier != source.Identifier || current.Line != source.Line) return;
            if (Breakpoints.Contains(Cpu.ProgramCounter)) return;
            // A source line may contain a loop. Stop after one pass instead of
            // trapping an interactive step forever on the same mapping.
            if (!visited.Add(Cpu.ProgramCounter)) return;
            StepInstruction(cancellationToken);
        }
    }

    /// <summary>Executes a fixed number of complete instructions.</summary>
    public ExecutionResult StepInstructions(int count, CancellationToken cancellationToken = default)
    {
        var before = CycleCount;
        for (var i = 0; i < count; i++)
        {
            if (IsAtHalt) return Result(StopReason.Halt, before);
            if (cancellationToken.IsCancellationRequested) return Result(StopReason.Cancelled, before);
            StepInstruction(cancellationToken);
        }
        return Result(StopReason.Completed, before);
    }

    /// <summary>Advances across one or more mapped source lines.</summary>
    public ExecutionResult StepSourceLines(int count, CancellationToken cancellationToken = default)
    {
        var before = CycleCount;
        for (var i = 0; i < count; i++)
        {
            if (IsAtHalt) return Result(StopReason.Halt, before);
            if (cancellationToken.IsCancellationRequested) return Result(StopReason.Cancelled, before);
            StepSource(cancellationToken);
            if (Breakpoints.Contains(Cpu.ProgramCounter) && Cpu.ShouldFetchIR)
                return Result(StopReason.Breakpoint, before);
        }
        return Result(StopReason.Completed, before);
    }

    /// <summary>Executes a fixed number of hardware cycles.</summary>
    public ExecutionResult Tick(int count, CancellationToken cancellationToken = default)
    {
        var before = CycleCount;
        for (var i = 0; i < count; i++)
        {
            if (cancellationToken.IsCancellationRequested) return Result(StopReason.Cancelled, before);
            StepCycle();
        }
        return Result(StopReason.Completed, before);
    }

    /// <summary>Runs synchronously until a stop condition is reached.</summary>
    public ExecutionResult Run(uint? until, long? maxTicks, CancellationToken cancellationToken = default)
    {
        var before = CycleCount;
        var limit = maxTicks ?? DefaultRunTickLimit;
        var first = true;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested) return Result(StopReason.Cancelled, before);
            if (IsAtHalt) return Result(StopReason.Halt, before);
            if (until == Cpu.ProgramCounter && Cpu.ShouldFetchIR) return Result(StopReason.Address, before);
            if (!first && Cpu.ShouldFetchIR && Breakpoints.Contains(Cpu.ProgramCounter))
                return Result(StopReason.Breakpoint, before);
            if (CycleCount - before >= limit) return Result(StopReason.TickLimit, before);
            StepCycle();
            first = false;
        }
    }

    /// <summary>Resolves a symbol, hexadecimal value, decimal value or PC-relative address.</summary>
    public uint ResolveAddress(string text)
    {
        text = text.Trim();
        if (Symbols.TryGetValue(text, out var symbol)) return symbol;
        if ((text.StartsWith('+') || text.StartsWith('-')) && int.TryParse(text, out var displacement))
            return unchecked((ushort)(Cpu.ProgramCounter + displacement));
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var hex)) return hex;
        if (uint.TryParse(text, out var value)) return value;
        throw new FormatException($"Unknown address or symbol '{text}'.");
    }

    /// <summary>Resolves <c>file:line</c> or <c>:line</c> to generated ROM addresses.</summary>
    public IReadOnlyList<ushort> ResolveSourceAddresses(string text)
    {
        if (Program is null) throw new InvalidOperationException("No program has been loaded.");
        var separator = text.LastIndexOf(':');
        if (separator < 0 || !int.TryParse(text.AsSpan(separator + 1), out var line) || line <= 0)
            throw new FormatException($"Invalid source location '{text}'. Expected file:line or :line.");

        var requestedFile = text[..separator].Trim();
        var currentSource = Program.Rom.FirstOrDefault(entry => entry.Address == Cpu.ProgramCounter)?.Source?.Identifier;
        var defaultSource = currentSource ?? Program.GeneratedAssemblyIdentifier ?? Program.File.FullName;
        var identifier = string.IsNullOrEmpty(requestedFile)
            ? defaultSource
            : Program.SourceDocuments.Keys.FirstOrDefault(candidate => SourceIdentifierMatches(candidate, requestedFile));
        if (identifier is null)
            throw new InvalidOperationException($"Source document '{requestedFile}' is not part of the loaded program.");

        var addresses = Program.Rom
            .Where(entry => entry.Source?.Line == line && SourceIdentifiersEqual(entry.Source.Identifier, identifier))
            .Select(entry => entry.Address)
            .Distinct()
            .Order()
            .ToArray();
        if (addresses.Length == 0)
            throw new InvalidOperationException($"{FormatSourceName(identifier)}:{line} does not emit a ROM instruction.");
        return addresses;
    }

    /// <summary>Returns whether an expression uses the source-location syntax.</summary>
    public static bool IsSourceLocation(string text)
    {
        var separator = text.LastIndexOf(':');
        return separator >= 0 && int.TryParse(text.AsSpan(separator + 1), out _);
    }

    /// <summary>Adds a memory expression to the watch list.</summary>
    public Watch AddWatch(string expression)
    {
        var address = ResolveAddress(expression);
        if (!Addressing.TryTranslateVirtualAddress(address, Addressing.AddressView.PhysicalOffset, out _, out _))
            throw new ArgumentOutOfRangeException(nameof(expression), "A watch must target ROM, RAM or MMIO.");
        var existing = Watches.FirstOrDefault(watch => watch.Address == address);
        if (existing is not null) return existing;
        var watch = new Watch(Watches.Count == 0 ? 1 : Watches.Max(item => item.Id) + 1, address, expression);
        Watches.Add(watch);
        return watch;
    }

    /// <summary>Writes a debugger value to ROM, RAM or a connected MMIO device.</summary>
    public void WriteMemory(uint address, ushort value)
    {
        if (!Addressing.TryTranslateVirtualAddress(address, Addressing.AddressView.PhysicalOffset,
                out var offset, out var region))
            throw new ArgumentOutOfRangeException(nameof(address), "Address is outside the S-CPU memory map.");

        switch (region)
        {
            case Addressing.MemoryRegion.Rom:
                Cpu.ROM[offset] = value;
                break;
            case Addressing.MemoryRegion.Ram:
                Cpu.RAM[offset] = value;
                break;
            case Addressing.MemoryRegion.Mmio:
                var deviceId = (DeviceId)((offset >> 8) & 7);
                if (!Cpu.Devices.TryGetValue(deviceId, out var device))
                    throw new InvalidOperationException($"MMIO device {(int)deviceId} is not connected.");
                device[(byte)(offset & 0xFF)] = value;
                break;
        }
    }

    /// <summary>Reads ROM, RAM or MMIO without consuming device input.</summary>
    public ushort ReadMemory(uint address)
    {
        if (!Addressing.TryTranslateVirtualAddress(address, Addressing.AddressView.PhysicalOffset,
                out var offset, out var region))
            throw new ArgumentOutOfRangeException(nameof(address), "Address is outside the S-CPU memory map.");

        return region switch
        {
            Addressing.MemoryRegion.Rom => Cpu.ROM[offset],
            Addressing.MemoryRegion.Ram => Cpu.RAM[offset],
            Addressing.MemoryRegion.Mmio when Cpu.Devices.TryGetValue((DeviceId)((offset >> 8) & 7), out var device)
                => device.Peek((byte)(offset & 0xFF)),
            Addressing.MemoryRegion.Mmio => 0,
            _ => 0
        };
    }

    /// <summary>Builds a logical stack snapshot using the S-CPU SP and FP conventions.</summary>
    public StackSnapshot GetStack(int? maximumEntries = null)
    {
        var storedStackPointer = Cpu.LookupValue(ReservedAddresses.StackPointer);
        var storedFramePointer = Cpu.LookupValue(ReservedAddresses.FramePointer);
        var stackPointer = storedStackPointer == 0
            ? MemoryMap.Stack.EndInclusive
            : MemoryMap.VirtualAddressBias + storedStackPointer;
        uint? framePointer = storedFramePointer == 0
            ? null
            : MemoryMap.VirtualAddressBias + storedFramePointer;
        var entries = new List<StackEntry>();

        if (MemoryMap.Stack.Contains(stackPointer))
            for (var address = stackPointer + 1;
                 address <= MemoryMap.Stack.EndInclusive && entries.Count < (maximumEntries ?? int.MaxValue);
                 address++)
                entries.Add(new StackEntry(address, Cpu.LookupValue(address), address == framePointer));

        return new StackSnapshot(stackPointer, framePointer, storedStackPointer != 0, entries);
    }

    public bool IsAtHalt => Program is not null && Cpu.ShouldFetchIR && Program.HaltAddresses.Contains(Cpu.ProgramCounter);

    private ExecutionResult Result(StopReason reason, long before)
    {
        LastStopReason = reason;
        State = reason switch
        {
            StopReason.Halt => SimulatorState.Halted,
            StopReason.Breakpoint => SimulatorState.Breakpoint,
            StopReason.Faulted => SimulatorState.Faulted,
            _ => SimulatorState.Paused
        };
        return new(reason, CycleCount - before, Cpu.ProgramCounter);
    }

    /// <summary>Creates an immutable, side-effect-free CPU snapshot.</summary>
    public CpuSnapshot Snapshot(double actualFrequency = 0, double actualInstructionsPerSecond = 0,
        TimeSpan executionTime = default) => new(
        Cpu.AccumulatorRegister, Cpu.InstructionRegister, Cpu.ProgramCounter,
        Cpu.CarryFlag, Cpu.IndirectedFlag, Cpu.StepCounter, Cpu.CurrentInstruction,
        Cpu.CurrentAddressingMode, Cpu.CurrentInstructionOperand, Cpu.PeekDataBus,
        Cpu.ALUOperand,
        Cpu.ROM[Cpu.ProgramCounter],
        Cpu.IsROMEnable ? $"ROM[0x{Cpu.ROMAddress:X4}]" :
        Cpu.IsRAMEnable ? $"RAM[0x{Cpu.CurrentInstructionOperand:X3}]" :
        Cpu.IsIOEnable ? $"MMIO device {(int)Cpu.TargetDevice}, register 0x{Cpu.CurrentInstructionOperand & 0xFF:X2}" :
        Cpu.CurrentAddressingMode == AddressingMode.Immediate ? "Immediate operand" : "None",
        CycleCount, InstructionCount, State, actualFrequency,
        actualInstructionsPerSecond, executionTime, LastStopReason, Fault);

    private static bool PathsReferToSameFile(string left, string right) => string.Equals(
        Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool SourceIdentifierMatches(string identifier, string requested) =>
        SourceIdentifiersEqual(identifier, requested) ||
        string.Equals(Path.GetFileName(identifier), requested, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetFileNameWithoutExtension(identifier), requested, StringComparison.OrdinalIgnoreCase);

    private static bool SourceIdentifiersEqual(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return true;
        try { return PathsReferToSameFile(left, right); }
        catch { return false; }
    }

    private static string FormatSourceName(string identifier) =>
        string.IsNullOrEmpty(Path.GetFileName(identifier)) ? identifier : Path.GetFileName(identifier);
}
