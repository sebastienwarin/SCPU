using SCPU.Assembler.Model;

namespace SCode.Compiler
{
    /// <summary>
    /// Represents the result of compiling an S-Code program.
    /// </summary>
    public class CompileResult
    {
        /// <summary>
        /// The generated assembly document produced by the compiler.
        /// This is the plain text assembly code corresponding to the input S-Code,
        /// wrapped as a <see cref="SourceDocument"/> for downstream processing 
        /// (e.g. inspection, saving to disk, or passing to the assembler).
        /// </summary>
        public required SourceDocument GeneratedAssembly { get; set; }
    }
}
