namespace SCPU.Simulator.ConsoleUI
{
    using SCode.Compiler;
    using SCode.Compiler.Assembler;
    using SCode.Compiler.Exceptions;
    using SCPU.Simulator.ConsoleUI.IODevices;
    using SCPU.Simulator.ConsoleUI.TableSources;
    using SCPU.Simulator.Core;
    using SCPU.Simulator.Core.Enums;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Terminal.Gui;
    using Timer = System.Timers.Timer;

    public partial class MainWindow
    {
        private const int REFRESH_UI_INTERVAL = 50;
        private const int DEFAULT_FREQUENCY = 1_000; // = 1kHz
        private const int MAX_FREQUENCY = 1_000_000; // = 1Mhz
        private const int STEP_LINE_MAX_ITERATIONS = 256;

        private readonly Processor processor;
        private readonly BackgroundWorker backgroundWorker;
        private readonly object syncLock = new();

        private int frequencyHz;
        private TimeSpan cyclePause = TimeSpan.Zero;
        private Stopwatch stopwatch = new();
        private string lastFileLoaded;
        private Shortcut scFrequency, scStatus, scRun;
        private bool hasBreak = false, bypassBreak = false;
        private bool stopOnHalt = true;

        public static MainWindow Current { get; private set; }

        public SortedSet<int> Breakpoints { get; private set; } = [];
        public SortedSet<int> Watchs { get; private set; } = [];

        public bool IsRunning => backgroundWorker.IsBusy && !backgroundWorker.CancellationPending;
        public Processor Processor => processor;
        public List<CodeAnalyzer.LineInfo> ProgramSource { get; private set; } = [];
        public List<CodeAnalyzer.SymbolInfo> Symbols => tableSymbols.Data as List<CodeAnalyzer.SymbolInfo>;
        public HashSet<ushort> HaltAddresses { get; private set; } = [];

        public int FrequencyHz
        {
            get { return frequencyHz; }
            set
            {
                if (value > 0 && value <= MAX_FREQUENCY)
                {
                    frequencyHz = value;
                    cyclePause = TimeSpan.FromMicroseconds(MAX_FREQUENCY / frequencyHz);
                    if (StatusBar != null) RefreshUI();
                }
            }
        }

        public MainWindow()
        {
            // Singleton
            Current = this;

            // Init UI
            InitializeComponent();
            BorderStyle = LineStyle.None;
            frmProgramControl.CanFocus =
                frmInstruction.CanFocus =
                frmDataPath.CanFocus =
                frmDemo.CanFocus = false;

            // Init S-CPU processor
            this.FrequencyHz = DEFAULT_FREQUENCY;
            processor = new Processor();
            processor.Devices.Add(DeviceId.Device0, new DemoDevice(cbDemoLED, txtDemoHex, txtDemoHex2));
            processor.Devices.Add(DeviceId.Device1, new TerminalDevice(textView));

            // Create BackgroundWorker for SCPU's tick
            backgroundWorker = new BackgroundWorker() { WorkerSupportsCancellation = true };
            backgroundWorker.RunWorkerCompleted += (sender, e) =>
            {
                stopwatch.Stop();
                RefreshUI();
            };
            backgroundWorker.DoWork += (sender, e) =>
            {
                DateTime now = DateTime.Now;
                stopwatch.Start();
                while (!backgroundWorker.CancellationPending)
                {
                    if (cyclePause != TimeSpan.Zero && DateTime.Now.Subtract(now) > cyclePause)
                    {
                        lock (syncLock)
                        {
                            if (stopOnHalt && processor.StepCounter == Step.S1 && HaltAddresses.Contains(processor.ProgramCounter))
                            {
                                break;
                            }
                            else if (processor.ShouldFetchIR && !bypassBreak && !hasBreak && Breakpoints.Contains(processor.ProgramCounter))
                            {
                                hasBreak = true;
                                break;
                            }
                            bypassBreak = false;
                            this.processor.Tick();
                        }
                        Thread.SpinWait(1);
                        now = DateTime.Now;
                    }
                }
            };

            // Create timer for refresh UI
            var updateUITimer = new Timer(REFRESH_UI_INTERVAL) { AutoReset = true, Enabled = true };
            updateUITimer.Elapsed += (_, _) =>
            {
                if (IsRunning)
                {
                    Application.Invoke(() =>
                    {
                        lock (syncLock)
                        {
                            this.RefreshUI();
                        }
                    });
                }
            };

            // Build MenuBar
            Add(BuildMenu());

            // Build StatusBar
            Add(BuildStatusBar());

            // Load the file from the command line argument if exists
            this.Loaded += (sender, e) => LoadFile(Program.Options.File);

            // Init the ROM & Breakpoints table views
            tableROM.Table = CreateTableSourceWithCheckBox(tableROM, Breakpoints, new ProcessorRomTableSource(processor));
            tableBreakpoints.Table = CreateTableSourceWithCheckBox(tableBreakpoints, Breakpoints, new BreakpointsTableSource(this));
            tableROM.CellActivated += TableROM_CellActivated;
            tableBreakpoints.CellActivated += TableROM_CellActivated;

            // Init the RAM & Watchs table views
            tableRAM.Table = CreateTableSourceWithCheckBox(tableRAM, Watchs, new ProcessorRamTableSource(processor));
            tableWatchs.Table = CreateTableSourceWithCheckBox(tableWatchs, Watchs, new WatchsTableSource(this));

            // Ensure to refresh UI on tab changed if not running
            tabView.SelectedTabChanged += (s, e) =>
            {
                if (!IsRunning)
                {
                    RefreshUI();
                }
            };

            // Refresh UI on start
            RefreshUI();
        }

