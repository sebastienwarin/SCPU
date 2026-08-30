using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Instructions;

namespace SCode.Compiler.Ast.Expressions.Binary
{
    public class BitwiseExpression : BinaryExpression<BitwiseOperator>
    {
        protected override void BuildBinaryExpression(ValueOrAddress rightOperand)
        {
            switch (Operator)
            {
                case BitwiseOperator.And:
                    Context.InstructionBuilder.EmitAnd(rightOperand);
                    break;

                case BitwiseOperator.Or:
                    Context.InstructionBuilder.EmitOr(rightOperand);
                    break;

                case BitwiseOperator.Xor:
                    Context.InstructionBuilder.EmitExclusifOr(rightOperand);
                    break;
            }
        }
    }
}
