namespace SCPU.Simulator.ConsoleUI.TableSources
{
    using SCPU.Simulator.Core;
    using Terminal.Gui;

    public class ProcessorRomTableSource : IEnumerableTableSource<MemoryEntry>
    {
        private readonly Processor processor;

        private readonly Dictionary<string, Func<MemoryEntry, object>> columnDefinitions = new()
        {
            { "Addr", p => "0x" + p.Address.ToString("X4") },
            { "Value16", p => "0x" + p.Value.ToString("X4") },
            { "Value10", p => p.Value.ToString() },
            { "Label", p => CodeAnalyzer.GetLinesAtAddress(MainWindow.Current.ProgramSource, p.Address).LastOrDefault()?.Symbol?.Name ?? "" },
            { "Line", p => CodeAnalyzer.GetLinesAtAddress(MainWindow.Current.ProgramSource, p.Address).LastOrDefault()?.Line ?? "" }
        };
        
        public string[] ColumnNames { get; }
        public int Columns => ColumnNames.Length;
        public int Rows => processor.ROM.Length;

        public object this[int row, int col] => columnDefinitions[ColumnNames[col]](new MemoryEntry() { Address = row, Value = processor.ROM[row] });

        public ProcessorRomTableSource(Processor processor)
        {
            this.processor = processor;
            ColumnNames = columnDefinitions.Keys.ToArray();
        }

        IEnumerable<MemoryEntry> IEnumerableTableSource<MemoryEntry>.GetAllObjects() => processor.ROM.Select((v, i) => new MemoryEntry() { Address = i, Value = v });

        MemoryEntry IEnumerableTableSource<MemoryEntry>.GetObjectOnRow(int row) => new() { Address = row, Value = processor.ROM[row] };
    }
}
