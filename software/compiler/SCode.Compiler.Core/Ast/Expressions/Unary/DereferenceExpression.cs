using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions.Unary
{
    public class DereferenceExpression : UnaryExpression
    {
        protected override void OnBuild()
        {
            // Build expression
            Target.Build();

            // Load the value pointed
            Context.InstructionBuilder.EmitStoreA(Registers.RPeek);
            Context.InstructionBuilder.EmitLoadA(Registers.RPeek.AsIndirectAddress());
        }

        // Writes 'value' at the address this expression points to (used by assignment and inc/dec).
        public void EmitStoreThrough(TemporaryVariable value)
        {
            Target.Build();
            Context.InstructionBuilder.EmitStoreA(Registers.RPeek);
            Context.InstructionBuilder.EmitMove(value, Registers.RPeek.AsIndirectAddress());
        }

        public override TypeInfo GetResultType()
        {
            if (Target is IdentifierExpression identifierExpression &&
                CurrentScope.TryGetIdentifier(identifierExpression.Identifier, out var identifierInfo) &&
                identifierInfo.DataType != null && (identifierInfo.DataType.IsPointer || identifierInfo.DataType.TypeInfo == TypeInfo.String))
            {
                TypeInfo type = identifierInfo.DataType;
                if (type.TypeCode == SCodeType.String)
                {
                    return TypeInfo.Char;
                }
                else // if ( type.IsPointer )
                {
                    type.PointerLevel--;
                    return type;
                }
            }
            else
            {
                return Target.GetResultType();
            }
        }
    }
}
