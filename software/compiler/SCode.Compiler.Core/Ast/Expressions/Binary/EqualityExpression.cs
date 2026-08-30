using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Instructions;

namespace SCode.Compiler.Ast.Expressions.Binary
{
    public class EqualityExpression : BooleanBinaryExpression<EqualityOperator>
    {
        protected override void BuildBooleanBinaryExpression(ValueOrAddress rightOperand, string labelTrue, string labelFalse, string labelExit)
        {
            var builder = Context.InstructionBuilder;
            
            switch (Operator)
            {
                case EqualityOperator.Equal:
                    builder.EmitJumpIfZero(labelTrue);         // True if A==0 (JZ Then)
                    builder.EmitJump(labelFalse);
                    break;

                case EqualityOperator.NotEqual:
                    builder.EmitJumpIfZero(labelFalse);        // True if A!=0 (JNZ Then)
                    break;
            }
        }
    }
}
