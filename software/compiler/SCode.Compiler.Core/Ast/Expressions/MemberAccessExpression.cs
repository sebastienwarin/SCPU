using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    public class MemberAccessExpression : Expression
    {
        [ChildNode]
        public Expression Expression { get; set; }

        [ChildNode]
        public Identifier Member { get; set; }

        public override TypeInfo GetResultType()
        {
            var expressionType = Expression.GetResultType();
            if (!expressionType.IsBaseType && expressionType.Declaration != null)
            {
                if (expressionType.Declaration.Identifier.CurrentScope.TryGetIdentifier(Member, out var identifierInfo) &&
                    identifierInfo.Type == IdentifierInfo.IdentifierType.Member)
                {
                    return identifierInfo.DataType!;
                }
                else
                {
                    throw RaiseError($"{Member} is undefined on '{Expression}'");
                }
            }
            else
            {
                throw RaiseError($"Can not determine the member type: '{Expression}' is not a custom type");
            }
        }

        public override string ToString() => $"{Expression}.{Member}";
    }
}
