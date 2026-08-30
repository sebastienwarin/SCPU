using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SCPU.Assembler.Exporters;
using SCPU.Architecture;
using SCPU.Simulator.Debugger;
using SCPU.Simulator.Desktop.Infrastructure;
using SCPU.Simulator.Devices;

namespace SCPU.Simulator.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly DebugSession _session;
    private readonly SimulationRunner _runner;
    private readonly ProgramExporter _exporter;
    private readonly IDesktopFilePicker _filePicker;
    private readonly LaunchOptions _launchOptions;
    private readonly BufferedTerminalDevice _terminal;
    private readonly LedPanelDevice _ledPanel;
    private readonly UiLogStore _logStore;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly DesktopSettingsStore _settingsStore;
    private readonly Dictionary<ushort, RomRowViewModel> _romByAddress = [];
    private readonly List<MemoryRowViewModel> _ramRows = [];
    private bool _disposed;
    private bool _initialized;
    private SimulatorState? _lastSnapshotState;

    [ObservableProperty] private string _fileName = "No program loaded";
    [ObservableProperty] private string _state = "Ready";
    [ObservableProperty] private string _runPauseLabel = "Run (F5)";
    [ObservableProperty] private string _accumulator = "0x0000";
    [ObservableProperty] private string _instructionRegister = "0x0000";
    [ObservableProperty] private string _programCounter = "0x0000";
    [ObservableProperty] private string _flags = "C=0  I=0  S0";
    [ObservableProperty] private string _currentInstruction = "—";
    [ObservableProperty] private string _nextInstruction = "—";
    [ObservableProperty] private string _status = "Ready";
    [ObservableProperty] private string _dataBus = "0x0000";
    [ObservableProperty] private string _aluOperand = "0x0000";
    [ObservableProperty] private string _addressingDetails = "—";
    [ObservableProperty] private string _dataSource = "—";
    [ObservableProperty] private string _instructionRate = "0 instr/s";
    [ObservableProperty] private string _terminalOutput = string.Empty;
    [ObservableProperty] private string _display1 = "0x0000";
    [ObservableProperty] private string _ledValue = "0x0000";
    [ObservableProperty] private ushort _display1Value;
    [ObservableProperty] private bool _showUsedMemoryOnly = true;
    [ObservableProperty] private string _stackStatus = "Stack is empty";
    [ObservableProperty] private bool _followProgramCounter = true;
    [ObservableProperty] private bool _stopOnHalt = true;
    [ObservableProperty] private RomRowViewModel? _currentRomRow;
    [ObservableProperty] private RomRowViewModel? _selectedRomRow;
    [ObservableProperty] private SourceRowViewModel? _selectedSourceRow;
    [ObservableProperty] private SourceCodeLineViewModel? _selectedAssemblySourceLine;
    [ObservableProperty] private MemoryRowViewModel? _selectedRamRow;
    [ObservableProperty] private WatchRowViewModel? _selectedWatch;
    [ObservableProperty] private BreakpointRowViewModel? _selectedBreakpoint;
    [ObservableProperty] private SpeedOption? _selectedSpeed;
    [ObservableProperty] private bool _isCpuPanelExpanded;
    [ObservableProperty] private bool _isBottomPanelExpanded;
    [ObservableProperty] private bool _hasSourceDocument;
    [ObservableProperty] private bool _showOriginalSCode;

    public MainWindowViewModel(
        DebugSession session,
        SimulationRunner runner,
        ProgramExporter exporter,
        IDesktopFilePicker filePicker,
        LaunchOptions launchOptions,
        BufferedTerminalDevice terminal,
        LedPanelDevice ledPanel,
        UiLogStore logStore,
        DesktopSettingsStore settingsStore,
        ILogger<MainWindowViewModel> logger)
    {
        _session = session;
        _runner = runner;
        _exporter = exporter;
        _filePicker = filePicker;
        _launchOptions = launchOptions;
        _terminal = terminal;
        _ledPanel = ledPanel;
        _logStore = logStore;
        _settingsStore = settingsStore;
        _logger = logger;
        var settings = settingsStore.Load();
        _isCpuPanelExpanded = settings.CpuPanelExpanded;
        _isBottomPanelExpanded = settings.BottomPanelExpanded;
        _runner.SnapshotAvailable += OnSnapshotAvailable;
        _terminal.OutputProduced += OnTerminalOutputProduced;
        _ledPanel.RegisterChanged += OnLedRegisterChanged;
        _logStore.EntryAdded += OnLogAdded;

        OpenCommand = new AsyncRelayCommand(OpenAsync);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync, () => _session.LoadedFile is not null);
        RunPauseCommand = new AsyncRelayCommand(RunPauseAsync, () => _session.Program is not null);
        StepCommand = new AsyncRelayCommand(StepAsync, () => _session.Program is not null);
        SourceStepCommand = new AsyncRelayCommand(SourceStepAsync, () => _session.Program is not null);
        ResetCommand = new AsyncRelayCommand(ResetAsync, () => _session.Program is not null);
        RestartCommand = new AsyncRelayCommand(RestartAsync, () => _session.Program is not null);
        StepCycleCommand = new AsyncRelayCommand(StepCycleAsync, () => _session.Program is not null);
        ExportCommand = new AsyncRelayCommand<OutputFormat>(ExportAsync, _ => _session.Program is not null);
        ToggleBreakpointCommand = new RelayCommand(ToggleBreakpoint, () => SelectedRomRow is not null);
        RemoveBreakpointCommand = new RelayCommand(RemoveBreakpoint, () => SelectedBreakpoint is not null);
        ClearBreakpointsCommand = new RelayCommand(ClearBreakpoints, () => _session.Breakpoints.Count > 0);
        AddWatchCommand = new RelayCommand(AddWatch, () => SelectedRamRow is not null);
        RemoveWatchCommand = new RelayCommand(RemoveWatch, () => SelectedWatch is not null);
        ClearWatchesCommand = new RelayCommand(ClearWatches, () => Watches.Count > 0);
        ToggleStopOnHaltCommand = new RelayCommand(() => StopOnHalt = !StopOnHalt);
        ClearTerminalCommand = new RelayCommand(ClearTerminalOutput);
        ToggleCpuPanelCommand = new RelayCommand(() => IsCpuPanelExpanded = !IsCpuPanelExpanded);
        ToggleBottomPanelCommand = new RelayCommand(() => IsBottomPanelExpanded = !IsBottomPanelExpanded);

        Speeds.Add(new SpeedOption("5 Hz", 5));
        Speeds.Add(new SpeedOption("10 Hz", 10));
        Speeds.Add(new SpeedOption("100 Hz", 100));
        Speeds.Add(new SpeedOption("1 kHz", 1_000));
        Speeds.Add(new SpeedOption("10 kHz", 10_000));
        Speeds.Add(new SpeedOption("100 kHz", 100_000));
        Speeds.Add(new SpeedOption("1 MHz", 1_000_000));
        Speeds.Add(new SpeedOption("2 MHz", 2_000_000));
        Speeds.Add(new SpeedOption("4 MHz", 4_000_000));
        Speeds.Add(new SpeedOption("Maximum", 0));
        SelectedSpeed = _launchOptions.Frequency is { } requestedFrequency
            ? Speeds.FirstOrDefault(speed => speed.Frequency == requestedFrequency) ?? new SpeedOption($"{requestedFrequency:N0} Hz", requestedFrequency)
            : Speeds[^3];
        if (!Speeds.Contains(SelectedSpeed)) Speeds.Add(SelectedSpeed);
        FollowProgramCounter = _launchOptions.FollowProgramCounter ?? true;
    }

    public ObservableCollection<RomRowViewModel> Rom { get; } = [];
    public ObservableCollection<MemoryRowViewModel> Ram { get; } = [];
    public ObservableCollection<MemoryRowViewModel> Stack { get; } = [];
    public ObservableCollection<WatchRowViewModel> Watches { get; } = [];
    public ObservableCollection<BreakpointRowViewModel> Breakpoints { get; } = [];
    public ObservableCollection<SymbolRowViewModel> Symbols { get; } = [];
    public ObservableCollection<SourceRowViewModel> Source { get; } = [];
    public ObservableCollection<SourceCodeLineViewModel> AssemblySourceLines { get; } = [];
    public ObservableCollection<RomRowViewModel> SelectedSourceInstructions { get; } = [];
    public ObservableCollection<UiLogEntry> Logs { get; } = [];
    public ObservableCollection<SpeedOption> Speeds { get; } = [];
    public ObservableCollection<LedIndicatorViewModel> LedIndicators { get; } = [];
    public IAsyncRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IAsyncRelayCommand RunPauseCommand { get; }
    public IAsyncRelayCommand StepCommand { get; }
    public IAsyncRelayCommand SourceStepCommand { get; }
    public IAsyncRelayCommand ResetCommand { get; }
    public IAsyncRelayCommand RestartCommand { get; }
    public IAsyncRelayCommand StepCycleCommand { get; }
    public IAsyncRelayCommand<OutputFormat> ExportCommand { get; }
    public IRelayCommand ToggleBreakpointCommand { get; }
    public IRelayCommand RemoveBreakpointCommand { get; }
    public IRelayCommand ClearBreakpointsCommand { get; }
    public IRelayCommand AddWatchCommand { get; }
    public IRelayCommand RemoveWatchCommand { get; }
    public IRelayCommand ClearWatchesCommand { get; }
    public IRelayCommand ToggleStopOnHaltCommand { get; }
    public IRelayCommand ClearTerminalCommand { get; }
    public IRelayCommand ToggleCpuPanelCommand { get; }
    public IRelayCommand ToggleBottomPanelCommand { get; }
    public GridLength CpuPanelWidth => new(IsCpuPanelExpanded ? 310 : 32);
    public double CpuPanelMinWidth => IsCpuPanelExpanded ? 240 : 32;
    public GridLength CpuSplitterWidth => new(5);
    public GridLength BottomPanelHeight => new(IsBottomPanelExpanded ? 260 : 32);
    public double BottomPanelMinHeight => IsBottomPanelExpanded ? 210 : 32;
    public GridLength BottomSplitterHeight => new(5);
    public string AssemblySourceTitle => _session.Program?.Type == ProgramFileType.SCode ? "GENERATED ASSEMBLY" : "ASSEMBLY SOURCE";
    public string SelectedAssemblyBreakpointActionLabel => SelectedAssemblySourceLine?.IsBreakpoint == true
        ? "Remove breakpoint" : "Add breakpoint";
    public bool HasSelectedSourceInstructions => SelectedSourceInstructions.Count > 0;
    public GridLength GeneratedInstructionsHeight => new(HasSelectedSourceInstructions ? 160 : 0);
    public string WindowTitle => FileName == "No program loaded" ? "S-CPU Simulator" : $"{FileName} — S-CPU Simulator";
    public string SelectedRomBreakpointActionLabel => SelectedRomRow?.IsBreakpoint == true ? "Remove breakpoint" : "Add breakpoint";
    public string SelectedSourceBreakpointActionLabel => SelectedSourceRow?.IsBreakpoint == true ? "Remove breakpoint" : "Add breakpoint";
    public string SelectedRamWatchActionLabel => SelectedRamRow?.IsWatched == true ? "Remove watch" : "Add watch";

    public uint ResolveAddress(string text) => _session.ResolveAddress(text);

    public uint ResolveNavigableAddress(string text)
    {
        var address = ResolveAddress(text);
        if ((address <= ushort.MaxValue && _romByAddress.ContainsKey((ushort)address)) ||
            _ramRows.Any(row => row.RawAddress == address))
            return address;
        throw new ArgumentOutOfRangeException(nameof(text), $"Address 0x{address:X} is not present in the loaded ROM or RAM.");
    }

    public uint ResolveWatchAddress(string text)
    {
        var address = ResolveAddress(text);
        _ = _session.ReadMemory(address);
        return address;
    }

    public uint ResolveBreakpointAddress(string text)
    {
        var address = ResolveAddress(text);
        if (address <= ushort.MaxValue && _romByAddress.ContainsKey((ushort)address)) return address;
        throw new ArgumentOutOfRangeException(nameof(text), $"ROM address 0x{address:X} is not loaded.");
    }

    public int SelectAddress(uint address)
    {
        if (address <= ushort.MaxValue && _romByAddress.TryGetValue((ushort)address, out var romRow))
        {
            SelectedRomRow = romRow;
            return 0;
        }

        var ramRow = _ramRows.FirstOrDefault(row => row.RawAddress == address);
        if (ramRow is not null)
        {
            ShowUsedMemoryOnly = false;
            SelectedRamRow = ramRow;
            return 2;
        }

        throw new ArgumentOutOfRangeException(nameof(address), $"Address 0x{address:X} is not present in ROM or RAM.");
    }

    public async Task InitializeAsync()
    {
        if (_launchOptions.FilePath is { } path)
            await LoadAsync(path);
        else
            ApplySnapshot(_session.Snapshot());

        foreach (var entry in _logStore.Snapshot()) Logs.Add(entry);
        _initialized = true;
    }

    private async Task OpenAsync()
    {
        var path = await _filePicker.PickProgramAsync();
        if (path is not null) await LoadAsync(path);
    }

    private async Task ReloadAsync()
    {
        await _runner.PauseAsync();
        await _session.ReloadAsync();
        RefreshProgram();
        _logger.LogInformation("Reloaded {Program}", _session.LoadedFile?.FullName);
    }

    private async Task LoadAsync(string path)
    {
        try
        {
            await _runner.PauseAsync();
            Status = $"Loading {Path.GetFileName(path)}…";
            await _session.LoadAsync(new FileInfo(path));
            RefreshProgram();
            _logger.LogInformation("Loaded {Program}", path);
        }
        catch (Exception exception)
        {
            Status = exception.Message;
            _logger.LogError(exception, "Unable to load {Program}", path);
        }
    }

    private async Task RunPauseAsync()
    {
        if (_runner.IsRunning)
        {
            await _runner.PauseAsync();
            _logger.LogInformation("Simulation paused at PC 0x{ProgramCounter:X4}", _session.Cpu.ProgramCounter);
        }
        else
        {
            await _runner.RunAsync();
            _logger.LogInformation("Simulation started at {Frequency}", SelectedSpeed?.Label ?? "unknown speed");
        }
    }

    private async Task StepAsync()
    {
        await _runner.StepInstructionAsync();
        _logger.LogInformation("Stepped one instruction to PC 0x{ProgramCounter:X4}", _session.Cpu.ProgramCounter);
    }

    private async Task SourceStepAsync()
    {
        await _runner.StepSourceAsync();
        _logger.LogInformation("Stepped to the next source line at PC 0x{ProgramCounter:X4}", _session.Cpu.ProgramCounter);
    }

    private async Task ResetAsync()
    {
        await _runner.ResetAsync();
        RefreshDeviceViews();
        _logger.LogInformation("CPU reset");
    }

    private async Task RestartAsync()
    {
        await _runner.RestartAsync();
        RefreshDeviceViews();
        _logger.LogInformation("CPU restarted");
    }

    private async Task StepCycleAsync()
    {
        await _runner.StepCycleAsync();
        _logger.LogInformation("Stepped one hardware cycle to {Step} at PC 0x{ProgramCounter:X4}",
            _session.Cpu.StepCounter, _session.Cpu.ProgramCounter);
    }

    private async Task ExportAsync(OutputFormat format)
    {
        if (_session.Program is not { } program) return;
        var extension = format switch
        {
            OutputFormat.Binary => "bin",
            OutputFormat.IntelHex => "hex",
            OutputFormat.Logisim16 => "hex",
            OutputFormat.Verilog => "mem",
            OutputFormat.Gowin => "mi",
            OutputFormat.Symbol => "sym",
            _ => "txt"
        };
        var suggestedName = $"{Path.GetFileNameWithoutExtension(program.File.Name)}.{extension}";
        var path = await _filePicker.PickExportPathAsync(suggestedName, format.ToString(), extension);
        if (path is null) return;

        try
        {
            await _exporter.ExportAsync(program, new FileInfo(path), format);
            _logger.LogInformation("Exported {Format} to {Path}", format, path);
        }
        catch (Exception exception)
        {
            Status = exception.Message;
            _logger.LogError(exception, "Unable to export {Format} to {Path}", format, path);
        }
    }

    private void RefreshProgram()
    {
        Rom.Clear();
        _romByAddress.Clear();
        CurrentRomRow = null;
        if (_session.Program is { } program)
        {
            FileName = program.File.Name;
            SourceLocation? previousRomSource = null;
            foreach (var entry in program.Rom)
            {
                var isContinuation = entry.Source is not null && previousRomSource is not null &&
                    entry.Source.Line == previousRomSource.Line &&
                    string.Equals(entry.Source.Identifier, previousRomSource.Identifier, StringComparison.OrdinalIgnoreCase);
                var row = new RomRowViewModel(entry, showSource: !isContinuation);
                previousRomSource = entry.Source;
                Rom.Add(row);
                _romByAddress.Add(entry.Address, row);
            }
        }
        BuildToolModels();
        RefreshDeviceViews();
        ApplySnapshot(_session.Snapshot());
        NotifyCommands();
    }

    private void OnSnapshotAvailable(object? sender, CpuSnapshot snapshot) =>
        Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));

    private void ApplySnapshot(CpuSnapshot snapshot)
    {
        if (_lastSnapshotState != snapshot.State)
        {
            var breakpointStopChanged = _lastSnapshotState == SimulatorState.Breakpoint || snapshot.State == SimulatorState.Breakpoint;
            if (snapshot.State == SimulatorState.Halted)
                _logger.LogInformation("Simulation halted at PC 0x{ProgramCounter:X4}", snapshot.ProgramCounter);
            else if (snapshot.State == SimulatorState.Breakpoint)
                _logger.LogInformation("Breakpoint reached at PC 0x{ProgramCounter:X4}", snapshot.ProgramCounter);
            else if (snapshot.State == SimulatorState.Faulted)
                _logger.LogError("Simulation faulted at PC 0x{ProgramCounter:X4}: {Fault}", snapshot.ProgramCounter, snapshot.Fault);
            _lastSnapshotState = snapshot.State;
            if (breakpointStopChanged) RefreshBreakpoints();
        }
        State = snapshot.State.ToString();
        RunPauseLabel = snapshot.State == SimulatorState.Running ? "Pause (F5)" : "Run (F5)";
        Accumulator = $"0x{snapshot.Accumulator:X4}  {snapshot.Accumulator}";
        InstructionRegister = $"0x{snapshot.InstructionRegister:X4}";
        ProgramCounter = $"0x{snapshot.ProgramCounter:X4}";
        Flags = $"C={(snapshot.Carry ? 1 : 0)}  I={(snapshot.Indirected ? 1 : 0)}  {snapshot.Step}";
        CurrentInstruction = $"{snapshot.Instruction}  {snapshot.AddressingMode}  0x{snapshot.Operand:X3}";
        NextInstruction = InstructionFormatter.Format(snapshot.NextInstruction);
        DataBus = $"0x{snapshot.DataBus:X4}  {snapshot.DataBus}";
        AluOperand = $"0x{snapshot.AluOperand:X4}  {snapshot.AluOperand}";
        AddressingDetails = $"{snapshot.AddressingMode} · operand 0x{snapshot.Operand:X3}";
        DataSource = snapshot.DataSource;
        InstructionRate = $"{snapshot.ActualInstructionsPerSecond:N0} instr/s";
        var target = SelectedSpeed?.Label ?? "—";
        Status = $"{snapshot.State}  ·  PC 0x{snapshot.ProgramCounter:X4}  ·  {snapshot.Cycles:N0} cycles  ·  {FormatExecutionTime(snapshot.ExecutionTime)}  ·  {target} → {snapshot.ActualFrequency:N0} Hz  ·  {InstructionRate}";
        if (CurrentRomRow?.AddressValue != snapshot.ProgramCounter)
        {
            if (CurrentRomRow is not null) CurrentRomRow.IsCurrent = false;
            _romByAddress.TryGetValue(snapshot.ProgramCounter, out var current);
            CurrentRomRow = current;
            if (CurrentRomRow is not null) CurrentRomRow.IsCurrent = true;
        }
        foreach (var sourceRow in Source)
            sourceRow.IsCurrent = CurrentRomRow is not null &&
                sourceRow.SourceIdentifier == CurrentRomRow.SourcePath && sourceRow.SourceLine == CurrentRomRow.SourceLine;
        foreach (var sourceLine in AssemblySourceLines)
            sourceLine.IsCurrent = sourceLine.Addresses.Contains(snapshot.ProgramCounter);
        // The compact RAM view is cheap enough to sample at the UI refresh rate.
        // The complete 2K view remains pause/step only to avoid needless redraws.
        if (snapshot.State != SimulatorState.Running || ShowUsedMemoryOnly) RefreshMemoryTools();
    }

    private void OnTerminalOutputProduced(object? sender, char character) =>
        Dispatcher.UIThread.Post(() => TerminalOutput = _terminal.Output);

    private void OnLedRegisterChanged(object? sender, DeviceRegisterChangedEventArgs eventArgs) =>
        Dispatcher.UIThread.Post(RefreshLedPanel);

    private void OnLogAdded(object? sender, UiLogEntry entry) => Dispatcher.UIThread.Post(() =>
    {
        Logs.Add(entry);
        while (Logs.Count > _logStore.Capacity) Logs.RemoveAt(0);
    });

    public void SendTerminalInput(string text) => _terminal.Enqueue(text);

    private void ClearTerminalOutput()
    {
        _terminal.ClearOutput();
        TerminalOutput = string.Empty;
        _logger.LogInformation("Terminal output cleared");
    }

    private void RefreshDeviceViews()
    {
        TerminalOutput = _terminal.Output;
        RefreshLedPanel();
    }

    private void BuildToolModels()
    {
        if (_session.Program is not { } program) return;
        var labels = program.Symbols.GroupBy(item => item.Value)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(item => item.Key)));

        _ramRows.Clear();
        for (uint offset = 0; offset < _session.Cpu.RAM.Length; offset++)
        {
            var address = MemoryMap.Ram.Start + offset;
            _ramRows.Add(new MemoryRowViewModel(address, _session.Cpu.RAM[offset], labels.GetValueOrDefault(address, string.Empty)));
        }
        RefreshRamView();

        Symbols.Clear();
        foreach (var symbol in program.Symbols.OrderBy(item => item.Value).ThenBy(item => item.Key))
        {
            var region = MemoryMap.Rom.Contains(symbol.Value) ? "ROM" : MemoryMap.Ram.Contains(symbol.Value) ? "RAM" :
                MemoryMap.Mmio.Contains(symbol.Value) ? "MMIO" : "VALUE";
            Symbols.Add(new SymbolRowViewModel(symbol.Key, symbol.Value, region, ReadSymbolValue(symbol.Value)));
        }

        Source.Clear();
        foreach (var entry in program.Rom.Where(item => item.Source is not null)
                     .GroupBy(item => (item.Source!.Identifier, item.Source.Line)).Select(group => group.First()))
            Source.Add(new SourceRowViewModel(entry.Address, $"{entry.Address:X4}",
                $"{Path.GetFileName(entry.Source!.Identifier)}:{entry.Source.Line}", entry.Source.Content,
                entry.Source.Identifier, entry.Source.Line));

        BuildSourceExplorer(program);

        RefreshBreakpoints();
        RefreshMemoryTools();
    }

    private void RefreshMemoryTools()
    {
        for (var i = 0; i < _ramRows.Count; i++) _ramRows[i].Update(_session.Cpu.RAM[i]);
        if (ShowUsedMemoryOnly) RefreshRamView();
        var stack = _session.GetStack();
        var stackShapeChanged = Stack.Count != stack.Entries.Count || stack.Entries.Where((entry, index) =>
            Stack[index].RawAddress != entry.Address || Stack[index].Label != (entry.IsFramePointer ? "Frame pointer" : string.Empty)).Any();
        if (stackShapeChanged)
        {
            Stack.Clear();
            foreach (var entry in stack.Entries)
                Stack.Add(new MemoryRowViewModel(entry.Address, entry.Value,
                    entry.IsFramePointer ? "Frame pointer" : string.Empty));
        }
        else
        {
            for (var index = 0; index < stack.Entries.Count; index++) Stack[index].Update(stack.Entries[index].Value);
        }
        var framePointerText = stack.FramePointer is { } fp ? $"0x{fp:X5}" : "—";
        var initialStack = stack.IsInitialized ? string.Empty : " (initial)";
        StackStatus = $"SP 0x{stack.StackPointer:X5}{initialStack} · FP {framePointerText} · {Stack.Count} word(s)";
        foreach (var watch in Watches) watch.Update(_session.ReadMemory(watch.RawAddress));
        foreach (var symbol in Symbols) symbol.Update(ReadSymbolValue(symbol.RawAddress));
        foreach (var line in AssemblySourceLines)
            line.SymbolToolTip = line.ReferencedSymbols.Count == 0 ? null : string.Join(Environment.NewLine,
                line.ReferencedSymbols.Select(FormatSymbolToolTip));
    }

    private void RefreshRamView()
    {
        var rows = _ramRows.Where(row => !ShowUsedMemoryOnly || row.Value != 0 || !string.IsNullOrEmpty(row.Label)).ToArray();
        if (Ram.Count == rows.Length && Ram.Select(row => row.RawAddress).SequenceEqual(rows.Select(row => row.RawAddress)))
            return;

        var selectedAddress = SelectedRamRow?.RawAddress;
        Ram.Clear();
        foreach (var row in rows)
            Ram.Add(row);
        SelectedRamRow = selectedAddress is null ? null : Ram.FirstOrDefault(row => row.RawAddress == selectedAddress);
    }

    private void BuildSourceExplorer(ProgramImage program)
    {
        AssemblySourceLines.Clear();
        SelectedSourceInstructions.Clear();
        SelectedAssemblySourceLine = null;
        HasSourceDocument = program.Type != ProgramFileType.Binary && program.OriginalSourceText is not null;
        ShowOriginalSCode = false;
        if (!HasSourceDocument) return;

        var assemblyText = program.Type == ProgramFileType.SCode
            ? program.GeneratedAssemblyText ?? string.Empty
            : program.OriginalSourceText!;
        var assemblyIdentifier = program.Type == ProgramFileType.SCode
            ? program.GeneratedAssemblyIdentifier ?? string.Empty
            : program.File.FullName;
        AddSourceLines(AssemblySourceLines, assemblyText, assemblyIdentifier, program, includeMappings: true,
            markIncludes: program.SourceDocuments.Count > 1);

        foreach (var document in program.SourceDocuments
                     .Where(document => !SourceIdentifiersEqual(document.Key, assemblyIdentifier))
                     .OrderBy(document => program.Rom.Where(entry => entry.Source is not null &&
                             SourceIdentifiersEqual(entry.Source.Identifier, document.Key))
                         .Select(entry => (int)entry.Address).DefaultIfEmpty(int.MaxValue).Min()))
        {
            AssemblySourceLines.Add(new SourceCodeLineViewModel(0,
                $"; ===== Included source: {Path.GetFileName(document.Key)} =====", document.Key, [], []));
            AddSourceLines(AssemblySourceLines, document.Value, document.Key, program, includeMappings: true,
                markIncludes: true);
        }
    }

    private void AddSourceLines(ObservableCollection<SourceCodeLineViewModel> target, string text,
        string identifier, ProgramImage program, bool includeMappings, bool markIncludes = false)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var lineNumber = index + 1;
            var addresses = includeMappings
                ? program.Rom.Where(entry => entry.Source?.Line == lineNumber &&
                    SourceIdentifiersEqual(entry.Source.Identifier, identifier)).Select(entry => entry.Address).ToArray()
                : [];
            var displayedLine = markIncludes && lines[index].TrimStart().StartsWith("#include", StringComparison.OrdinalIgnoreCase)
                ? $"; {lines[index].Trim()}  (expanded below)"
                : lines[index];
            var referencedSymbols = includeMappings
                ? program.Symbols.Where(symbol =>
                        (MemoryMap.Ram.Contains(symbol.Value) || MemoryMap.Mmio.Contains(symbol.Value)) &&
                        ContainsSymbolReference(lines[index], symbol.Key))
                    .OrderByDescending(symbol => symbol.Key.Length).ToArray()
                : [];
            target.Add(new SourceCodeLineViewModel(lineNumber, displayedLine, identifier, addresses, referencedSymbols));
        }
    }

    private static bool SourceIdentifiersEqual(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return true;
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static bool ContainsSymbolReference(string line, string symbol)
    {
        for (var start = 0; (start = line.IndexOf(symbol, start, StringComparison.OrdinalIgnoreCase)) >= 0; start++)
        {
            var beforeIsIdentifier = start > 0 && IsAssemblyIdentifierCharacter(line[start - 1]);
            var end = start + symbol.Length;
            var afterIsIdentifier = end < line.Length && IsAssemblyIdentifierCharacter(line[end]);
            if (!beforeIsIdentifier && !afterIsIdentifier) return true;
        }
        return false;
    }

    private static bool IsAssemblyIdentifierCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '.' or '$';

    private ushort ReadSymbolValue(uint address)
    {
        try { return _session.ReadMemory(address); }
        catch (ArgumentOutOfRangeException) { return unchecked((ushort)address); }
    }

    private string FormatSymbolToolTip(KeyValuePair<string, uint> symbol)
    {
        try
        {
            var value = _session.ReadMemory(symbol.Value);
            return $"{symbol.Key} · 0x{symbol.Value:X5} · 0x{value:X4} ({value})";
        }
        catch (InvalidOperationException)
        {
            return $"{symbol.Key} · 0x{symbol.Value:X5} · value unavailable";
        }
    }

    private static string FormatExecutionTime(TimeSpan elapsed) => elapsed.TotalHours >= 1
        ? elapsed.ToString(@"hh\:mm\:ss\.fff")
        : elapsed.ToString(@"mm\:ss\.fff");

    private void RefreshLedPanel()
    {
        Display1 = $"0x{_ledPanel.Display1:X4}  {_ledPanel.Display1}";
        Display1Value = _ledPanel.Display1;
        var ledByte = (byte)_ledPanel.Leds;
        LedValue = $"0x{ledByte:X2}";
        LedIndicators.Clear();
        for (var bit = 7; bit >= 0; bit--)
        {
            var isOn = (ledByte & (1 << bit)) != 0;
            LedIndicators.Add(new LedIndicatorViewModel(bit, isOn, isOn ? $"Bit {bit}: ON" : $"Bit {bit}: OFF"));
        }
    }

    private void ToggleBreakpoint()
    {
        if (SelectedRomRow is null) return;
        ToggleRomBreakpoint(SelectedRomRow);
    }

    public void ToggleRomBreakpoint(RomRowViewModel row)
    {
        if (!_session.Breakpoints.Add(row.AddressValue)) _session.Breakpoints.Remove(row.AddressValue);
        _logger.LogInformation("Breakpoint toggled at ROM address 0x{Address:X4}", row.AddressValue);
        RefreshBreakpoints();
    }

    private void RemoveBreakpoint()
    {
        if (SelectedBreakpoint is null) return;
        _session.Breakpoints.Remove(SelectedBreakpoint.AddressValue);
        RefreshBreakpoints();
    }

    private void ClearBreakpoints()
    {
        _session.Breakpoints.Clear();
        RefreshBreakpoints();
    }

    private void RefreshBreakpoints()
    {
        foreach (var romRow in Rom) romRow.IsBreakpoint = _session.Breakpoints.Contains(romRow.AddressValue);
        foreach (var sourceRow in Source)
            sourceRow.IsBreakpoint = _session.Program?.Rom.Any(entry => _session.Breakpoints.Contains(entry.Address) &&
                entry.Source?.Identifier == sourceRow.SourceIdentifier && entry.Source.Line == sourceRow.SourceLine) == true;
        foreach (var sourceLine in AssemblySourceLines)
            sourceLine.IsBreakpoint = sourceLine.Addresses.Any(_session.Breakpoints.Contains);
        Breakpoints.Clear();
        foreach (var address in _session.Breakpoints.Order())
        {
            var row = _session.Program?.Rom.FirstOrDefault(item => item.Address == address);
            Breakpoints.Add(new BreakpointRowViewModel(address, $"{address:X4}", row?.Instruction ?? string.Empty,
                row?.Label ?? string.Empty, _session.State == SimulatorState.Breakpoint && _session.Cpu.ProgramCounter == address));
        }
        ClearBreakpointsCommand.NotifyCanExecuteChanged();
        RemoveBreakpointCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedRomBreakpointActionLabel));
        OnPropertyChanged(nameof(SelectedSourceBreakpointActionLabel));
        OnPropertyChanged(nameof(SelectedAssemblyBreakpointActionLabel));
    }

    private void AddWatch()
    {
        if (SelectedRamRow is null || Watches.Any(item => item.RawAddress == SelectedRamRow.RawAddress)) return;
        Watches.Add(new WatchRowViewModel(SelectedRamRow.RawAddress, SelectedRamRow.Label));
        RefreshMemoryTools();
        ClearWatchesCommand.NotifyCanExecuteChanged();
        RefreshWatchMarkers();
    }

    public void AddWatch(uint address)
    {
        _ = _session.ReadMemory(address);
        if (Watches.Any(item => item.RawAddress == address)) return;
        var label = _session.Symbols.FirstOrDefault(symbol => symbol.Value == address).Key ?? string.Empty;
        Watches.Add(new WatchRowViewModel(address, label));
        RefreshMemoryTools();
        ClearWatchesCommand.NotifyCanExecuteChanged();
        RefreshWatchMarkers();
        _logger.LogInformation("Watch added at 0x{Address:X5}", address);
    }

    public void AddBreakpoint(uint address)
    {
        if (address > ushort.MaxValue || !_romByAddress.ContainsKey((ushort)address))
            throw new ArgumentOutOfRangeException(nameof(address), $"ROM address 0x{address:X} is not loaded.");
        _session.Breakpoints.Add((ushort)address);
        RefreshBreakpoints();
        _logger.LogInformation("Breakpoint added at ROM address 0x{Address:X4}", address);
    }

    private void RemoveWatch()
    {
        if (SelectedWatch is not null) Watches.Remove(SelectedWatch);
        ClearWatchesCommand.NotifyCanExecuteChanged();
        RefreshWatchMarkers();
    }

    private void ClearWatches()
    {
        Watches.Clear();
        ClearWatchesCommand.NotifyCanExecuteChanged();
        RefreshWatchMarkers();
        _logger.LogInformation("All watches removed");
    }

    public void ToggleSelectedRamWatch()
    {
        if (SelectedRamRow is null) return;
        var existing = Watches.FirstOrDefault(watch => watch.RawAddress == SelectedRamRow.RawAddress);
        if (existing is null) AddWatch(SelectedRamRow.RawAddress);
        else Watches.Remove(existing);
        ClearWatchesCommand.NotifyCanExecuteChanged();
        RefreshWatchMarkers();
    }

    public void ToggleSourceBreakpoint(SourceRowViewModel source)
    {
        var addresses = _session.Program?.Rom.Where(entry => entry.Source?.Identifier == source.SourceIdentifier &&
            entry.Source.Line == source.SourceLine).Select(entry => entry.Address).ToArray() ?? [];
        if (addresses.Any(_session.Breakpoints.Contains))
            foreach (var address in addresses) _session.Breakpoints.Remove(address);
        else if (addresses.Length > 0)
            _session.Breakpoints.Add(addresses[0]);
        RefreshBreakpoints();
    }

    public void ToggleAssemblySourceBreakpoint(SourceCodeLineViewModel source)
    {
        if (source.Addresses.Any(_session.Breakpoints.Contains))
            foreach (var address in source.Addresses) _session.Breakpoints.Remove(address);
        else if (source.Addresses.Count > 0)
            _session.Breakpoints.Add(source.Addresses[0]);
        RefreshBreakpoints();
    }

    public void AddSourceWatches(SourceCodeLineViewModel source)
    {
        foreach (var symbol in source.ReferencedSymbols)
            AddWatch(symbol.Value);
    }

    public void CloseSourceInstructions() => SelectedAssemblySourceLine = null;

    private void RefreshWatchMarkers()
    {
        foreach (var row in _ramRows) row.IsWatched = Watches.Any(watch => watch.RawAddress == row.RawAddress);
        OnPropertyChanged(nameof(SelectedRamWatchActionLabel));
    }

    private void NotifyCommands()
    {
        ReloadCommand.NotifyCanExecuteChanged();
        RunPauseCommand.NotifyCanExecuteChanged();
        StepCommand.NotifyCanExecuteChanged();
        SourceStepCommand.NotifyCanExecuteChanged();
        ResetCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
        StepCycleCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
        AddWatchCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _runner.SnapshotAvailable -= OnSnapshotAvailable;
        _terminal.OutputProduced -= OnTerminalOutputProduced;
        _ledPanel.RegisterChanged -= OnLedRegisterChanged;
        _logStore.EntryAdded -= OnLogAdded;
    }

    partial void OnSelectedSpeedChanged(SpeedOption? value)
    {
        if (value is null) return;
        _runner.TargetFrequency = value.Frequency;
        if (_initialized) _logger.LogInformation("Simulation frequency changed to {Frequency}", value.Label);
    }

    partial void OnSelectedRomRowChanged(RomRowViewModel? value)
    {
        ToggleBreakpointCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedRomBreakpointActionLabel));
    }
    partial void OnSelectedSourceRowChanged(SourceRowViewModel? value) => OnPropertyChanged(nameof(SelectedSourceBreakpointActionLabel));
    partial void OnSelectedAssemblySourceLineChanged(SourceCodeLineViewModel? value)
    {
        SelectedSourceInstructions.Clear();
        if (value is not null)
            foreach (var address in value.Addresses)
                if (_romByAddress.TryGetValue(address, out var row)) SelectedSourceInstructions.Add(row);
        OnPropertyChanged(nameof(SelectedAssemblyBreakpointActionLabel));
        OnPropertyChanged(nameof(HasSelectedSourceInstructions));
        OnPropertyChanged(nameof(GeneratedInstructionsHeight));
    }
    partial void OnSelectedRamRowChanged(MemoryRowViewModel? value)
    {
        AddWatchCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedRamWatchActionLabel));
    }
    partial void OnSelectedWatchChanged(WatchRowViewModel? value) => RemoveWatchCommand.NotifyCanExecuteChanged();
    partial void OnSelectedBreakpointChanged(BreakpointRowViewModel? value) => RemoveBreakpointCommand.NotifyCanExecuteChanged();
    partial void OnShowUsedMemoryOnlyChanged(bool value) => RefreshRamView();
    partial void OnFileNameChanged(string value) => OnPropertyChanged(nameof(WindowTitle));
    partial void OnShowOriginalSCodeChanged(bool value)
    {
        OnPropertyChanged(nameof(AssemblySourceTitle));
    }
    partial void OnStopOnHaltChanged(bool value)
    {
        _runner.StopOnHalt = value;
        if (_initialized) _logger.LogInformation("Stop on HALT {State}", value ? "enabled" : "disabled");
    }

    partial void OnIsCpuPanelExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(CpuPanelWidth));
        OnPropertyChanged(nameof(CpuPanelMinWidth));
        OnPropertyChanged(nameof(CpuSplitterWidth));
        SavePanelSettings();
    }

    partial void OnIsBottomPanelExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(BottomPanelHeight));
        OnPropertyChanged(nameof(BottomPanelMinHeight));
        OnPropertyChanged(nameof(BottomSplitterHeight));
        SavePanelSettings();
    }

    private void SavePanelSettings() =>
        _settingsStore.Save(new DesktopSettings(IsCpuPanelExpanded, IsBottomPanelExpanded));

}