        public void RefreshUI()
        {
            // Status bar
            scFrequency.Title = frequencyHz < 1000 ? $"{frequencyHz} Hz" : $"{frequencyHz / 1000} kHz";
            scStatus.Title = IsRunning ? "Running" : hasBreak ? "Break" : stopwatch.Elapsed == TimeSpan.Zero ? "Ready" : HaltAddresses.Contains(processor.ProgramCounter) ? "Halted" : "Pause";
            if (stopwatch.Elapsed > TimeSpan.Zero)
            {
                scStatus.Title += $" {stopwatch.Elapsed}";
            }
            scRun.Title = IsRunning ? "Stop" : hasBreak ? "Continue" : "Run";

            // Program location
            this.txtStep.Text = processor.StepCounter == Step.S0 ? "S0/FETCH" : "S1/EXECUTE";
            this.txtProgramCounter.Text = processor.ProgramCounter.ToString("X4");
            this.txtInstructionReg.Text = processor.InstructionRegister.ToString("X4");
            this.txtAddressMode.Text = processor.CurrentAddressingMode.ToString();
            this.cbCarryFlag.CheckedState = processor.CarryFlag ? CheckState.Checked : CheckState.UnChecked;
            this.cbIndirected.CheckedState = processor.IndirectedFlag ? CheckState.Checked : CheckState.UnChecked;

            // Instructions
            this.txtInstruction.Text = processor.ProgramCounter == 0 && processor.StepCounter == 0 ? "..." : DecodeInstruction(processor.InstructionRegister);
            this.txtNextInstruction.Text = DecodeInstruction(processor.ROM[processor.ProgramCounter]);

            // Data path
            this.txtOperand.Text = processor.CurrentInstructionOperand.ToString("X4");
            this.txtOperand2.Text = processor.CurrentInstructionOperand.ToString();
            this.txtAccumulator.Text = processor.AccumulatorRegister.ToString("X4");
            this.txtAccumulator2.Text = processor.AccumulatorRegister.ToString();
            this.txtALUOperand.Text = processor.ALUOperand.ToString("X4");
            this.txtALUOperand2.Text = processor.ALUOperand.ToString();
            this.txtDataBus.Text = processor.DataBus.ToString("X4");
            this.txtDataBus2.Text = processor.DataBus.ToString();
            this.txtDataSource.Text = processor.IsROMEnable ? $"ROM[0x{processor.ROMAddress.ToString("X2")}]" :
                            processor.IsRAMEnable ? $"RAM[0x{processor.CurrentInstructionOperand.ToString("X2")}]" :
                            processor.IsIOEnable ? $"IO[0x{processor.CurrentInstructionOperand.ToString("X2")}] DevId #{(int)processor.TargetDevice}" :
                            processor.CurrentAddressingMode == AddressingMode.IMM ? "OPERAND (IMM)" :
                            "N/A";

            // Memory (RAM & ROM) tabview
            if (tabView.SelectedTab.DisplayText == "Memory")
            {
                if (processor.IsROMEnable)
                {
                    tableROM.SelectedRow = processor.ROMAddress;
                    tableROM.EnsureSelectedCellIsVisible();
                    tableROM.SetFocus();
                }
                else if (processor.IsRAMEnable)
                {
                    tableRAM.SelectedRow = processor.CurrentInstructionOperand;
                    tableRAM.EnsureSelectedCellIsVisible();
                    tableRAM.SetFocus();
                }
            }

            // Breakpoints tabview
            else if (tabView.SelectedTab.DisplayText == "Breakpoints")
            {
                tableBreakpoints.SelectedRow = Breakpoints.ToList().FindIndex(v => processor.ProgramCounter == v);
                tableBreakpoints.EnsureSelectedCellIsVisible();
            }

            // Source tabview
            else if (tabView.SelectedTab.DisplayText == "Source")
            {
                GotoLineSource(ProgramSource.Where(line => line.Annotation != null && line.Annotation.Address <= processor.ProgramCounter).LastOrDefault());
            }

            // Stack tabview
            else if (tabView.SelectedTab.DisplayText == "Stack")
            {
                var sp = processor.LookupValue(DefaultAddresses.StackPointer);
                var fp = processor.LookupValue(DefaultAddresses.FramePointer);
                if (sp == 0 && processor.ProgramCounter < 0x100)
                {
                    sp = DefaultAddresses.UserPage - 1;
                }
                tableStack.Table = new EnumerableTableSource<Tuple<int, int, ushort>>(
                    processor.RAM
                        .Select((data, address) => new Tuple<int, ushort>(address, data))
                        .Where(tuple => tuple.Item1 > sp - DefaultAddresses.RAM && tuple.Item1 < DefaultAddresses.UserPage - DefaultAddresses.RAM)
                        .Select((tuple, index) => new Tuple<int, int, ushort>(index, tuple.Item1 + DefaultAddresses.RAM, tuple.Item2)),
                    new Dictionary<string, Func<Tuple<int, int, ushort>, object>>
                    {
                        { "Item", p => p.Item1.ToString() },
                        { "Addr", p => "0x" + p.Item2.ToString("X4") },
                        { "Value16", p => "0x" + p.Item3.ToString("X4") },
                        { "Value10", p => p.Item3.ToString() },
                        { "Comment", p => p.Item2 == fp ? "Frame Pointer" : "" },
                    });
                tableStack.SelectedRow = 0;
                tableStack.EnsureSelectedCellIsVisible();
            }
        }

