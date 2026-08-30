using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Instructions;

namespace SCode.Compiler.Ast.Expressions.Binary
{
    public class LogicalExpression : BooleanBinaryExpression<LogicalOperator>
    {
        public override void Build()
        {
            var builder = Context.InstructionBuilder;
            var labelTrue = RandomGenerator.RandomStringLabel("case_true");
            var labelFalse = RandomGenerator.RandomStringLabel("case_false");
            var labelExit = RandomGenerator.RandomStringLabel("exit");

            // Evaluate left operand
            LeftOperand.Build();
            switch (Operator)
            {
                case LogicalOperator.And:
                    builder.EmitJumpIfZero(labelFalse);    // If Left == false, shortcut to false
                    break;

                case LogicalOperator.Or:
                    builder.EmitJumpIfNotZero(labelTrue);  // If Left == true, shortcut to true
                    break;
            }

            // Evaluate right operand
            RightOperand.Build();
            switch (Operator)
            {
                case LogicalOperator.And:
                    builder.EmitJumpIfZero(labelFalse);    // If Left == false, shortcut to false
                    break;

                case LogicalOperator.Or:
                    builder.EmitJumpIfNotZero(labelTrue);  // If Left == true, shortcut to true
                    break;
            }

            // If no operand is True in OR operation, the result is False
            if (Operator == LogicalOperator.Or)
            {
                builder.EmitJump(labelFalse);
            }

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

        protected override void BuildBooleanBinaryExpression(ValueOrAddress rightOperand, string labelTrue, string labelFalse, string labelExit)
        {
            throw new NotImplementedException("Use Build() method directly");
        }
    }
}
