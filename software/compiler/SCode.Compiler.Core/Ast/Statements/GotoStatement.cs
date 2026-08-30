namespace SCode.Compiler.Ast.Statements
{
    public class GotoStatement : Statement
    {
        [ChildNode]
        public Identifier Identifier { get; set; }

        protected override void OnBuild()
        {
            if (!CurrentScope.TryGetIdentifier(Identifier, out var identifierInfo))
            {
                throw RaiseError($"Label '{Identifier}' not found for 'goto' statement.");
            }

            Context.InstructionBuilder.EmitJump(identifierInfo.UniqueName);
        }

        public override string ToString() => $"Goto {Identifier}";
    }
}
