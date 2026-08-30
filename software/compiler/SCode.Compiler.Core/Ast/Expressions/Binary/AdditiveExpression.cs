using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Instructions;

namespace SCode.Compiler.Ast.Expressions.Binary
{
    public class AdditiveExpression : BinaryExpression<AdditiveOperator>
    {
        protected override void BuildBinaryExpression(ValueOrAddress rightOperand)
        {
            switch (Operator)
            {
                case AdditiveOperator.Add:
                    Context.InstructionBuilder.EmitAdd(rightOperand);
                    break;

                case AdditiveOperator.Subtract:
                    Context.InstructionBuilder.EmitSubtract(rightOperand);
                    break;
            }
        }
    }
}
