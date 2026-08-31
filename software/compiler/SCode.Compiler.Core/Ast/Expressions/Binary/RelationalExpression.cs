using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Instructions;

namespace SCode.Compiler.Ast.Expressions.Binary
{
    public class RelationalExpression : BooleanBinaryExpression<RelationalOperator>
    {
        protected override void BuildBinaryExpression(ValueOrAddress rightOperand)
        {
            if (!IsSignedIntegerOperation())
            {
                base.BuildBinaryExpression(rightOperand);
                return;
            }

            var builder = Context.InstructionBuilder;
            var left = Context.TemporaryVariables.Create();
            var right = Context.TemporaryVariables.Create();

            // Bias both operands by 0x8000 so signed ordering becomes unsigned ordering.
            // On 16 bits, ADD #0x8000 flips bit 15 exactly like XOR #0x8000 but costs 1 word instead of 12.
            builder.EmitStoreA(left);
            builder.EmitLoadA(rightOperand);
            builder.EmitAdd(unchecked((short)0x8000));
            builder.EmitStoreA(right);
            builder.EmitLoadA(left);
            builder.EmitAdd(unchecked((short)0x8000));
            builder.EmitSubtract(right);

            EmitBooleanResult(labels =>
                BuildBooleanBinaryExpression(right, labels.labelTrue, labels.labelFalse, labels.labelExit));
        }

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
