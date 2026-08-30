using SCode.Compiler.Ast.Literals;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    internal class LiteralExpression : Expression
    {
        [ChildNode]
        public Literal Literal { get; set; }

        public bool IsNumericValue => Literal is LiteralInt || Literal is LiteralChar || Literal is LiteralBoolean;
        public short NumericValue => ((LiteralInt)(dynamic)Literal).Value;

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;
            if (IsNumericValue)
            {
                builder.EmitLoadA(NumericValue);
            }
            else if (Literal is LiteralString literalString)
            {
                builder.EmitLoadA(literalString.ResourceKey.AsImmediateValue());
            }
            else
            {
                // TODO: long
                throw new NotImplementedException("Long type is not yet supported");
            }
        }

        public override TypeInfo GetResultType()
        {
            return TypeInfo.FromSystemType(Literal.Value.GetType());
        }

        public override string ToString() => Literal.ToString();
    }
}
