using Antlr4.Runtime;

namespace SCode.Compiler
{
    public class SourceRange
    {
        public string? Source { get; set; }
        public FileInfo? SourceFile => new FileInfo(Source);

        public Position Start { get; set; }
        public Position End { get; set; }

        public static SourceRange FromParserContext(ParserRuleContext context)
        {
            return new SourceRange
            {
                Source = context.Start.TokenSource?.SourceName,
                Start = Position.FromToken(context.Start),
                End = Position.FromToken(context.Stop ?? context.Start)
            };
        }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(Source) ?
                $"{SourceFile.Name}({Start})" :
                $"({Start})";
        }

        public struct Position
        {
            public bool IsEmpty => Line == 0;
            public int Line { get; set; }
            public int Column { get; set; }

            public override string ToString() => $"{Line}:{Column}";

            public static Position FromToken(IToken token)
            {
                return new Position { Line = token.Line, Column = token.Column };
            }
        }
    }
}
