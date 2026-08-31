using SCode.Compiler.Ast.Enums;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions.Unary
{
    public class IncDecExpression : UnaryExpression
    {
        private IdentifierInfo _identifierInfo;

        public IncDecOperator Operator { get; set; }
        public Order Order { get; set; }

        protected override void OnPrepare()
        {
            PrepareChildren();
            if (Target is Expression expression && !TypeHelper.CanConvert(expression.GetResultType(), TypeInfo.Int))
            {
                throw RaiseError($"Increment (++) and decrement (--) operators require a type convertible to 'int'.");
            }
            else if (Target is IdentifierExpression identifierExpression &&
                    CurrentScope.TryGetIdentifier(identifierExpression.Identifier, out _identifierInfo) &&
                    _identifierInfo.Type == IdentifierInfo.IdentifierType.Constant)
            {
                throw RaiseError($"The identifier '{identifierExpression.Identifier}' is a constant and cannot be modified.");
            }
        }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;

            // Build target expression
            Target.Build();

            // If post operation inside an expression, save the initial value to R0
            bool isInExpression = this.Parent is Expression;
            if (isInExpression && Order == Order.Post)
            {
                builder.EmitStoreA(Registers.R0);
            }

            // Inc/dec operation
            if (Operator == IncDecOperator.Increment)
            {
                builder.EmitIncrementA();
            }
            else
            {
                builder.EmitDecrementA();
            }

            // Save value
            if (_identifierInfo.IsLocalVariableOrParameter(out var functionDeclaration))
            {
                var tempVar = Context.TemporaryVariables.Create();
                builder.EmitStoreA(tempVar);
                functionDeclaration!.LoadIdentifierAddress(_identifierInfo, Registers.RPeek);
                builder.EmitMove(tempVar, Registers.RPeek.AsIndirectAddress());
            }
            else if (Target is DereferenceExpression dereference)
            {
                var tempVar = Context.TemporaryVariables.Create();
                builder.EmitStoreA(tempVar);
                dereference.EmitStoreThrough(tempVar);
            }
            else
            {
                builder.EmitStoreA(_identifierInfo.UniqueName);
            }

            // If post operation inside an expression, restore the initial value from R0
            if (isInExpression && Order == Order.Post)
            {
                builder.EmitLoadA(Registers.R0);
            }
        }

        public override TypeInfo GetResultType()
        {
            return Target.GetResultType();
        }

        public override string ToString() => $"{Order} {Operator} {Target}";
    }
}