        public bool LoadFile(string file = "")
        {
            // Stop the current simulation
            if (IsRunning)
            {
                backgroundWorker.CancelAsync();
            }

            // Load file dialog
            if (!File.Exists(file))
            {
                var fd = new FileDialog
                {
                    OpenMode = OpenMode.File,
                    MustExist = true,
                    AllowsMultipleSelection = false,
                    AllowedTypes = [
                        new AllowedType("Source Files", Compiler.AsmFileExtension, Compiler.SCodeFileExtension),
                        new AllowedType("ASM Files", Compiler.AsmFileExtension),
                        new AllowedType("SCode Files", Compiler.SCodeFileExtension),
                        new AllowedType("ROM Files", ".bin", ".rom")
                    ]
                };
                if (File.Exists(lastFileLoaded))
                {
                    fd.Path = lastFileLoaded;
                }
                Application.Run(fd);
                fd.Dispose();
                if (fd.Canceled)
                {
                    return false;
                }
                file = fd.Path;
            }
            lastFileLoaded = file;

            // Compile & Assemble
            var fileInfo = new FileInfo(file);
            switch (fileInfo.Extension.ToLower())
            {
                case Compiler.AsmFileExtension:
                    if (!LoadAsmFile(fileInfo, out file))
                    {
                        return false;
                    }
                    break;

                case Compiler.SCodeFileExtension:
                    var asmFile = Path.Combine(AssemblyBuilder.WorkingDirectory, fileInfo.Name + Compiler.AsmFileExtension);
                    var compiler = new Compiler(file, asmFile);
                    try
                    {
                        compiler.Compile();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Query("Error SCode",
                            (ex is NodeCompilerException nodeException) ? $"{nodeException.Node.Source}: {nodeException.Message}" : ex.Message, "OK");
                        return false;
                    }
                    if (!LoadAsmFile(new FileInfo(compiler.OutputFile!.FullName), out file))
                    {
                        return false;
                    }
                    break;

                default:
                    ProgramSource.Clear();
                    tableSource.Table = null;
                    tableSymbols.Table = null;
                    break;
            }

            // Load the ROM
            lock (syncLock)
            {
                // Load ROM & reset the CPU
                processor.LoadROM(file);
                processor.Reset();
                // Reset stopwatch & remove existing breakpoints
                stopwatch.Reset();
                RemoveBreakpoints();
                // Refresh UI
                RefreshUI();
                // Find program ends
                HaltAddresses = new HashSet<ushort>(processor.ROM.Where((data, address) =>
                {
                    // Check instruction and next instruction is the same
                    if (data != 0 && address < processor.ROM.Length - 1 && processor.ROM[address + 1] == data + 1)
                    {
                        // Check the instruction is JCC to itself
                        return ((Instruction)(data >> 14) == Instruction.JCC && (data & 0xFFF) == address);
                    }
                    // Otherwise return false
                    return false;
                }).Select((data, address) => (ushort)((data & 0xFFF) + 1)));
            }
            return true;
        }

