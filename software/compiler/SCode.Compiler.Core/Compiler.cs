using Antlr4.Runtime;
using Microsoft.Extensions.Logging;
using SCode.Compiler.Ast;
using SCode.Compiler.Exceptions;
using SCPU.Assembler.Model;

namespace SCode.Compiler
{
    /// <summary>
    /// Provides the main entry point for compiling S-Code sources into assembly code.
    /// </summary>
    public class Compiler(ILogger<Compiler> logger)
    {
        /// <summary>
        /// Compiles the given S-Code source document into an intermediate <see cref="CompileResult"/>.
        /// </summary>
        /// <param name="request">The compile request, containing the input <see cref="SourceDocument"/>.</param>
        /// <returns>
        /// A <see cref="CompileResult"/> containing the generated assembly document.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="request"/> is null.</exception>
        /// <exception cref="Exception">Any parsing, semantic, or code generation error is logged and rethrown.</exception>
        public async Task<CompileResult> CompileAsync(CompileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // Create an ANTLR input stream from the source document
                AntlrInputStream inputStream;
                if (request.Source is FileSourceDocument)
                {
                    // File-backed source: let Antlr read directly from disk
                    inputStream = new AntlrFileStream(request.Source.Identifier);
                }
                else
                {
                    // Inline source: read text into memory and tag with identifier
                    inputStream = new AntlrInputStream(await request.Source.ReadAllTextAsync())
                    {
                        name = request.Source.Identifier
                    };
                }

                // Parse and build the program AST/IR
                var program = Program.ParseInputStream(inputStream);
                var context = CompilationContext.CreateContext(program);
                program.VisitProgram(context);
                program.Prepare();
                program.Build();

                // Generate assembly text from the instruction builder
                var result = new CompileResult()
                {
                    GeneratedAssembly = SourceDocument.FromInline(
                        context.InstructionBuilder.AssemblyBuilder.GenerateAssembly(),
                        nameof(CompileResult.GeneratedAssembly),
                        request.Source.BaseDirectory)
                };

                logger.LogInformation("Compilation completed successfully for {Source}.", request.Source.Identifier);
                return result;
            }
            catch (NodeCompilerException ex)
            {
                logger.LogError("Compilation error: {Message}", ex.Message);
                throw;
            }
            catch (ParserException ex)
            {
                logger.LogError("Parsing error: {Message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected compiler failure while processing: {Source}", request.Source.Identifier);
                throw;
            }
        }
    }
}
