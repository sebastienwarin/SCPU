using SCode.Compiler.Ast;
using SCode.Compiler.Ast.Statements;
using SCode.Compiler.Exceptions;
using SCode.Compiler.Instructions;

namespace SCode.Compiler
{
    public class CompilationContext
    {
        public List<FileInfo> SourceFiles { get; } = [];
        public InstructionBuilder InstructionBuilder { get; } = new();
        public TemporaryVariableManager TemporaryVariables { get; } = new();
        
        public Program Program { get; set; }

        public Scope GlobalScope { get; private set; }
        public List<NodeCompilerException> Errors { get; } = [];

        public Dictionary<string, FunctionDeclarationStatement> CalledFunctions { get; } = [];

        private CompilationContext(Program program)
        {
            GlobalScope = Scope.CreateGlobalScope(this);
            Program = program ?? throw new ArgumentNullException(nameof(program));
            if (program.Source.SourceFile is not null)
            {
                SourceFiles.Add(program.Source.SourceFile);
            }
        }

        internal static CompilationContext CreateContext(Program program)
        {
            return new CompilationContext(program);
        }
    }
}
