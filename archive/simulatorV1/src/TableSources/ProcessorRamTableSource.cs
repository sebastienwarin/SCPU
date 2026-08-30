namespace SCPU.Simulator.ConsoleUI.TableSources
{
    using SCPU.Simulator.Core;
    using Terminal.Gui;

    public class ProcessorRamTableSource : IEnumerableTableSource<MemoryEntry>
    {
        private readonly Processor processor;

        private readonly Dictionary<string, Func<MemoryEntry, object>> columnDefinitions = new()
        {
            { "Addr", p => "0x" + (p.Address + DefaultAddresses.RAM).ToString("X4") },
            { "Value16", p => "0x" + p.Value.ToString("X4") },
            { "Value10", p => p.Value.ToString() },
            { "Label", p => string.Join(',', MainWindow.Current.Symbols?.Where(s => s.Address == (p.Address +  DefaultAddresses.RAM)).Select(s => s.Name) ?? []) },
        };
        
        public string[] ColumnNames { get; }
        public int Columns => ColumnNames.Length;
        public int Rows => processor.RAM.Length;

        public object this[int row, int col] => columnDefinitions[ColumnNames[col]](new MemoryEntry() { Address = row, Value = processor.RAM[row] });

        public ProcessorRamTableSource(Processor processor)
        {
            this.processor = processor;
            ColumnNames = columnDefinitions.Keys.ToArray();
        }

        IEnumerable<MemoryEntry> IEnumerableTableSource<MemoryEntry>.GetAllObjects() => processor.RAM.Select((v, i) => new MemoryEntry() { Address = i, Value = v });

        MemoryEntry IEnumerableTableSource<MemoryEntry>.GetObjectOnRow(int row) => new() { Address = row, Value = processor.RAM[row] };
    }
}
