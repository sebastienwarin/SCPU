using Antlr4.Runtime;

namespace SCode.Compiler.Exceptions
{
    public class ParserErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        public override void SyntaxError(
            TextWriter output, IRecognizer recognizer,
            IToken offendingSymbol, int line,
            int charPositionInLine, string msg,
            RecognitionException e)
        {
            SyntaxError(output, recognizer, 0, line, charPositionInLine, msg, e);
        }

        public void SyntaxError(
            TextWriter output, IRecognizer recognizer,
            int offendingSymbol, int line,
            int charPositionInLine, string msg,
            RecognitionException e)
        {
            var position = new SourceRange.Position { Column = charPositionInLine, Line = line };
            var sourceRange = new SourceRange()
            {
                Source = recognizer.InputStream.SourceName,
                Start = position,
                End = position,
            };
            throw new ParserException($"{sourceRange} : {msg}", e) { SourceRange = sourceRange };
        }
    }
}
