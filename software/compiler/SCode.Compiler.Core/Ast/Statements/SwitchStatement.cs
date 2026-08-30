using SCode.Compiler.Ast.Expressions;
using SCode.Compiler.Ast.Literals;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Statements
{
    public class SwitchStatement : Statement, BreakStatement.IBreakableFlow
    {
        public string BreakLabel { get; } = RandomGenerator.RandomStringLabel("switch_exit");

        [ChildNode]
        public Expression Condition { get; set; }

        [ChildNode]
        public List<SwitchSection> Sections { get; set; }

        protected override void OnPrepare()
        {
            PrepareChildren();
            if (!TypeHelper.CanConvert(Condition.GetResultType(), TypeInfo.Int, true))
            {
                throw RaiseError("The expression evaluated in a 'switch' statement must be of integer type.");
            }
        }

        protected override void OnBuild()
        {
            var builder = Context.InstructionBuilder;

            // Evalute the condition and store result
            var evalVariable = Context.TemporaryVariables.Create();
            Condition.Build();
            builder.EmitStoreA(evalVariable);

            // Switch cases routing
            foreach (var section in Sections)
            {
                if (section.IsDefaultCase)
                {
                    // Default = always jump
                    builder.EmitJump(section.Label);
                }
                else
                {
                    // Evaluate expressions
                    foreach (var expression in section.Cases)
                    {
                        expression.Build();
                        builder.EmitSubtract(evalVariable);
                        builder.EmitJumpIfZero(section.Label);
                    }
                }
            }

            // If no case match, exit!
            builder.EmitJump(BreakLabel);

            // Generate body sections
            foreach (var section in Sections)
            {
                builder.SetLabel(section.Label);
                section.Body.Build();
            }

            // Exit label
            builder.SetLabel(BreakLabel);
        }

        public override string ToString() => $"Switch {Condition}";

        public class SwitchSection : Statement
        {
            public string Label { get; } = RandomGenerator.RandomStringLabel("switch_section");

            [ChildNode]
            public List<Expression> Cases { get; set; }
            
            [ChildNode]
            public Block Body { get; set; }

            public bool IsDefaultCase => Cases?.Count == 0;

            protected override void OnPrepare()
            {
                PrepareChildren();
                if (!IsDefaultCase && Cases.Any(@case => @case is not LiteralExpression literalExpression || literalExpression.Literal is not LiteralInt))
                {
                    throw RaiseError("Label must be an integer literal.");
                }
            }

            public override string ToString() => IsDefaultCase ? "Case Default" :
                Cases.Count == 1 ? $"Case {Cases.First()}" : $"Multi-Cases";
        }
    }
}