        private bool LoadAsmFile(FileInfo asmFileInfo, out string romFile)
        {
            // Prepare temp files
            romFile = Path.Combine(AssemblyBuilder.WorkingDirectory, asmFileInfo.Name + ".bin");
            var annotatedFile = Path.Combine(AssemblyBuilder.WorkingDirectory, asmFileInfo.Name + ".annotated");
            var symbolFile = Path.Combine(AssemblyBuilder.WorkingDirectory, asmFileInfo.Name + ".symbol");

            // Assemble
            try
            {
                AssemblyBuilder.Assemble(asmFileInfo,
                    [
                        new AssemblyOutputFile(romFile),
                        new AssemblyOutputFile(annotatedFile, AssemblyOutputFile.AnnotatedFormat),
                        new AssemblyOutputFile(symbolFile, AssemblyOutputFile.SymbolsFormat),
                    ]);
            }
            catch (AssemblyException ex)
            {
                File.Delete(romFile);
                MessageBox.Query("Error", ex.Message, "OK");
                return false;
            }

            // Analyze code
            ProgramSource = CodeAnalyzer.Analyze(asmFileInfo.FullName, annotatedFile, symbolFile);

            // Build a treeview for source browsing
            TreeView<CodeAnalyzer.LineInfo> tree = new()
            {
                AspectGetter = p => p.Annotation != null ? ("0x" + p.Annotation.Address.ToString("X4")) : "",
                TreeBuilder = new DelegateTreeBuilder<CodeAnalyzer.LineInfo>(GetSourceLineChildren)
            };
            tree.AddObjects(ProgramSource);
            tableSource.Table = new TreeTableSource<CodeAnalyzer.LineInfo>(tableSource, "Addr", tree,
                new()
                {
                        { "Line", p => p.Line }
                });
            tableSource.SelectedRow = 0;
            tableSource.EnsureSelectedCellIsVisible();
            // On cell double-click, go to ROM address
            tableSource.CellActivated += (sender, e) =>
            {
                if (tableSource.Table is TreeTableSource<CodeAnalyzer.LineInfo> treeSource)
                {
                    var line = treeSource.GetObjectOnRow(e.Row);
                    if (line != null && line.Annotation != null)
                    {
                        tabView.SelectedTab = tabView.Tabs.FirstOrDefault(t => t.DisplayText == "Memory");
                        tableROM.SelectedRow = line.Annotation.Address;
                        tableROM.EnsureSelectedCellIsVisible();
                    }
                }
            };

            // Symbols table
            var symbols = CodeAnalyzer.LoadSymbols(symbolFile);
            tableSymbols.Data = symbols;
            tableSymbols.Table = new EnumerableTableSource<CodeAnalyzer.SymbolInfo>(symbols, new()
                    {
                        { "Memory", p => p.Address < DefaultAddresses.RAM ? "ROM" : p.Address < DefaultAddresses.IODevice ? "RAM" : "IO" },
                        { "Bank", p => p.Address < DefaultAddresses.EntryPoint ? "HEADER" : p.Address < DefaultAddresses.RAM ? "PROGRAM" : p.Address < DefaultAddresses.UserPage ? "ZERO_PAGE" : p.Address < DefaultAddresses.ReservedPage ? "USER_PAGE" : p.Address < DefaultAddresses.IODevice ? "RESV_PAGE" : "DEV" + ((p.Address >> 8) & 7) },
                        { "Addr", p => "0x" + p.Address.ToString("X4") },
                        { "Symbol", p => p.Name },
                        { "Value16", p => "0x" + processor.LookupValue(p.Address).ToString("X4") },
                        { "Value10", p => processor.LookupValue(p.Address).ToString() }
                    });
            tableSymbols.SelectedRow = 0;
            tableSymbols.EnsureSelectedCellIsVisible();

            // Done
            return true;
        }

