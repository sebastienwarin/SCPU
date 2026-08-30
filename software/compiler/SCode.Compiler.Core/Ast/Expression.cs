using SCode.Compiler.Type;

namespace SCode.Compiler.Ast
{
    public abstract class Expression : Node
    {
        public abstract TypeInfo GetResultType();
    }
}
