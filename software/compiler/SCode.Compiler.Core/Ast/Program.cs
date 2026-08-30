using Antlr4.Runtime;
using SCode.Compiler.Ast.Statements;
using SCode.Compiler.Ast.Statements.VariableDeclaration;
using SCode.Compiler.Exceptions;
using SCode.Compiler.Type;
using SCPU.Architecture;

namespace SCode.Compiler.Ast
{
    public class Program : Node
    {
        // First word available after all the static RAM allocations
        public const string HeapStartSymbol = "__heap_start";
        // First word after the heap area (start of the reserved page)
        public const string HeapEndSymbol = "__heap_end";

        [ChildNode]
        public List<Statement> Body { get; set; }

        public void VisitProgram(CompilationContext context)
        {
            // Set context
            this.SetContext(context);
            // Resolve include statements
            while (ResolveIncludes(out var expandedBody))
            {
                Body = expandedBody;
            }
            // Check program content
            if (Body == null || Body.Count == 0)
            {
                throw this.RaiseError("Program cannot be empty");
            }
            // Declare the memory layout symbols, resolved by the assembler
            Body.Insert(0, CreateHeapSymbolsDeclaration());
            this.SetContext(context);
            // Visit the AST
            this.Visit();
        }

        public override void Prepare()
        {
            // Register functions first
            FunctionDeclarationStatement.RegisterSpecialsFunctions(this);
            foreach (var function in Body.Where(node => node is FunctionDeclarationStatement).Cast<FunctionDeclarationStatement>())
            {
                function.Register();
            }
            // Then, prepare children
            PrepareChildren();
        }

        public override void Build()
        {
            // Build all except functions
            Body.Where(node => node is not FunctionDeclarationStatement).BuildNodes();
            // Halt program
            Context.InstructionBuilder.EmitHalt();
            // Build called functions only
            Body.Where(node => node is FunctionDeclarationStatement functionDeclaration && 
                               Context.CalledFunctions.ContainsKey(functionDeclaration.Identifier))
                .BuildNodes();

            // Must stay the very last user page reservation : its address marks the end of the static allocations
            Context.InstructionBuilder.AssemblyBuilder.AddMemoryReservation(HeapStartSymbol, 1);
            Context.InstructionBuilder.DeclareConstants(HeapEndSymbol, $"0x{MemoryMap.Reserved.Start:X}");
        }

        private static VariableDeclarationStatement CreateHeapSymbolsDeclaration()
        {
            return new VariableDeclarationStatement
            {
                IsExtern = true,
                Type = new TypeDescriptor { Name = TypeInfo.Int.Name, IsBaseType = true },
                Variables =
                [
                    new VariableDeclarator { Identifier = HeapStartSymbol },
                    new VariableDeclarator { Identifier = HeapEndSymbol }
                ]
            };
        }

        private bool ResolveIncludes(out List<Statement>? expandedBody)
        {
            if (Body?.Any(n => n is IncludeStatement) ?? false)
            {
                expandedBody = [];
                foreach (var statement in Body)
                {
                    if (statement is IncludeStatement include)
                    {
                        if (!include.FileInfo.Exists)
                        {
                            throw RaiseError($"Unable to locate included file '{include.FileInfo}'");
                        }
                        else if (include.IsAssemblyFile)
                        {
                            Context.InstructionBuilder.AssemblyBuilder.Includes.Add(include.FileInfo.FullName);
                        }
                        else if (!Context.SourceFiles.Any(fi => fi.FullName.Equals(include.FileInfo.FullName, StringComparison.OrdinalIgnoreCase)))
                        {
                            // TODO : in-memory ?
                            Context.SourceFiles.Add(include.FileInfo);
                            var subprogram = ParseInputStream(new AntlrFileStream(include.FileInfo.FullName));
                            subprogram.SetContext(Context);

                            if (subprogram.Body.Count > 0)
                            {
                                if (statement.HasLabel)
                                {
                                    subprogram.Body[0].Label = statement.Label;
                                }
                                expandedBody.AddRange(subprogram.Body);
                            }
                        }
                    }
                    else
                    {
                        expandedBody.Add(statement);
                    }
                }
                return true;
            }
            else
            {
                expandedBody = null;
                return false;
            }
        }

        public static Program ParseInputStream(AntlrInputStream inputStream)
        {
            var lexer = new SCodeLexer(inputStream);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new ParserErrorListener());

            var commonTokenStream = new CommonTokenStream(lexer);
            var parser = new SCodeParser(commonTokenStream)
            {
                //ErrorHandler = new BailErrorStrategy(),
            };
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new ParserErrorListener());

            var scodeVisitor = new SCodeVisitor();
            return scodeVisitor.VisitProgram(parser.program());
        }
    }
}
