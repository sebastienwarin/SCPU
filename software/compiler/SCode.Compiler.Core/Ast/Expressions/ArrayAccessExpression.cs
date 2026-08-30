using SCode.Compiler.Ast.Literals;
using SCode.Compiler.Ast.Statements.VariableDeclaration;
using SCode.Compiler.Instructions;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    public class ArrayAccessExpression : Expression
    {
        private IdentifierInfo? arrayIdentifierInfo;

        public IdentifierExpression? ArrayIdentifier => Array as IdentifierExpression;
        public IdentifierInfo? ArrayIdentifierInfo => arrayIdentifierInfo;
        public VariableDeclarator? ArrayVariableDeclarator => arrayIdentifierInfo?.SourceNode as VariableDeclarator;

        public bool HasIndices => Indices?.Count > 0;

        [ChildNode]
        public Expression Array { get; set; }

        [ChildNode]
        public List<Expression> Indices { get; set; }

        protected override void OnPrepare()
        {
            PrepareChildren();
            if (Array is ArrayAccessExpression)
            {
                throw RaiseError($"Nested arrays are not supported - multi-dimensional arrays should use the format Array[x, y].");
            }
            else if (Array is IdentifierExpression identifier &&
                CurrentScope.TryGetIdentifier(identifier.Identifier, out arrayIdentifierInfo) &&
                arrayIdentifierInfo.SourceNode is VariableDeclarator declarator)
            {
                if (!declarator.IsArray && declarator.Declaration.Type != TypeInfo.String)
                {
                    throw RaiseError($"Attempted array-style indexing on identifier '{identifier.Identifier}' which is neither an array nor a string.");
                }
                else if (declarator.IsArray && declarator.ArraySpecifier?.Count != Indices.Count)
                {
                    throw RaiseError($"Incorrect number of indices for array '{identifier.Identifier}'. Expected {declarator.ArraySpecifier?.Count} but received {Indices.Count}.");
                }
            }
            else
            {
                throw RaiseError($"Not supported array access expression on '{Array}'");
            }
        }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;

            // If the array is global/static, accessed with a literal index and not contains pointer or string, the element value can be resolved directly
            if (ArrayVariableDeclarator != null && ArrayVariableDeclarator.IsGlobalOrStatic && IsLiteralIntIndices() &&
                (!ArrayVariableDeclarator.Declaration.Type.IsPointer && ArrayVariableDeclarator.Declaration.Type.TypeInfo != TypeInfo.String))
            { 
                builder.EmitLoadA(GenerateArrayOffsetAddress());
            }
            else
            {
                EmitLoadRowAddress();
                builder.EmitStoreA(Registers.RPeek);
                builder.EmitLoadA(Registers.RPeek.AsIndirectAddress());
            }
        }

        public void EmitLoadRowAddress()
        {
            var builder = Context.InstructionBuilder;

            // Element size
            int typeSize = ArrayVariableDeclarator!.Declaration.Type.TypeInfo.Size;

            // All indices are compile-time constants
            if (IsLiteralIntIndices())
            {
                // Load Array + offset address
                ArrayIdentifier!.EmitLoadAddress();
                builder.EmitAdd(CalculateLiteralOffset());
            }
            // 1D arrays OR strings (array of char)
            else if (ArrayVariableDeclarator!.ArraySpecifier?.Count == 1 ||
                     ArrayVariableDeclarator!.Declaration.Type == TypeInfo.String)
            {
                // Evaluate index -> offset
                Indices.First().Build();
                var offset = Context.TemporaryVariables.Create();
                builder.EmitStoreA(offset);

                // Scale offset by element size if elements occupy >1 word.
                if (typeSize > 1)
                {
                    builder.EmitLoadA(offset);
                    BuildMultiply(typeSize);
                    builder.EmitStoreA(offset);
                }

                // Final effective address: base + offset
                ArrayIdentifier!.EmitLoadAddress();
                builder.EmitAdd(offset);
            }
            // Multi-dimensional arrays
            else
            {
                var offset = Context.TemporaryVariables.Create();
                var stride = Context.TemporaryVariables.Create();

                builder.EmitMove(0, offset);  // offset = 0
                builder.EmitMove(1, stride);  // stride = 1

                for (int d = ArrayVariableDeclarator!.ArraySpecifier!.Count - 1; d >= 0; d--)
                {
                    // offset += indices[d] * stride;
                    Indices[d].Build();        // A <- indices[d]
                    BuildMultiply(stride);     // A <- A * stride
                    builder.EmitAdd(offset);   // A <- A + offset
                    builder.EmitStoreA(offset);// offset = A

                    // stride *= size[d];
                    builder.EmitLoadA(ArrayVariableDeclarator!.ArraySpecifier!.Sizes[d]); // A <- size[d]
                    BuildMultiply(stride);     // A <- A * stride
                    builder.EmitStoreA(stride);// stride = A
                }

                // Scale offset by element size if elements occupy >1 word.
                if (typeSize > 1)
                {
                    builder.EmitLoadA(offset);
                    BuildMultiply(typeSize);
                    builder.EmitStoreA(offset);
                }

                // Final effective address: base + offset
                ArrayIdentifier!.EmitLoadAddress();
                builder.EmitAdd(offset);
            }
        }

        public string GenerateArrayOffsetAddress()
        {
            int offset = CalculateLiteralOffset();
            return offset > 0 ? $"({arrayIdentifierInfo!.UniqueName}+{offset})" : $"{arrayIdentifierInfo!.UniqueName}";
        }

        public int CalculateLiteralOffset()
        {
            if (IsLiteralIntIndices())
            {
                int offset = 0;
                int stride = 1;

                if (ArrayVariableDeclarator!.Declaration.Type == TypeInfo.String && Indices.Count == 1)
                {
                    offset = ((LiteralInt)((LiteralExpression)Indices[0]).Literal).Value;
                }
                else
                {
                    for (int d = ArrayVariableDeclarator!.ArraySpecifier!.Count - 1; d >= 0; d--)
                    {
                        offset += ((LiteralInt)((LiteralExpression)Indices[d]).Literal).Value * stride;
                        stride *= ArrayVariableDeclarator!.ArraySpecifier!.Sizes[d];
                    }
                }
                return offset * ArrayVariableDeclarator!.Declaration.Type.TypeInfo.Size;
            }
            else
            {
                throw RaiseError($"Unable CalculateLiteralOffset() for '{ArrayIdentifier}' - indices are not literals");
            }
        }

        public bool IsLiteralIntIndices()
        {
            return Indices.All(i => i is LiteralExpression expression && expression.Literal is LiteralInt);
        }

        public override TypeInfo GetResultType()
        {
            // Element type of the indexed expression (e.g., int for int[], string for string[], char for char[])
            var elementType = Array.GetResultType();

            // Special case: indexing a plain string variable (not string[]) yields a single char
            if (elementType == TypeInfo.String &&
                Array is IdentifierExpression identifier &&
                CurrentScope.TryGetIdentifier(identifier.Identifier, out arrayIdentifierInfo) &&
                arrayIdentifierInfo.SourceNode is VariableDeclarator declarator &&
                !declarator.IsArray)
            {
                return TypeInfo.Char;
            }

            // Default: result type is the array's element type
            return elementType;
        }


        public override string ToString() => $"{Array}[{string.Join(", ", Indices.Select(e => e.ToString()))}]";

        private void BuildMultiply(ValueOrAddress rightOperand)
        {
            var builder = Context.InstructionBuilder;

            // Generate labels
            var loopStart = RandomGenerator.RandomStringLabel("loop_start");
            var loopEnd = RandomGenerator.RandomStringLabel("loop_end");

            // Process operation
            builder.EmitStoreA(Registers.R0);       // R0 = Left (result)
            builder.EmitStoreA(Registers.R1);       // R1 = Left (multiplicand)
            builder.EmitDecrement(rightOperand);
            builder.EmitStoreA(Registers.R2);       // R2 = Right-1 (multiplier)
            builder.EmitJumpIfZero(loopEnd);        // Exit if R2 = 0
            builder.SetLabel(loopStart);            // Loop start
            builder.EmitLoadA(Registers.R0);
            builder.EmitAdd(Registers.R1);
            builder.EmitStoreA(Registers.R0);       // R0 += R1
            builder.EmitDecrement(Registers.R2);
            builder.EmitStoreA(Registers.R2);       // Decrement R2
            builder.EmitJumpIfNotZero(loopStart);   // Jump if R2 > 0
            builder.SetLabel(loopEnd);              // End : store result in A
            builder.EmitLoadA(Registers.R0);
        }
    }
}
