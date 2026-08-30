namespace SCPU.Simulator.ConsoleUI.TableSources
{
    using System.Collections.Generic;
    using Terminal.Gui;

    internal class BreakpointsTableSource : IEnumerableTableSource<MemoryEntry>
    {
        private readonly MainWindow mainWindow;

        private readonly Dictionary<string, Func<MemoryEntry, object>> columnDefinitions = new()
        {
            { "Addr", p => "0x" + p.Address.ToString("X4") },
            { "Value", p => "0x" + p.Value.ToString("X4") },
            { "Instruction", p => MainWindow.DecodeInstruction(p.Value) }
        };

        public string[] ColumnNames { get; }
        public int Columns => ColumnNames.Length;
        public int Rows => mainWindow.Breakpoints.Count;

        public object this[int row, int col]
        {
            get
            {
                var address = mainWindow.Breakpoints.ToArray()[row];
                return columnDefinitions[ColumnNames[col]](new MemoryEntry() { Address = address, Value = mainWindow.Processor.ROM[address] });
            }
        }

        public BreakpointsTableSource(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            ColumnNames = columnDefinitions.Keys.ToArray();
        }

        IEnumerable<MemoryEntry> IEnumerableTableSource<MemoryEntry>.GetAllObjects() => mainWindow.Breakpoints.Order().Select(v => new MemoryEntry() { Address = v, Value = mainWindow.Processor.ROM[v] });

        MemoryEntry IEnumerableTableSource<MemoryEntry>.GetObjectOnRow(int row)
        {
            var address = mainWindow.Breakpoints.ToArray()[row];
            return new MemoryEntry() { Address = address, Value = mainWindow.Processor.ROM[address] };
        }
    }
}
