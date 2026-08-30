using SCode.Compiler.Ast.Literals;
using SCode.Compiler.Ast.Statements;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    public class FunctionInvocationExpression : Expression
    {
        private FunctionDeclarationStatement? _functionDeclarationStatement = null;

        [ChildNode]
        public Identifier Identifier { get; set; }

        [ChildNode]
        public List<Expression> Arguments { get; set; }

        protected override void OnPrepare()
        {
            PrepareChildren();
            if (!CurrentScope.TryGetIdentifier(Identifier.Name, out var identifierInfo))
            {
                throw RaiseError($"Function '{Identifier.Name}' is not defined in the current scope.");
            }
            else if (Identifier.Name == FunctionDeclarationStatement.AsmFunctionName)
            {
                if (Arguments?.Count != 1 || Arguments.First() is not LiteralExpression expression || expression.Literal is not LiteralString)
                {
                    throw RaiseError("The function asm() requires a string literal as its argument. Only hard-coded string values are accepted.");
                }
            }
            else if (identifierInfo?.SourceNode is not FunctionDeclarationStatement functionDeclarationStatement)
            {
                throw RaiseError($"Function declaration '{Identifier.Name}' does not exist.");
            }
            else if (functionDeclarationStatement.Parameters.Count != Arguments.Count)
            {
                throw RaiseError($"The arguments passed do not match the signature of the function '{Identifier.Name}'. Please check the number and types of arguments required.");
            }
            else
            {
                // Check arguments/parameters
                for (int i = 0; i < Arguments.Count; i++)
                {
                    if(!TypeHelper.CanConvert(functionDeclarationStatement.Parameters[i].Type, Arguments[i].GetResultType()))
                    {
                        throw RaiseError($"The argument for parameter '{functionDeclarationStatement.Parameters[i].Identifier}' does not match the expected type in the function signature.");
                    }
                }

                _functionDeclarationStatement = functionDeclarationStatement;
                Context.CalledFunctions[functionDeclarationStatement.Identifier] = functionDeclarationStatement;
            }
        }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;
            if (Identifier.Name == FunctionDeclarationStatement.AsmFunctionName)
            {
                builder.EmitRaw(Arguments.First().ToString());
            }
            else
            {
                // Push result container
                if (_functionDeclarationStatement!.HasReturnValue)
                {
                    builder.EmitPush(0);
                }

                // Push arguments
                for (int i = Arguments.Count - 1; i >= 0; i--)
                {
                    Arguments[i].Build();
                    builder.EmitPushA();
                }

                // Call the function
                builder.EmitCallSubroutine(_functionDeclarationStatement.Identifier.Name);

                // Free stack
                for (int i = Arguments.Count - 1; i >= 0; i--)
                {
                    builder.EmitPopA();
                }
                if (_functionDeclarationStatement.HasReturnValue)
                {
                    // Return value to accumulator
                    builder.EmitPopA();
                }
            }
        }

        public override TypeInfo GetResultType()
        {
            CurrentScope.TryGetIdentifier(Identifier, out var functionDeclaration);
            return functionDeclaration?.DataType ?? TypeInfo.Empty;
        }

        public override string ToString() => $"Call {Identifier}({string.Join(", ", Arguments)})";
    }
}
