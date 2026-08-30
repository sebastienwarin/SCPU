namespace SCode.Compiler.Instructions
{
    public class InstructionBuilder
    {
        private string? nextLabel = null;

        internal AssemblyBuilder AssemblyBuilder { get; } = new AssemblyBuilder();

        public void DeclareConstants(string identifier, object value)
        {
            AssemblyBuilder.Constants.Add(identifier, value);
        }

        public string DeclareProgramData(string identifier, object value, bool bypassDedup = false)
        {
            if (bypassDedup)
            {
                AssemblyBuilder.AddData(BankType.ProgramData, identifier, value);
                return identifier;
            }
            else
            {
                AssemblyBuilder.TryAddUniqueData(BankType.ProgramData, identifier, value, out string finalIdentifier);
                return finalIdentifier;
            }
        }

        public void EmitInstruction(Instruction instruction)
        {
            EmitRaw(instruction.ToString());
        }

        public void EmitRaw(string data)
        {
            foreach (string line in data
                .Split(Environment.NewLine)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l)))
            {
                AssemblyBuilder.AddBankData(BankType.Program, new BankData
                {
                    Label = nextLabel,
                    Value = line
                });
                nextLabel = null;
            }
        }

        public void SetLabel(string label)
        {
            if (nextLabel != null)
            {
                AssemblyBuilder.AddBankData(BankType.Program, new BankData { Label = nextLabel });
            }
            nextLabel = label;
        }

        #region S-CPU standard instructions

        public void EmitNotOr(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("NOR", operand));
        }

        public void EmitAdd(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("ADD", operand));
        }

        public void EmitStoreA(string address)
        {
            EmitInstruction(Instruction.Create("STA", address));
        }

        public void EmitJumpIfCarryClear(string address)
        {
            EmitInstruction(Instruction.Create("JCC", address));
        }

        #endregion

        #region Common macro-instructions

        public void EmitClearA()
        {
            EmitInstruction(Instruction.Create("CLR"));
        }

        public void EmitLoadA(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("LDA", operand));
        }

        public void EmitMove(ValueOrAddress source, string destination)
        {
            EmitInstruction(Instruction.Create("MOV", (ValueOrAddress)destination, source));
        }

        public void EmitHalt()
        {
            EmitInstruction(Instruction.Create("HALT"));
        }

        public void EmitReset()
        {
            EmitInstruction(Instruction.Create("RST"));
        }

        #endregion

        #region Jump macro-instructions

        public void EmitJumpIfZero(string address)
        {
            EmitInstruction(Instruction.Create("JZ", address));
        }

        public void EmitJumpIfNotZero(string address)
        {
            EmitInstruction(Instruction.Create("JNZ", address));
        }

        public void EmitJumpIfCarrySet(string address)
        {
            EmitInstruction(Instruction.Create("JCS", address));
        }

        public void EmitJump(string address)
        {
            EmitInstruction(Instruction.Create("JMP", address));
        }

        #endregion

        #region Math macro-instructions

        public void EmitNotA()
        {
            EmitInstruction(Instruction.Create("NOT"));
        }

        public void EmitNot(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("NOT", operand));
        }

        public void EmitAnd(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("AND", operand));
        }

        public void EmitNotAnd(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("NAND", operand));
        }

        public void EmitOr(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("OR", operand));
        }

        public void EmitExclusifOr(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("XOR", operand));
        }

        public void EmitLogicalShiftLeftA()
        {
            EmitInstruction(Instruction.Create("LSL"));
        }

        public void EmitLogicalShiftLeft(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("LSL", operand));
        }

        public void EmitRotateLeftA()
        {
            EmitInstruction(Instruction.Create("ROL"));
        }

        public void EmitRotateLeft(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("ROL", operand));
        }

        public void EmitLogicalShiftRightA()
        {
            EmitInstruction(Instruction.Create("LSR"));
        }

        public void EmitLogicalShiftRight(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("LSR", operand));
        }

        public void EmitRotateRightA()
        {
            EmitInstruction(Instruction.Create("ROR"));
        }

        public void EmitRotateRight(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("ROR", operand));
        }

        #endregion

        #region Logic macro-instruction

        public void EmitIncrementA()
        {
            EmitInstruction(Instruction.Create("INC"));
        }

        public void EmitIncrement(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("INC", operand));
        }

        public void EmitDecrementA()
        {
            EmitInstruction(Instruction.Create("DEC"));
        }

        public void EmitDecrement(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("DEC", operand));
        }

        public void EmitNegateA()
        {
            EmitInstruction(Instruction.Create("NEG"));
        }

        public void EmitNegate(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("NEG", operand));
        }

        public void EmitSubtract(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("SUB", operand));
        }

        public void EmitAddWithCarry(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("ADC", operand));
        }

        public void EmitSubtractWithCarry(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("SBC", operand));
        }

        public void EmitLoadAWithCarry(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("LDC", operand));
        }

        public void EmitClearCarryFlag()
        {
            EmitInstruction(Instruction.Create("CLC"));
        }

        public void EmitSetCarryFlag()
        {
            EmitInstruction(Instruction.Create("SEC"));
        }

        #endregion

        #region Stack macro-instruction

        public void EmitLoadAFromStack(short index)
        {
            EmitInstruction(Instruction.Create("LDS", index));
        }

        public void EmitLoadFromStack(short index, string address)
        {
            EmitInstruction(Instruction.Create("LDS", index, address));
        }

        public void EmitStoreAToStack(short index)
        {
            EmitInstruction(Instruction.Create("STS", index));
        }

        public void EmitStoreToStack(short index, string address)
        {
            EmitInstruction(Instruction.Create("STS", index, address));
        }

        public void EmitPopA()
        {
            EmitInstruction(Instruction.Create("POP"));
        }

        public void EmitPop(string address = null)
        {
            EmitInstruction(Instruction.Create("POP", address));
        }

        public void EmitPushA()
        {
            EmitInstruction(Instruction.Create("PUSH"));
        }

        public void EmitPush(ValueOrAddress operand)
        {
            EmitInstruction(Instruction.Create("PUSH", operand));
        }

        public void EmitCallSubroutine(string address)
        {
            EmitInstruction(Instruction.Create("CALL", address));
        }

        public void EmitReturnFromSubroutine()
        {
            EmitInstruction(Instruction.Create("RET"));
        }

        #endregion
    }
}
