using System.Text.RegularExpressions;

namespace SCode.Compiler.Instructions
{
    internal class AssemblyOptimizer
    {
        private static readonly List<RegexLineProcessor> lineProcessors =
        [
            new RegexLineProcessor(RegexLineType.SkipNextLine, "MOV (?<id>.*), .*", "LDA (?<id>.*)"),
            new RegexLineProcessor(RegexLineType.SkipNextLine, "STA (?<id>.*)", "LDA (?<id>.*)"),
            new RegexLineProcessor(RegexLineType.SkipNextLine, ".*", "ADD #0"),
            new RegexLineProcessor(RegexLineType.SkipNextLine, ".*", "SUB #0"),
            new RegexLineProcessor(RegexLineType.ReplaceNextLine, "STA (?<id>.*)", "PUSH (?<id>.*)", "PUSH"),
            new RegexLineProcessor(RegexLineType.SkipLine | RegexLineType.ReplaceNextLine, "PUSH #0", "STS #1, (?<id>.*)", "PUSH $1"),
        ];

        internal static bool ProcessLines(ref List<BankData> programData)
        {
            bool isUpdated = false;
            var programDataOptimized = new List<BankData>();
            for (int i = 0; i < programData.Count; i++)
            {
                bool hasNextItem = i < programData.Count - 1;
                if (hasNextItem && !programData[i].HasLabel && !programData[i + 1].HasLabel)
                {
                    bool lineProcessed = false;
                    foreach (var processor in lineProcessors)
                    {
                        lineProcessed = processor.Process(programData[i].Value, programData[i + 1].Value, out string? newLine);
                        if (lineProcessed)
                        {
                            isUpdated = true;
                            if (!processor.Type.HasFlag(RegexLineType.SkipLine))
                            {
                                if (processor.Type.HasFlag(RegexLineType.ReplaceLine) && !string.IsNullOrEmpty(newLine))
                                {
                                    programDataOptimized.Add(new BankData { Value = newLine });
                                }
                                else
                                {
                                    programDataOptimized.Add(programData[i]);
                                }
                            }

                            if (!processor.Type.HasFlag(RegexLineType.SkipNextLine))
                            {
                                if (processor.Type.HasFlag(RegexLineType.ReplaceNextLine) && !string.IsNullOrEmpty(newLine))
                                {
                                    programDataOptimized.Add(new BankData { Value = newLine });
                                }
                                else
                                {
                                    programDataOptimized.Add(programData[i]);
                                }
                            }
                            i++;
                            break;
                        }
                    }
                    if (lineProcessed) continue;
                }
                programDataOptimized.Add(programData[i]);
            }
            if (isUpdated)
            {
                programData = programDataOptimized;
            }
            return isUpdated;
        }

        [Flags]
        internal enum RegexLineType
        {
            SkipLine = 1,
            SkipNextLine = 2,
            ReplaceLine = 4,
            ReplaceNextLine = 8
        }

        internal struct RegexLineProcessor
        {
            private readonly string[] groupNames;
            public Regex CurrentLineRegex { get; set; }
            public Regex NextLineRegex { get; set; }
            public string? Subtitution { get; set; }
            public RegexLineType Type { get; set; }

            public RegexLineProcessor(RegexLineType type, string currentLineRegex, string nextLineRegex, string? subtitution = null)
            {
                if ((type.HasFlag(RegexLineType.ReplaceLine) && type.HasFlag(RegexLineType.SkipLine)) ||
                    (type.HasFlag(RegexLineType.ReplaceNextLine) && type.HasFlag(RegexLineType.SkipNextLine)) ||
                    (type.HasFlag(RegexLineType.ReplaceLine) && type.HasFlag(RegexLineType.ReplaceNextLine)))
                {
                    throw new ArgumentException("Invalid types", nameof(type));
                }

                Type = type;
                CurrentLineRegex = new Regex(currentLineRegex, RegexOptions.Compiled);
                NextLineRegex = new Regex(nextLineRegex, RegexOptions.Compiled);
                Subtitution = subtitution;
                groupNames = CurrentLineRegex.GetGroupNames().Where(name => !int.TryParse(name, out _)).ToArray();
            }

            public readonly bool Process(string currentLine, string nextLine, out string? replaceNextLine)
            {
                replaceNextLine = null;
                var matchCurrentLine = CurrentLineRegex.Match(currentLine);
                if (matchCurrentLine.Success)
                {
                    var matchNextLine = NextLineRegex.Match(nextLine);
                    if (matchNextLine.Success)
                    {
                        bool groupsMatch = true;
                        foreach (var groupName in groupNames)
                        {
                            if (!matchCurrentLine.Groups.ContainsKey(groupName) ||
                                !matchNextLine.Groups.ContainsKey(groupName) ||
                                matchCurrentLine.Groups[groupName].Value != matchNextLine.Groups[groupName].Value)
                            {
                                groupsMatch = false;
                                break;
                            }
                        }
                        // All match
                        if (groupsMatch)
                        {
                            if (!string.IsNullOrEmpty(Subtitution))
                            {
                                if (Type.HasFlag(RegexLineType.ReplaceNextLine))
                                {
                                    replaceNextLine = NextLineRegex.Replace(nextLine, Subtitution);
                                }
                                else if (Type.HasFlag(RegexLineType.ReplaceLine))
                                {
                                    replaceNextLine = CurrentLineRegex.Replace(currentLine, Subtitution);
                                }
                            }
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }
}
