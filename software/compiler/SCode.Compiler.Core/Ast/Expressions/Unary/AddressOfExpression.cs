using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions.Unary
{
    public class AddressOfExpression : UnaryExpression
    {
        protected override void OnPrepare()
        {
            PrepareChildren();
            if (Target is not IdentifierExpression && Target is not ArrayAccessExpression)
            {
                throw RaiseError($"The '&' (address-of) operator can only be applied to identifiers and array accessor.");
            }
        }
        
        protected override void OnBuild()
        {
            if (Target is IdentifierExpression identifierExpression)
            {
                identifierExpression.EmitLoadAddress(true);
            }
            else if (Target is ArrayAccessExpression arrayAccessExpression)
            {
                arrayAccessExpression.EmitLoadRowAddress();
            }
        }

        public override TypeInfo GetResultType()
        {
            return TypeInfo.Int;
        }
    }
}
