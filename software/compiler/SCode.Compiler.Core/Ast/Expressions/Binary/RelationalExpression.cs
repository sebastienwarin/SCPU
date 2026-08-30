using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Instructions;

namespace SCode.Compiler.Ast.Expressions.Binary
{
    public class RelationalExpression : BooleanBinaryExpression<RelationalOperator>
    {
        protected override void BuildBooleanBinaryExpression(ValueOrAddress rightOperand, string labelTrue, string labelFalse, string labelExit)
        {
            var builder = Context.InstructionBuilder;

            switch (Operator)
            {
                case RelationalOperator.LessThan:
                    builder.EmitJumpIfCarryClear(labelFalse);  // True if carry set (JCS Then)
                    builder.EmitJumpIfZero(labelFalse);        // True if A!=0 (JNZ Then)
                    break;

                case RelationalOperator.LessThanOrEqual:
                    builder.EmitJumpIfCarrySet(labelTrue);     // True if carry set (JCS Then)
                    builder.EmitJumpIfNotZero(labelFalse);     // True if A==0 (JZ Then)
                    break;

                case RelationalOperator.GreaterThan:
                    builder.EmitJumpIfCarrySet(labelFalse);    // True if carry clear (JCC Then)
                    builder.EmitJumpIfZero(labelFalse);        // True if A!=0 (JNZ Then)
                    break;

                case RelationalOperator.GreaterThanOrEqual:
                    builder.EmitJumpIfCarryClear(labelTrue);   // True if carry clear (JCC Then)
                    builder.EmitJumpIfNotZero(labelFalse);     // True if A==0 (JN Then)
                    break;
            }
        }
    }
}
