using SCode.Compiler.Ast.Expressions;
using SCode.Compiler.Ast.Expressions.Initializers;
using SCode.Compiler.Ast.Statements;

namespace SCode.Compiler.Ast.Literals
{
    public class LiteralString : Literal<string>
    {
        public string ResourceKey { get; private set; } = RandomGenerator.RandomStringLabel("string");

        public override void Prepare()
        {
            // Declare all litteral strings in program data except for inline assembly and value initializer
            if (Parent?.Parent is not ValueInitializerExpression && 
                (Parent?.Parent is not FunctionInvocationExpression functionInvocation ||
                functionInvocation.Identifier != FunctionDeclarationStatement.AsmFunctionName))
            {
                ResourceKey = Context.InstructionBuilder.DeclareProgramData(ResourceKey, Value);
            }
        }
    }
}
