namespace SCode.Compiler.Ast.Literals
{
    public class LiteralChar : Literal<char>
    {
        public static explicit operator LiteralInt(LiteralChar literalChar)
        {
            return new LiteralInt()
            {
                Source = literalChar.Source,
                Value = (short)literalChar.Value
            };
        }

        public override string ToString() => Value.Escape();
    }
}
