using SCode.Compiler.Ast.Statements;
using SCode.Compiler.Ast.Statements.VariableDeclaration;
using SCode.Compiler.Instructions;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    public abstract class BinaryExpression<TOperator> : BinaryExpression where TOperator : Enum
    {
        public TOperator Operator { get; set; }

        public override string ToString() => $"({LeftOperand} {Operator} {RightOperand})";
    }

    public abstract class BinaryExpression : Expression
    {
        [ChildNode]
        public Expression LeftOperand { get; set; }

        [ChildNode]
        public Expression RightOperand { get; set; }

        protected abstract void BuildBinaryExpression(ValueOrAddress rightOperand);

        protected override void OnPrepare()
        {
            PrepareChildren();
            if (!TypeHelper.TryGetBinaryResultType(LeftOperand.GetResultType(), RightOperand.GetResultType(), out _))
            {
                throw RaiseError($"Incompatible types for the binary expression '{this.GetType().Name}'");
            }
        }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;

            // Determine how to obtain the right operand as a ValueOrAddress
            ValueOrAddress? rightOperand = null;

            if (RightOperand is LiteralExpression literalExpression && literalExpression.IsNumericValue)
            {
                // Right operand is a numeric literal : use its immediate value
                rightOperand = literalExpression.NumericValue;
            }
            else if (RightOperand is IdentifierExpression id &&
                    CurrentScope.TryGetIdentifier(id.Identifier, out var identifierInfo) &&
                    (identifierInfo.Type == IdentifierInfo.IdentifierType.Constant ||
                    (identifierInfo.SourceNode is VariableDeclarator variable && variable.IsGlobalOrStatic)))
            {
                if (identifierInfo.SourceNode is AssemblyConstantStatement)
                {
                    // Right operand is a compile-time assembly constant
                    rightOperand = identifierInfo.UniqueName.AsImmediateValue();
                }
                else
                {
                    // Right operand is a global or static variable accessible by its address
                    rightOperand = identifierInfo.UniqueName;
                }
            }
            else
            {
                // Right operand is a runtime expression : evaluate it first
                RightOperand.Build();

                // Push its result on the stack for later use (no direct ValueOrAddress yet)
                builder.EmitPushA();
            }

            // Evaluate the left operand (result goes into the accumulator A)
            LeftOperand.Build();

            // If the right operand was previously pushed on the stack...
            if (!rightOperand.HasValue)
            {
                // Preserve the left operand before restoring the right one
                var leftTemp = Context.TemporaryVariables.Create();
                builder.EmitStoreA(leftTemp);

                // Pop the computed right operand into a temporary variable
                rightOperand = Context.TemporaryVariables.Create();
                builder.EmitPop(rightOperand);

                // Restore the left operand to the accumulator (A)
                builder.EmitLoadA(leftTemp);
            }

            // Emit the actual binary operation (A = A op rightOperand).
            BuildBinaryExpression(rightOperand.Value);
        }

        public override TypeInfo GetResultType()
        {
            if (!TypeHelper.TryGetBinaryResultType(LeftOperand.GetResultType(), RightOperand.GetResultType(), out var resultType))
            {
                throw RaiseError($"Incompatible types: Cannot determine a valid result type for the binary expression of type '{this.GetType().Name}' between '{LeftOperand}' and '{RightOperand}'");
            }
            return resultType;
        }

        // True when the operation must use signed 16-bit integer semantics.
        // Integer literals are sign-neutral: an explicit signed int makes it signed, otherwise a uint/char keeps it unsigned.
        protected bool IsSignedIntegerOperation()
        {
            var left = LeftOperand.GetResultType();
            var right = RightOperand.GetResultType();
            if (!IsIntegerFamily(left) || !IsIntegerFamily(right))
            {
                return false;
            }

            bool leftSigned = left.TypeCode == SCodeType.Int && LeftOperand is not LiteralExpression;
            bool rightSigned = right.TypeCode == SCodeType.Int && RightOperand is not LiteralExpression;
            if (leftSigned || rightSigned)
            {
                return true;
            }

            bool anyUnsigned = left.TypeCode is SCodeType.UInt or SCodeType.Char
                            || right.TypeCode is SCodeType.UInt or SCodeType.Char;
            return !anyUnsigned;
        }

        private static bool IsIntegerFamily(TypeInfo type) => !type.IsPointer &&
            type.TypeCode is SCodeType.Int or SCodeType.UInt or SCodeType.Char;
    }
}
