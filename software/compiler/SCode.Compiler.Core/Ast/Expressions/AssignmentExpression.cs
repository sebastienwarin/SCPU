using SCode.Compiler.Ast.Expressions.Unary;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    public class AssignmentExpression : Expression
    {
        private IdentifierInfo _identifierInfo;

        [ChildNode]
        public Expression Target { get; set; }

        [ChildNode]
        public Expression Value { get; set; }

        protected override void OnPrepare()
        {
            PrepareChildren();
            if (Target is not IdentifierExpression &&
                Target is not DereferenceExpression &&
                (Target is not ArrayAccessExpression arrayTarget || arrayTarget.Array is not IdentifierExpression) &&
                (Target is not MemberAccessExpression memberTarget || memberTarget.Expression is not IdentifierExpression))
            {
                throw RaiseError($"Incompatible assignment - cannot assign '{Value}' to '{Target}'.");
            }
            else if (Target is IdentifierExpression identifierExpression &&
                    CurrentScope.TryGetIdentifier(identifierExpression.Identifier, out _identifierInfo) &&
                    _identifierInfo.Type == IdentifierInfo.IdentifierType.Constant)
            {
                throw RaiseError($"The identifier '{identifierExpression.Identifier}' is a constant and cannot be modified.");
            }
            else if (_identifierInfo != null && _identifierInfo.DataType != null &&
                    (!_identifierInfo.DataType.CanAssignTo(Value.GetResultType()) &&
                    !(_identifierInfo.DataType.TypeInfo == TypeInfo.String && Value is AddressOfExpression)))
            {
                throw RaiseError($"Incompatible assignment type to '{Target}' - cannot assign '{Value.GetResultType()}' to '{Target.GetResultType()}'.");
            }
            else if (Target is DereferenceExpression && !Target.GetResultType().CanAssignTo(Value.GetResultType()))
            {
                throw RaiseError($"Incompatible assignment type to '{Target}' - cannot assign '{Value.GetResultType()}' to '{Target.GetResultType()}'.");
            }
        }

        protected override void OnBuild()
        {
            Value.Build();

            if (Target is IdentifierExpression)
            {
                if (_identifierInfo.IsLocalVariableOrParameter(out var functionDeclaration))
                {
                    var tempVar = Context.TemporaryVariables.Create();
                    Context.InstructionBuilder.EmitStoreA(tempVar);
                    functionDeclaration!.LoadIdentifierAddress(_identifierInfo, Registers.RPeek);
                    Context.InstructionBuilder.EmitMove(tempVar, Registers.RPeek.AsIndirectAddress());
                }
                else
                {
                    Context.InstructionBuilder.EmitStoreA(_identifierInfo.UniqueName);
                }
            }
            else if (Target is ArrayAccessExpression arrayAccessExpression)
            {
                // If the array is global/static, accessed with a literal index and not contains pointer or string, the element value can be stored directly
                if (arrayAccessExpression.ArrayVariableDeclarator != null &&
                    arrayAccessExpression.ArrayVariableDeclarator.IsGlobalOrStatic && arrayAccessExpression.IsLiteralIntIndices() &&
                    (!arrayAccessExpression.ArrayVariableDeclarator.Declaration.Type.IsPointer && arrayAccessExpression.ArrayVariableDeclarator.Declaration.Type.TypeInfo != TypeInfo.String))
                {
                    // Store A
                    Context.InstructionBuilder.EmitStoreA(arrayAccessExpression.GenerateArrayOffsetAddress());
                }
                else
                {
                    // Keep value on the stack
                    Context.InstructionBuilder.EmitPushA();

                    // Build target & keep value to temp. var.
                    arrayAccessExpression.EmitLoadRowAddress();
                    var arrayAddress = Context.TemporaryVariables.Create();
                    Context.InstructionBuilder.EmitStoreA(arrayAddress);

                    // Pop the value & store the array
                    Context.InstructionBuilder.EmitPop(arrayAddress.Address.AsIndirectAddress());
                }
            }
            else if (Target is DereferenceExpression dereferenceExpression)
            {
                var tempVar = Context.TemporaryVariables.Create();
                Context.InstructionBuilder.EmitStoreA(tempVar);
                dereferenceExpression.EmitStoreThrough(tempVar);
            }
            else if (Target is MemberAccessExpression)
            {
                throw new NotImplementedException("MemberAccessExpression not yet supported");
            }
            else
            {
                throw RaiseError("Invalid assignement !");
            }
        }

        public override TypeInfo GetResultType()
        {
            return Target.GetResultType();
        }

        public override string ToString() => $"Assign {Target} = {Value}";
    }
}
