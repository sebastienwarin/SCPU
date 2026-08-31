using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Instructions;

namespace SCode.Compiler.Ast.Expressions.Binary
{
    public class ShiftExpression : BinaryExpression<ShiftOperator>
    {
        protected override void BuildBinaryExpression(ValueOrAddress rightOperand)
        {
            switch (Operator)
            {
                case ShiftOperator.LeftShift:
                    BuildShiftInstructions(rightOperand, builder => builder.EmitLogicalShiftLeftA());
                    break;

                case ShiftOperator.RightShift:
                    if (LeftOperand.GetResultType().TypeCode == Type.SCodeType.Int)
                    {
                        BuildArithmeticShiftRight(rightOperand);
                    }
                    else
                    {
                        BuildShiftInstructions(rightOperand, builder => builder.EmitLogicalShiftRightA());
                    }
                    break;
            }
        }

        private void BuildShiftInstructions(ValueOrAddress valueOrAddress, Action<InstructionBuilder> builderAction)
        {
            
            if (!string.IsNullOrEmpty(valueOrAddress.Address)) // Runtime shift (variable count)
            {
                var builder = Context.InstructionBuilder;

                // Allocate temporary variables and labels
                var leftOperand = Context.TemporaryVariables.Create();
                var index = Context.TemporaryVariables.Create();
                var loopLabel = RandomGenerator.RandomStringLabel("shift_loop");
                var exitLabel = RandomGenerator.RandomStringLabel("shift_exit");

                // Preserve current accumulator value (left operand)
                builder.EmitStoreA(leftOperand);

                // Initialize index with the shift count (right operand)
                builder.EmitMove(valueOrAddress.Address, index);

                // Loop start
                builder.SetLabel(loopLabel);

                // Exit if index == 0
                builder.EmitLoadA(index);
                builder.EmitJumpIfZero(exitLabel);

                // Perform one shift on the left operand
                builder.EmitLoadA(leftOperand);
                builderAction(Context.InstructionBuilder);
                builder.EmitStoreA(leftOperand);

                // Decrement index and repeat
                builder.EmitDecrement(index);
                builder.EmitStoreA(index);
                builder.EmitJump(loopLabel);

                // Loop exit
                builder.SetLabel(exitLabel);

                // Restore final result into A
                builder.EmitLoadA(leftOperand);
            }
            else // Compile-time shift (literal count)
            {
                // Unroll the shift instruction as many times as the literal specifies
                for (int i = 0; i < valueOrAddress.Value; i++)
                {
                    builderAction(Context.InstructionBuilder);
                }
            }
        }

        private void BuildArithmeticShiftRight(ValueOrAddress valueOrAddress)
        {
            var builder = Context.InstructionBuilder;
            var value = Context.TemporaryVariables.Create();
            var index = Context.TemporaryVariables.Create();
            var mask = Context.TemporaryVariables.Create();
            var loopLabel = RandomGenerator.RandomStringLabel("ashr_loop");
            var exitLabel = RandomGenerator.RandomStringLabel("ashr_exit");

            builder.EmitStoreA(value);

            // The sign never changes during an arithmetic shift, so capture the fill bit once.
            // value & 0x8000 is already the sign bit (0x8000 or 0), so 'OR mask' fills it back cheaply.
            builder.EmitLoadA(value);
            builder.EmitAnd(unchecked((short)0x8000));
            builder.EmitStoreA(mask);

            if (!string.IsNullOrEmpty(valueOrAddress.Address))
            {
                builder.EmitMove(valueOrAddress.Address, index);
            }
            else
            {
                builder.EmitMove(valueOrAddress.Value, index);
            }

            builder.SetLabel(loopLabel);
            builder.EmitLoadA(index);
            builder.EmitJumpIfZero(exitLabel);

            builder.EmitLoadA(value);
            builder.EmitLogicalShiftRightA();
            builder.EmitOr(mask);
            builder.EmitStoreA(value);

            builder.EmitDecrement(index);
            builder.EmitStoreA(index);
            builder.EmitJump(loopLabel);

            builder.SetLabel(exitLabel);
            builder.EmitLoadA(value);
        }
    }
}