        private IEnumerable<CodeAnalyzer.LineInfo> GetSourceLineChildren(CodeAnalyzer.LineInfo line)
        {
            try
            {
                if (line?.Annotation?.Instructions?.Length > 0 &&
                    !(line.Line.StartsWith(nameof(Instruction.NOR), StringComparison.OrdinalIgnoreCase) ||
                    line.Line.StartsWith(nameof(Instruction.ADD), StringComparison.OrdinalIgnoreCase) ||
                    line.Line.StartsWith(nameof(Instruction.STA), StringComparison.OrdinalIgnoreCase) ||
                    line.Line.StartsWith(nameof(Instruction.JCC), StringComparison.OrdinalIgnoreCase)))
                {
                    return line.Annotation.Instructions.Select((inst, idx) => new CodeAnalyzer.LineInfo
                    {
                        Number = line.Number,
                        Line = DecodeInstruction(inst),
                        Symbols = line.Symbols,
                        RawLine = line.RawLine,
                        Annotation = new CodeAnalyzer.AnnotationInfo()
                        {
                            Address = (ushort)(line.Annotation.Address + idx),
                            Line = line.Annotation.Line
                        }
                    }).AsEnumerable();
                }
                else
                {
                    return Enumerable.Empty<CodeAnalyzer.LineInfo>();
                }
            }
            catch (Exception)
            {
                return Enumerable.Empty<CodeAnalyzer.LineInfo>();
            }
        }

        public bool ExportFile(string format)
        {
            // Save file dialog
            var fileInfo = new FileInfo(lastFileLoaded);
            var sd = new SaveDialog
            {
                Title = "Save file",
                Path = fileInfo.FullName.Replace(fileInfo.Extension, ".rom"),
                AllowedTypes = new()
                {
                    new AllowedType ("ROM Files", ".rom", ".bin", ".txt"), new AllowedTypeAny ()
                }
            };
            Application.Run(sd);
            sd.Dispose();
            if (sd.Canceled)
            {
                return false;
            }

            // Confirm overwrite
            if (File.Exists(sd.Path) && MessageBox.Query("Save File", "File already exists. Overwrite any way?", "No", "Ok") == 0)
            {
                return false;
            }

            // Assemble
            try
            {
                AssemblyBuilder.Assemble(fileInfo, [new AssemblyOutputFile(sd.Path, format)]);
                return true;
            }
            catch (AssemblyException ex)
            {
                MessageBox.Query("Error", ex.Message, "OK");
                return false;
            }
        }

        public void RemoveBreakpoints()
        {
            Breakpoints.Clear();
            hasBreak = bypassBreak = false;
            RefreshUI();
            tableROM.SetNeedsDisplay();
            tableBreakpoints.SetNeedsDisplay();
        }
        public void RemoveWatchs()
        {
            Watchs.Clear();
            RefreshUI();
            tableRAM.SetNeedsDisplay();
            tableWatchs.SetNeedsDisplay();
        }

        public void ToogleSimulation()
        {
            if (!backgroundWorker.IsBusy)
            {
                hasBreak = false;
                bypassBreak = true;
                backgroundWorker.RunWorkerAsync();
            }
            else
            {
                backgroundWorker.CancelAsync();
            }
        }

        public void StepInstruction()
        {
            if (!IsRunning)
            {
                lock (syncLock)
                {
                    hasBreak = false;
                    bypassBreak = true;
                    this.processor.Tick();
                    RefreshUI();
                }
            }
        }

