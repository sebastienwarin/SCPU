using Antlr4.Runtime.Misc;

namespace SCode.Compiler.Exceptions
{
    public class ParserException(string message, Exception exception)
        : ParseCanceledException(message, exception)
    {
        public SourceRange SourceRange { get; set; }
    }
}