public sealed partial class RomRowViewModel : ObservableObject
{
    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isBreakpoint;
    [ObservableProperty] private string _currentMarker = string.Empty;

    public RomRowViewModel(RomEntry entry, bool showSource = true)
    {
        AddressValue = entry.Address;
        Address = $"{entry.Address:X4}";
        Word = $"{entry.Value:X4}";
        IsData = entry.IsData;
        Instruction = entry.Instruction;
        Label = entry.Label;
        SourceContent = showSource ? (entry.Source?.Content ?? string.Empty) : string.Empty;
        SourceLocation = showSource && entry.Source is not null ? $"{Path.GetFileName(entry.Source.Identifier)}:{entry.Source.Line}" : string.Empty;
        SourcePath = entry.Source?.Identifier ?? string.Empty;
        SourceLine = entry.Source?.Line ?? 0;
    }

    public ushort AddressValue { get; }
    public string Address { get; }
    public string Word { get; }
    public bool IsData { get; }
    public string Instruction { get; }
    public string Label { get; }
    public string SourceContent { get; }
    public string SourceLocation { get; }
    public string SourcePath { get; }
    public int SourceLine { get; }
    public bool ShowBreakpointDot => IsBreakpoint && !IsCurrent;
    public bool CanShowBreakpointHint => !IsBreakpoint && !IsCurrent;
    public bool IsCurrentBreakpoint => IsBreakpoint && IsCurrent;
    public string BreakpointActionLabel => IsBreakpoint ? "Remove breakpoint" : "Add breakpoint";

    partial void OnIsCurrentChanged(bool value)
    {
        RefreshMarker();
        OnPropertyChanged(nameof(ShowBreakpointDot));
        OnPropertyChanged(nameof(CanShowBreakpointHint));
        OnPropertyChanged(nameof(IsCurrentBreakpoint));
    }
    partial void OnIsBreakpointChanged(bool value)
    {
        RefreshMarker();
        OnPropertyChanged(nameof(ShowBreakpointDot));
        OnPropertyChanged(nameof(CanShowBreakpointHint));
        OnPropertyChanged(nameof(IsCurrentBreakpoint));
        OnPropertyChanged(nameof(BreakpointActionLabel));
    }

    private void RefreshMarker() => CurrentMarker = IsCurrent ? "▶" : IsBreakpoint ? "●" : string.Empty;
}