        public void StepLine()
        {
            if (!IsRunning && ProgramSource.Count > 0)
            {
                lock (syncLock)
                {
                    bool tick = false;
                    int iteration = 0;
                    CodeAnalyzer.LineInfo? line = null;
                    CodeAnalyzer.LineInfo? originLine = ProgramSource.FirstOrDefault(l => l.Annotation != null && l.Annotation?.Address == processor.ProgramCounter);
                    while (!tick || processor.StepCounter != Step.S0 || (line = ProgramSource.FirstOrDefault(l => l.Annotation != null && l.Annotation?.Address == processor.ProgramCounter)) == null || (originLine != null && originLine == line))
                    {
                        tick = true;
                        processor.Tick();
                        if (HaltAddresses.Contains(processor.ProgramCounter) || iteration++ == STEP_LINE_MAX_ITERATIONS)
                        {
                            break;
                        }
                    }
                }
                RefreshUI();
            }
        }

        public void Restart()
        {
            this.Reset();
            if (!IsRunning)
            {
                backgroundWorker.RunWorkerAsync();
            }
        }

        public void Reset()
        {
            lock (syncLock)
            {
                stopwatch.Reset();
                hasBreak = bypassBreak = false;
                processor.Reset();
                RefreshUI();
            }
        }

        public static string DecodeInstruction(ushort instructionData)
        {
            var instruction = (Instruction)(instructionData >> 14);
            var addressingMode = AddressingMode.ROM;
            if (((instructionData >> 13) & 1) > 0)
            {
                addressingMode = (AddressingMode)((instructionData >> 11) & 7);
            }
            var operandValue = (ushort)(instructionData & 0x7FF);
            var prefix = addressingMode == AddressingMode.IMM ? "#" : addressingMode == AddressingMode.INDR ? "@" : (addressingMode.ToString() + "[");
            var suffix = prefix.EndsWith("[") ? "]" : "";
            return $"{instruction} {prefix}0x{operandValue.ToString("X2")}{suffix}";
        }

        private void TableROM_CellActivated(object? sender, CellActivatedEventArgs e)
        {
            if (ProgramSource.Count > 0 && e.Table is CheckBoxTableSourceWrapperByObject<MemoryEntry> wrapper && wrapper.Wrapping is IEnumerableTableSource<MemoryEntry> table)
            {
                var entry = table.GetObjectOnRow(e.Row);
                tabView.SelectedTab = tabView.Tabs.FirstOrDefault(t => t.DisplayText == "Source");
                GotoLineSource(ProgramSource.Where(line => line.Annotation != null && line.Annotation.Address == entry.Address).FirstOrDefault());
            }
        }

        private void GotoLineSource(CodeAnalyzer.LineInfo? line)
        {
            if (line != null)
            {
                var tableSource = this.tableSource.Table as IEnumerableTableSource<CodeAnalyzer.LineInfo>;
                var visibleObjects = tableSource.GetAllObjects().ToList();
                var offset = processor.ProgramCounter - line.Annotation.Address;
                var rowId = visibleObjects.FindLastIndex(p => p.Annotation != null && p.Annotation.Address == line.Annotation.Address + offset);
                if (rowId < 0)
                {
                    rowId = visibleObjects.FindLastIndex(p => p.Annotation != null && p.Annotation.Address == line.Annotation.Address);
                }
                this.tableSource.SelectedRow = rowId;
                this.tableSource.EnsureSelectedCellIsVisible();
                if (tabView.SelectedTab.DisplayText == "Source")
                {
                    this.tableSource.SetFocus();
                }
            }
        }

        private StatusBar BuildStatusBar()
        {
            Shortcut scStep, scStepOver, scRestart;
            var statusBar = new StatusBar() { AlignmentModes = AlignmentModes.IgnoreFirstOrLast, CanFocus = false };
            var scQuit = new Shortcut { CanFocus = false, Title = "Quit", Key = Application.QuitKey };
            scStep = new Shortcut { CanFocus = false, Title = "Step Instr.", Key = Key.F9 };
            scStepOver = new Shortcut { CanFocus = false, Title = "Step Line", Key = Key.F10 };
            scRestart = new Shortcut { CanFocus = false, Title = "Restart", Key = Key.F5.WithCtrl.WithShift };
            scRun = new Shortcut { CanFocus = false, Title = "Run", Key = Key.F5 };
            scStatus = new Shortcut { CanFocus = false, Title = "Pause" };
            scFrequency = new Shortcut { CanFocus = false };
            statusBar.Add(scQuit,
                           scRun,
                           scStep,
                           scStepOver,
                           scRestart,
                           scFrequency,
                           scStatus
                          );
            // Attach handlers
            scStep.Accept += (sender, args) =>
            {
                StepInstruction();
                args.Handled = true;
            };
            scStepOver.Accept += (sender, args) =>
            {
                StepLine();
                args.Handled = true;
            };
            scRun.Accept += (sender, args) =>
            {
                ToogleSimulation();
                args.Handled = true;
            };
            scRestart.Accept += (sender, args) =>
            {
                Restart();
                args.Handled = true;
            };
            // Return the status bar
            return statusBar;
        }

