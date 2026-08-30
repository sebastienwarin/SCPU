namespace SCPU.Simulator.ConsoleUI
{
    using System.Text.RegularExpressions;

    public static class CodeAnalyzer
    {
        public static List<LineInfo> GetLinesAtAddress(List<LineInfo> lines, ushort address)
        {
            return lines.Where(line => line.Annotation?.Address == address).ToList();
        }

        public static List<LineInfo> GetLinesAtAddress(List<LineInfo> lines, int address)
        {
            return GetLinesAtAddress(lines, (ushort)address);
        }

        public static List<LineInfo> Analyze(string sourceFile, string annotatedFile, string symbolFile)
        {
            // Load the annotations & symbols
            var annotations = LoadAnnotations(annotatedFile);
            var symbols = LoadSymbols(symbolFile);

            // Load the source code lines
            var lines = File
                    .ReadAllLines(sourceFile)
                    .Select((srcline, idx) => new LineInfo()
                    {
                        Number = idx,
                        Line = Regex.Replace(srcline, @"(.*);.*", "$1").Trim(),
                        RawLine = srcline.Trim()
                    })
                    .ToList();

            // Resolve links
            int annotationIdx = 0;
            foreach (var line in lines)
            {
                // Skip empty, comment or directive line
                if (string.IsNullOrEmpty(line.Line) || line.Line.StartsWith("#") || line.Line.StartsWith(";"))
                {
                    continue;
                }

                // Search annotations
                line.Annotation = annotations?.Skip(annotationIdx).FirstOrDefault(a => a.Line == line.Line);
                if (line.Annotation != null)
                {
                    // Get symbols
                    line.Symbols.AddRange(symbols?.Where(symbol => symbol.Address == line.Annotation.Address));
                    // Save annotations index
                    annotationIdx = annotations.FindIndex(o => o.Address == line.Annotation.Address) + 1;
                }
            }

            // Return LineInfos
            return lines;
        }

        public static List<SymbolInfo> LoadSymbols(string symbolFile)
        {
            return File
                    .ReadAllLines(symbolFile)
                    .Where(line => !string.IsNullOrEmpty(line))
                    .Select(line => line.Split('='))
                    .Select(line => new SymbolInfo()
                    {
                        Name = line[0].Trim(),
                        Address = Convert.ToUInt16(line[1].Trim(), 16)
                    })
                    .Where(symbol => !symbol.Name.StartsWith("__"))
                    .ToList();
        }

        public static List<AnnotationInfo> LoadAnnotations(string annotatedFile)
        {
            return File
                    .ReadAllLines(annotatedFile)
                    .Skip(1)
                    .Where(line => !string.IsNullOrEmpty(line))
                    .Select(line => line.Split('|'))
                    .Select(line => new Tuple<ushort, string[]>(Convert.ToUInt16(line[1].Trim(), 16), line[2].Split(';')))
                    .Select(line => new AnnotationInfo()
                    {
                        Address = line.Item1,
                        Instructions = line.Item2[0].Trim().Split(' ').Where(v => !string.IsNullOrEmpty(v)).Select(v => Convert.ToUInt16(v, 16)).ToArray(),
                        Line = line.Item2[1].Trim()
                    })
                    .ToList();
        }

        public class AnnotationInfo
        {
            public ushort Address { get; set; }
            public ushort[] Instructions { get; set; }
            public string Line { get; set; }

            public override string ToString()
            {
                return $"{Address}: {Line}";
            }
        }

        public class SymbolInfo
        {
            public ushort Address { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return $"{Name}={Address}";
            }
        }

        public class LineInfo
        {
            public int Number { get; set; }
            public string Line { get; set; }
            public string RawLine { get; set; }
            public SymbolInfo Symbol => Symbols.LastOrDefault();
            public List<SymbolInfo> Symbols { get; set; } = [];
            public AnnotationInfo Annotation { get; set; }

            public override string ToString()
            {
                return Line;
            }
        }
    }
}
