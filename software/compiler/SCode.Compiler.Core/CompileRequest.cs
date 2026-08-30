using SCPU.Assembler.Model;

namespace SCode.Compiler
{
    /// <summary>
    /// Represents a request to compile an S-Code program.
    /// </summary>
    public class CompileRequest
    {
        /// <summary>
        /// The input S-Code source document to compile.
        /// Exactly one <see cref="SourceDocument"/> must be provided, typically:
        /// <list type="bullet">
        /// <item><description><see cref="SourceDocument.FileSourceDocument"/> for file-backed sources, or</description></item>
        /// <item><description><see cref="SourceDocument.InlineSourceDocument"/> for in-memory sources (e.g. REPL, tests).</description></item>
        /// </list>
        /// </summary>
        public required SourceDocument Source { get; init; }
    }
}