        private MenuBar BuildMenu()
        {
            // Export sub-menu
            var exportMenu = new MenuBarItem("_Export", new MenuItem[] {
                                        new ("_Binary", "", () => ExportFile ("binary")),
                                        new ("_Intel HEX", "", () => ExportFile ("intelhex")),
                                        new ("_Logisim", "", () => ExportFile ("logisim16")),
                                    })
            {
                CanExecute = () => lastFileLoaded != null && lastFileLoaded.EndsWith(".asm", StringComparison.InvariantCultureIgnoreCase)
            };
            // CPU Speed sub-menu
            var frequencyMenu = new MenuBarItem("_CPU Speed", new MenuItem[] {
                                        new ("1 Hz", "", () => FrequencyHz = 1),
                                        new ("10 Hz", "", () => FrequencyHz = 10),
                                        new ("100 Hz", "", () => FrequencyHz = 100),
                                        new ("1 kHz", "", () => FrequencyHz = 1_000),
                                        new ("10 kHz", "", () => FrequencyHz = 10_000),
                                        new ("100 kHz", "", () => FrequencyHz = 100_000),
                                        new ("1000 kHz", "", () => FrequencyHz = 1_000_000),
                                    });
            // Stop on HALT menu item
            var stopOnHaltMenuitem = new MenuItem
            {
                Title = "Stop on HALT",
                CheckType = MenuItemCheckStyle.Checked | MenuItemCheckStyle.NoCheck,
                Checked = stopOnHalt
            };
            stopOnHaltMenuitem.Action = () => { stopOnHalt = (bool)(stopOnHaltMenuitem.Checked = !stopOnHaltMenuitem.Checked); };
            // Menu bar
            var menu = new MenuBar
            {
                Menus = [
                    new MenuBarItem ("_File", new MenuItem [] {
                                        new ("_Open", "", () => LoadFile (), null, null, Key.O.WithCtrl),
                                        new ("_Reload", "", () => LoadFile (lastFileLoaded), null, null, Key.F5.WithCtrl),
                                        null,
                                        exportMenu,
                                        null,
                                        new ("_Quit", "", () => Application.RequestStop (), null, null, Key.Esc)
                                    }),
                    new MenuBarItem ("_Execution", new MenuItem [] {
                                        new ("_Start / Stop", "", () => ToogleSimulation (), null, null, Key.F5),
                                        new ("_Step Instruction", "", () => StepInstruction (), null, null, Key.F9),
                                        new ("_Step Line", "", () => StepLine (), null, null, Key.F10),
                                        new ("_Restart", "", () => Restart(), null, null, Key.F5.WithCtrl.WithShift),
                                        new ("_Reset", "", () => Reset(), null, null, Key.R.WithCtrl),
                                        frequencyMenu,
                                        stopOnHaltMenuitem,
                                        null,
                                        new ("_Goto address", "", () => GotoAddress(), null, null, Key.G.WithCtrl),
                                        new ("_Remove breakpoints", "", () => RemoveBreakpoints(), null, null, Key.F9.WithCtrl.WithShift),
                                        new ("_Remove watchs", "", () => RemoveWatchs(), null, null, Key.F10.WithCtrl.WithShift),
                                        null,
                                        new ("_Clear Terminal", "", () => processor.Devices[DeviceId.Device1].Reset(), null, null, Key.Delete),
                                    }),
                    new MenuBarItem ("_Help", new MenuItem [] {
                                        new ("_About", "", () => MessageBox.Query (
                                                          title: "",
                                                          message: GetAboutBoxMessage (),
                                                          wrapMessage: false,
                                                          buttons: "_Ok"
                                                         ), null, null, Key.A.WithCtrl)
                                    })
                ],
                Key = Key.F1
            };
            return menu;
        }

