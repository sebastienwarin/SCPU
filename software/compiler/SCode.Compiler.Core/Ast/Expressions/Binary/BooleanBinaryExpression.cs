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

            // Compare Left & Right operands with a subtraction
            builder.EmitSubtract(rightOperand); // Assume Acc=Left

            // Build the boolean expression
            EmitBooleanResult(labels =>
                BuildBooleanBinaryExpression(rightOperand, labels.labelTrue, labels.labelFalse, labels.labelExit));
        }

        protected void EmitBooleanResult(Action<(string labelTrue, string labelFalse, string labelExit)> buildComparison)
        {
            var builder = Context.InstructionBuilder;
            var labels = (
                labelTrue: RandomGenerator.RandomStringLabel("case_true"),
                labelFalse: RandomGenerator.RandomStringLabel("case_false"),
                labelExit: RandomGenerator.RandomStringLabel("exit"));

            buildComparison(labels);

            // Return True (#1)
            builder.SetLabel(labels.labelTrue);
            builder.EmitLoadA(1);
            builder.EmitJump(labels.labelExit);

            // Return False (#0)
            builder.SetLabel(labels.labelFalse);
            builder.EmitClearA();

            // End
            builder.SetLabel(labels.labelExit);
        }

        public override TypeInfo GetResultType()
        {
            return TypeInfo.Bool;
        }
    }
}
