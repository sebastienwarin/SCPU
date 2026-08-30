namespace SCode.Compiler.Ast.Literals
{
    public class LiteralBoolean : Literal<bool>
    {
        public static explicit operator LiteralInt(LiteralBoolean boolean)
        {
            return new LiteralInt()
            {
                Source = boolean.Source,
                Value = (short)(boolean.Value ? 1 : 0)
            };
        }
    }
}
