using SCode.Compiler.Ast.Statements.VariableDeclaration;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Statements
{
    [NestedScope(OnlyChild = true)]
    public class FunctionDeclarationStatement : Statement
    {
        public const string AsmFunctionName = "asm"; // void asm(string code)
        public static readonly string[] ReservedFunctions = [AsmFunctionName];

        public bool IsExtern { get; set; }

        [ChildNode]
        public Identifier Identifier { get; set; }

        [ChildNode]
        public List<Parameter> Parameters { get; set; }

        [ChildNode]
        public TypeDescriptor? ReturnType { get; set; }

        [ChildNode]
        public Block? Body { get; set; }

        public List<VariableDeclarator> LocalVariables { get; } = [];

        public bool HasReturnValue => ReturnType != null;

        internal void Register()
        {
            if (!CurrentScope.IsGlobalScope)
            {
                throw RaiseError($"The function '{Identifier.Name}' must be declare in the global scope.");
            }
            else if (!CurrentScope.RegisterIdentifier(Identifier, IdentifierInfo.IdentifierType.Function, ReturnType, this))
            {
                throw RaiseError($"The function '{Identifier}' is already defined in the current scope.");
            }
        }

        protected override void OnPrepare()
        {
            PrepareChildren();
            if (!IsExtern && HasReturnValue && !CheckReturnPath(Body))
            {
                throw RaiseError("Not all code paths return a value");
            }
        }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;

            // Do nothing if extern
            if (IsExtern) return;

            // Set entry point
            builder.SetLabel(Identifier.Name);

            // Create new stack frame
            builder.EmitMove(Registers.FramePointer, Registers.R0);             // Save FP to R0
            builder.EmitMove(Registers.StackPointer, Registers.FramePointer);   // Save SP to FP
            builder.EmitPush(Registers.R0);                                     // Push R0 (old FP)

            // Local variables stack reservations
            if (LocalVariables.Count > 0)
            {
                builder.EmitLoadA(Registers.StackPointer);
                builder.EmitSubtract(LocalVariables.Sum(x => x.Size));
                builder.EmitStoreA(Registers.StackPointer);
            }

            // Build function body
            Body?.Build();

            // If no return value
            if (!HasReturnValue)
            {
                // Free local variables
                FreeLocalVariables();
                // Restore previous stack frame
                RestorePreviousStackFrame();
                // Return
                builder.EmitReturnFromSubroutine();
            }
        }

        internal void FreeLocalVariables()
        {
            if (LocalVariables.Count > 0)
            {
                Context.InstructionBuilder.EmitLoadA(Registers.StackPointer);
                Context.InstructionBuilder.EmitAdd(LocalVariables.Sum(x => x.Size));
                Context.InstructionBuilder.EmitStoreA(Registers.StackPointer);
            }
        }

        internal void RestorePreviousStackFrame()
        {
            Context.InstructionBuilder.EmitPopA();
            Context.InstructionBuilder.EmitStoreA(Registers.FramePointer);
        }

        internal void LoadIdentifierAddress(IdentifierInfo identifierInfo, string? destination = null)
        {
            if (identifierInfo.Type == IdentifierInfo.IdentifierType.Parameter)
            {
                var parameterIndex = this.Parameters.FindIndex(p => p.Identifier.Name == identifierInfo.Name);
                if (parameterIndex >= 0)
                {
                    var maxsize = this.Parameters.Sum(p => ((TypeInfo)p.Type).Size) + 1;
                    var offset = this.Parameters.Skip(parameterIndex + 1).Sum(p => ((TypeInfo)p.Type).Size);
                    Context.InstructionBuilder.EmitLoadA(Registers.FramePointer);
                    Context.InstructionBuilder.EmitAdd(maxsize - offset);
                }
            }
            else if (identifierInfo.Type == IdentifierInfo.IdentifierType.Variable &&
                identifierInfo.SourceNode is VariableDeclarator variableDeclarator &&
                !variableDeclarator.IsGlobalOrStatic)
            {
                Context.InstructionBuilder.EmitLoadA(Registers.FramePointer);
                Context.InstructionBuilder.EmitSubtract(variableDeclarator.Offset);
            }
            else
            {
                throw RaiseError("Unable to load identifier address");
            }

            // Store address to destination if specified
            if (!string.IsNullOrEmpty(destination))
            {
                Context.InstructionBuilder.EmitStoreA(destination);
            }
        }

        internal static void RegisterSpecialsFunctions(Program program)
        {
            foreach (var function in ReservedFunctions)
            {
                program.Context.GlobalScope.RegisterIdentifier(function, IdentifierInfo.IdentifierType.Function);
            }
        }

        private static bool CheckReturnPath(Block? block) => AnalyzeBlock(block).AlwaysReturns;

        public override string ToString() => HasReturnValue ?
            $"Function {ReturnType} {Identifier}({string.Join(", ", Parameters.Select(p => p.Identifier))})" :
            $"Procedure {Identifier}({string.Join(", ", Parameters.Select(p => p.Identifier))})";

        private static Flow AnalyzeBlock(Block? block)
        {
            if (block == null || block.Body == null || block.Body.Count == 0)
            {
                return Flow.FallThrough();
            }
            else
            {
                foreach (Node node in block.Body)
                {
                    var flow = AnalyzeNode(node);

                    // If this statement guarantees a return on all paths that reach it,
                    // the block as a whole returns (code after is unreachable).
                    if (flow.AlwaysReturns)
                    {
                        return Flow.Returns();
                    }
                }

                // We reached the end with at least one path falling through : no guaranteed return
                return Flow.FallThrough();
            }
        }

        private static Flow AnalyzeNode(Node node)
        {
            switch (node)
            {
                case ReturnStatement:
                    return Flow.Returns();

                case Block subBlock:
                    return AnalyzeBlock(subBlock);

                case IfStatement ifStm:
                    {
                        // THEN branch
                        var thenFlow = AnalyzeBlock(ifStm.Then);

                        // ELSE branch: if absent, else "falls through" by definition
                        var elseFlow = ifStm.Else != null ? AnalyzeBlock(ifStm.Else) : Flow.FallThrough();

                        bool alwaysReturns = thenFlow.AlwaysReturns && elseFlow.AlwaysReturns;
                        bool fallsThrough = thenFlow.FallsThrough || elseFlow.FallsThrough;

                        return new Flow(alwaysReturns, fallsThrough);
                    }

                case WhileStatement whileStm:
                    {
                        // Conservative: unless you prove the loop is infinite and always returns,
                        // treat as possibly falling through (condition may be false initially).
                        var bodyFlow = AnalyzeBlock(whileStm.Body);

                        // Optional "else": runs when loop didn't break/execute.
                        var elseFlow = whileStm.Else != null ? AnalyzeBlock(whileStm.Else) : Flow.FallThrough();

                        // Conservatively, a while-statement itself does not guarantee a return.
                        // It falls through if either path can fall through.
                        bool fallsThrough = bodyFlow.FallsThrough || elseFlow.FallsThrough;
                        return new Flow(alwaysReturns: false, fallsThrough: fallsThrough);
                    }

                case ForStatement forStm:
                    {
                        // Same conservative approach as while: assume it can fall through.
                        var _ = AnalyzeBlock(forStm.Body); // analyze for sub-returns but don't claim always-returns
                        return Flow.FallThrough();
                    }

                case SwitchStatement switchStm:
                    {
                        // A switch "always returns" only if it is exhaustive (has a default)
                        // AND every section body always returns on all paths.
                        bool hasDefault = switchStm.Sections.Any(sec => sec.IsDefaultCase);
                        bool allSectionsReturn = true;
                        bool anySectionFallsThrough = false;

                        foreach (var sec in switchStm.Sections)
                        {
                            var secFlow = AnalyzeBlock(sec.Body);
                            allSectionsReturn &= secFlow.AlwaysReturns;
                            anySectionFallsThrough |= secFlow.FallsThrough;
                        }

                        bool alwaysReturns = hasDefault && allSectionsReturn;

                        // If not "alwaysReturns", control can continue after the switch.
                        bool fallsThrough = !alwaysReturns || anySectionFallsThrough;

                        return new Flow(alwaysReturns, fallsThrough);
                    }

                case BreakStatement:
                    return Flow.FallThrough();

                case ContinueStatement:
                    return Flow.FallThrough();

                default:
                    // Expression statements, assignments, etc. : execution continues.
                    return Flow.FallThrough();
            }
        }

        public class Parameter : Node
        {
            [ChildNode]
            public Identifier Identifier { get; set; }

            [ChildNode]
            public TypeDescriptor Type { get; set; }

            protected override void OnPrepare()
            {
                if (!CurrentScope.RegisterIdentifier(Identifier, IdentifierInfo.IdentifierType.Parameter, Type, this))
                {
                    throw RaiseError($"Identifier '{Identifier}' is already defined in the current scope.");
                }
            }

            public override string ToString() => $"{Type} {Identifier}";
        }

        private readonly struct Flow(bool alwaysReturns, bool fallsThrough)
        {
            public bool AlwaysReturns { get; } = alwaysReturns;
            public bool FallsThrough { get; } = fallsThrough;

            public static Flow Returns() => new(alwaysReturns: true, fallsThrough: false);
            public static Flow FallThrough() => new(alwaysReturns: false, fallsThrough: true);
        }
    }
}
