namespace SCode.Compiler.Ast
{
    public abstract class Statement : Node
    {
        public string? Label { get; set; }

        public bool HasLabel => !string.IsNullOrEmpty(Label);

        public override sealed void Prepare()
        {
            base.Prepare();
            if (HasLabel && !Context.GlobalScope.RegisterIdentifier(Label!, IdentifierInfo.IdentifierType.Label, sourceNode: this))
            {
                throw RaiseError($"The label '{Label}' is already defined.");
            }
        }

        public override sealed void Build()
        {
            if (HasLabel && CurrentScope.TryGetIdentifier(Label!, out var identifierInfo))
            {
                Context.InstructionBuilder.SetLabel(identifierInfo.UniqueName);
            }

            using var scope = Context.TemporaryVariables.CreateScope();
            base.Build();
        }
    }
}
