using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    public class CastExpression : Expression
    {
        [ChildNode]
        public TypeDescriptor TargetedType { get; set; }

        [ChildNode]
        public Expression Expression { get; set; }

        protected override void OnPrepare()
        {
            PrepareChildren();
            if (!TypeHelper.CanConvert(Expression.GetResultType(), TargetedType, true))
            {
                throw RaiseError("Invalid type cast. The specified conversion is not allowed between these types.");
            }
        }

        protected override void OnBuild()
        {
            Expression.Build();
            if (TargetedType.TypeInfo == TypeInfo.Bool)
            {
                var labelFalse = RandomGenerator.RandomStringLabel("case_false");
                var labelExit = RandomGenerator.RandomStringLabel("exit");

                var builder = Context.InstructionBuilder;

                // If Expression = 0 ==> Cast to False
                builder.EmitJumpIfZero(labelFalse);
                builder.EmitLoadA(1);
                builder.EmitJump(labelExit);

                // Otherwise, if Expression != 0 ==> Cast to True
                builder.SetLabel(labelFalse);
                builder.EmitClearA();

                builder.SetLabel(labelExit);
            }
        }

        public override TypeInfo GetResultType()
        {
            return TargetedType;
        }

        public override string ToString() => $"Cast ({Expression}) as {TargetedType}";
    }
}
