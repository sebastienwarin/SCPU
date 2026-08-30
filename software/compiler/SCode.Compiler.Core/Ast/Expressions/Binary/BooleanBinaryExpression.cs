using SCode.Compiler.Instructions;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions.Binary
{
    public abstract class BooleanBinaryExpression<TOperator> : BinaryExpression<TOperator> where TOperator : Enum
    {
        protected abstract void BuildBooleanBinaryExpression(ValueOrAddress rightOperand, string labelTrue, string labelFalse, string labelExit);

        protected override void BuildBinaryExpression(ValueOrAddress rightOperand)
        {
            var builder = Context.InstructionBuilder;

            // Generate labels
            var labelTrue = RandomGenerator.RandomStringLabel("case_true");
            var labelFalse = RandomGenerator.RandomStringLabel("case_false");
            var labelExit = RandomGenerator.RandomStringLabel("exit");

            // Compare Left & Right operands with a subtraction
            builder.EmitSubtract(rightOperand); // Assume Acc=Left

            // Build the boolean expression
            BuildBooleanBinaryExpression(rightOperand, labelTrue, labelFalse, labelExit);

            // Return True (#1)
            builder.SetLabel(labelTrue);
            builder.EmitLoadA(1);
            builder.EmitJump(labelExit);

            // Return False (#0)
            builder.SetLabel(labelFalse);
            builder.EmitClearA();

            // End
            builder.SetLabel(labelExit);
        }

        public override TypeInfo GetResultType()
        {
            return TypeInfo.Bool;
        }
    }
}
