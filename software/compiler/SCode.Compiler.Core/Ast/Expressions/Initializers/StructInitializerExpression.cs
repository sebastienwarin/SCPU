using SCode.Compiler.Ast.Statements;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions.Initializers
{
    public class StructInitializerExpression : InitializerExpression
    {
        public VariableDeclarationStatement? VariableDeclaration => GetFirstAncestor<VariableDeclarationStatement>();

        [ChildNode]
        public List<MemberInitializer> Initializers { get; set; }

        public override TypeInfo GetResultType()
        {
            return VariableDeclaration!.Type;
        }

        public override string ToString() => $"[{string.Join(", ", Initializers.Select(c => c.ToString()))}]";

        public class MemberInitializer : Node
        {
            [ChildNode]
            public Identifier Identifier { get; set; }

            [ChildNode]
            public InitializerExpression Initializer { get; set; }

            public override string ToString() => $"{Identifier} = {Initializer}";
        }
    }
}
