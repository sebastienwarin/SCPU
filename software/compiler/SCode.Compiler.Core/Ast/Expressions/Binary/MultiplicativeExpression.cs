using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Instructions;

namespace SCode.Compiler.Ast.Expressions.Binary
{
    public class MultiplicativeExpression : BinaryExpression<MultiplicativeOperator>
    {
        protected override void BuildBinaryExpression(ValueOrAddress rightOperand)
        {
            var builder = Context.InstructionBuilder;

            // Generate labels
            var labelLoop = RandomGenerator.RandomStringLabel("multiply_loop");
            var labelSetResult = RandomGenerator.RandomStringLabel("multiply_setresult");
            var labelZero = RandomGenerator.RandomStringLabel("multiply_zero");
            var labelExit = RandomGenerator.RandomStringLabel("multiply_exit");

            // Process operation
            switch (Operator)
            {
                case MultiplicativeOperator.Multiply:
                    builder.EmitStoreA(Registers.R0);           // Set R0 = Left (result)
                    builder.EmitStoreA(Registers.R1);           // Set R1 = Left (multiplicand)
                    builder.EmitJumpIfZero(labelZero);          // Jump if Left = 0
                    builder.EmitLoadA(rightOperand);            // Load right operand
                    builder.EmitJumpIfZero(labelZero);          // Jump if Right = 0
                    builder.EmitDecrement(rightOperand);
                    builder.EmitStoreA(Registers.R2);           // Set R2 = Right-1 (multiplier)
                    builder.EmitJumpIfZero(labelSetResult);     // Exit if R2 = 0
                    builder.SetLabel(labelLoop);                // Start Loop
                    builder.EmitLoadA(Registers.R0);
                    builder.EmitAdd(Registers.R1);
                    builder.EmitStoreA(Registers.R0);           // R0 += R1
                    builder.EmitDecrement(Registers.R2);
                    builder.EmitStoreA(Registers.R2);           // Decrement R2
                    builder.EmitJumpIfNotZero(labelLoop);       // Loop if R2 > 0
                    builder.EmitJump(labelSetResult);           // - otherwise, set result
                    break;

                case MultiplicativeOperator.Divide:
                case MultiplicativeOperator.Modulus:
                    builder.EmitStoreA(Registers.R0);               // Set R0 = Left (dividend)
                    builder.EmitLoadA(rightOperand);                // Load Right operand
                    builder.EmitJumpIfZero(labelZero);              // Jump if Right = 0
                    builder.EmitMove(0, Registers.R1);              // Set R1 = 0 (quotient)
                    builder.EmitMove(Registers.R0, Registers.R2);   // Set R2 = R0 (remainder = dividend)
                    builder.SetLabel(labelLoop);                    // Start Loop 
                    builder.EmitLoadA(Registers.R2);                // Load current remainder (R2)
                    builder.EmitSubtract(rightOperand);             // Subtract divisor from remainder
                    builder.EmitJumpIfCarrySet(labelSetResult);     // If remainder < divisor, exit & set result
                    builder.EmitStoreA(Registers.R2);               // remainder -= divisor
                    builder.EmitIncrement(Registers.R1);
                    builder.EmitStoreA(Registers.R1);               // Set R1 += 1
                    builder.EmitJump(labelLoop);                    // Loop
                    break;
            }

            // If Zero : clear A & exit
            builder.SetLabel(labelZero);
            builder.EmitClearA();
            builder.EmitJump(labelExit);

            // Set result : store result in A
            builder.SetLabel(labelSetResult);
            switch (Operator)
            {
                case MultiplicativeOperator.Multiply:
                    builder.EmitLoadA(Registers.R0);    // R0 = result
                    break;

                case MultiplicativeOperator.Divide:
                    builder.EmitLoadA(Registers.R1);    // R1 = quotient
                    break;

                case MultiplicativeOperator.Modulus:
                    builder.EmitLoadA(Registers.R2);    // R2 = remainder
                    break;
            }

            // Exit
            builder.SetLabel(labelExit);
        }
    }
}