        private void GotoAddress()
        {
            if (Prompt("Goto address", "Enter the address or label: ", "", out string address))
            {
                ushort decimalAddress = 0;

                // Get the decimal address
                CodeAnalyzer.SymbolInfo? symbolInfo;
                if (tableSymbols.Data is List<CodeAnalyzer.SymbolInfo> symbols &&
                    (symbolInfo = symbols.FirstOrDefault(s => s.Name.Equals(address.Trim(), StringComparison.OrdinalIgnoreCase))) != null)
                {
                    decimalAddress = symbolInfo.Address;
                }
                else
                {
                    try
                    {
                        decimalAddress = Convert.ToUInt16(address, 16);
                    }
                    catch
                    {
                        MessageBox.Query(title: "Invalid address", message: "Unable to find this address / symbol !", buttons: "_Ok");
                        return;
                    }
                }

                // Goto the address
                if (decimalAddress < DefaultAddresses.RAM)
                {
                    tabView.SelectedTab = tabView.Tabs.FirstOrDefault(t => t.DisplayText == "Memory");
                    tableROM.SelectedRow = decimalAddress;
                    tableROM.EnsureSelectedCellIsVisible();
                }
                else if (decimalAddress < DefaultAddresses.IODevice)
                {
                    tabView.SelectedTab = tabView.Tabs.FirstOrDefault(t => t.DisplayText == "Memory");
                    tableRAM.SelectedRow = decimalAddress - DefaultAddresses.RAM;
                    tableRAM.EnsureSelectedCellIsVisible();
                }
                else
                {
                    tabView.SelectedTab = tabView.Tabs.FirstOrDefault(t => t.DisplayText == "I/O Devices");
                }
            }
        }

        private CheckBoxTableSourceWrapperByObject<MemoryEntry> CreateTableSourceWithCheckBox(TableView tableView, SortedSet<int> checkedSet, IEnumerableTableSource<MemoryEntry> toWrap)
        {
            return new CheckBoxTableSourceWrapperByObject<MemoryEntry>(tableView, toWrap,
                (o) => checkedSet.Contains(o.Address),
                (o, state) =>
                {
                    if (state)
                    {
                        checkedSet.Add(o.Address);
                    }
                    else
                    {
                        checkedSet.Remove(o.Address);
                    }
                })
            {
                UseRadioButtons = false
            };
        }

        private bool Prompt(string title, string labelText, string defaultText, out string result)
        {
            bool confirm = false;
            var btnOk = new Button()
            {
                Title = "OK",
                IsDefault = true,
            };
            btnOk.MouseClick += (s, e) =>
            {
                confirm = true;
                Application.RequestStop();
            };
            var btnCancel = new Button() { Title = "Cancel" };
            btnCancel.MouseClick += (s, e) =>
            {
                confirm = false;
                Application.RequestStop();
            };

            var lbl = new Label() { Text = labelText };
            var tf = new TextField()
            {
                Text = defaultText,
                X = Pos.Right(lbl),
                Width = Dim.Fill(),
            };
            tf.ProcessKeyDown += (s, e) =>
            {
                if (e.KeyCode == KeyCode.Enter)
                {
                    confirm = true;
                    Application.RequestStop();
                }
            };
            tf.SelectAll();

            var dlg = new Dialog()
            {
                Title = title,
                Width = Dim.Percent(50),
                Height = 4
            };
            dlg.Add(lbl);
            dlg.Add(tf);

            dlg.AddButton(btnOk);
            dlg.AddButton(btnCancel);

            Application.Run(dlg);

            result = tf.Text?.ToString();

            return confirm;
        }

        private static string GetAboutBoxMessage()
        {
            StringBuilder msg = new();
            msg.AppendLine();

            msg.AppendLine("""
                         ____           ____  ____   _   _ 
                        / ___|         / ___||  _ \ | | | |
                        \___ \  _____ | |    | |_) || | | |
                         ___) ||_____|| |___ |  __/ | |_| |
                        |____/         \____||_|     \___/ 
                        """);
            msg.AppendLine();
            msg.AppendLine("Simulator");
            msg.AppendLine("Version 1.0 - October 2024");
            msg.AppendLine();
            msg.AppendLine("https://sebastien.warin.fr");

            return msg.ToString();
        }
    }
}



