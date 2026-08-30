namespace SCode.Compiler.Ast.Statements
{
    public class ReturnStatement : Statement
    {
        private FunctionDeclarationStatement? functionDeclarationStatement;

        [ChildNode]
        public Expression? Value { get; set; }

        protected override void OnPrepare()
        {
            PrepareChildren();
            functionDeclarationStatement = GetFirstAncestor<FunctionDeclarationStatement>();
            if (functionDeclarationStatement != null && functionDeclarationStatement.HasReturnValue && Value == null)
            {
                throw RaiseError("Function must return a value");
            }
        }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;
                        
            // If the return in inside a function
            if (functionDeclarationStatement != null)
            {
                // If the function has a return value
                if (functionDeclarationStatement.HasReturnValue)
                {
                    // Process the expression
                    Value!.Build();

                    // Save result to R0
                    builder.EmitStoreA(Registers.R0);

                    // Calculate the result container offset from the FP
                    builder.EmitLoadA(Registers.FramePointer);
                    builder.EmitAdd(functionDeclarationStatement.Parameters.Count + 2);
                    builder.EmitStoreA(Registers.R1);

                    // Store the result (R0) to the result container (R1)
                    builder.EmitMove(Registers.R0, Registers.R1.AsIndirectAddress());
                }

                // Free local variables & restore previous stack frame
                functionDeclarationStatement.FreeLocalVariables();
                functionDeclarationStatement.RestorePreviousStackFrame();

                // Return
                builder.EmitReturnFromSubroutine();
            }
            else // If outside a function
            {
                if (Value == null)
                {
                    // Clear A if no expression
                    builder.EmitClearA();
                }
                else
                {
                    // Otherwise, build the expression
                    Value.Build();
                }

                // In any case, Halt program
                builder.EmitHalt();
            }
        }

        public override string ToString() => $"Return {Value}";
    }
}
