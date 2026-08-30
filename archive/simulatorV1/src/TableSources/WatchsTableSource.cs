namespace SCPU.Simulator.ConsoleUI.TableSources
{
    using SCPU.Simulator.Core;
    using System.Collections.Generic;
    using Terminal.Gui;

    internal class WatchsTableSource : IEnumerableTableSource<MemoryEntry>
    {
        private readonly MainWindow mainWindow;

        private readonly Dictionary<string, Func<MemoryEntry, object>> columnDefinitions = new()
        {
            { "Addr", p => "0x" + (p.Address + DefaultAddresses.RAM).ToString("X4") },
            { "Value16", p => "0x" + p.Value.ToString("X4") },
            { "Value10", p => p.Value.ToString() },
            { "Label", p => string.Join(',', MainWindow.Current.Symbols?.Where(s => s.Address == (p.Address +  DefaultAddresses.RAM)).Select(s => s.Name) ?? []) },
        };

        public string[] ColumnNames { get; }
        public int Columns => ColumnNames.Length;
        public int Rows => mainWindow.Watchs.Count;

        public object this[int row, int col]
        {
            get
            {
                var address = mainWindow.Watchs.ToArray()[row];
                return columnDefinitions[ColumnNames[col]](new MemoryEntry() { Address = address, Value = mainWindow.Processor.RAM[address] });
            }
        }

        public WatchsTableSource(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            ColumnNames = columnDefinitions.Keys.ToArray();
        }

        IEnumerable<MemoryEntry> IEnumerableTableSource<MemoryEntry>.GetAllObjects() => mainWindow.Watchs.Order().Select(v => new MemoryEntry() { Address = v, Value = mainWindow.Processor.RAM[v] });

        MemoryEntry IEnumerableTableSource<MemoryEntry>.GetObjectOnRow(int row)
        {
            var address = mainWindow.Watchs.ToArray()[row];
            return new MemoryEntry() { Address = address, Value = mainWindow.Processor.RAM[address] };
        }
    }
}
