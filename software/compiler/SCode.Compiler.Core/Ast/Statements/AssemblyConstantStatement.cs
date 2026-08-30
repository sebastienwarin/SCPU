namespace SCode.Compiler.Ast.Statements
{
    public class AssemblyConstantStatement : Statement
    {
        [ChildNode]
        public Identifier Identifier { get; set; }

        [ChildNode]
        public TypeDescriptor Type { get; set; }

        public string Value { get; set; }

        protected override void OnPrepare()
        {
            if (!Context.GlobalScope.RegisterIdentifier(Identifier, IdentifierInfo.IdentifierType.Constant, Type, this))
            {
                throw RaiseError($"The assembly constant '{Identifier}' is already defined in the global scope.");
            }

            PrepareChildren();
        }

        protected override void OnBuild()
        {
            Context.InstructionBuilder.DeclareConstants(Identifier, Value);
        }

        public override string ToString() => $"Define assembly constant {Identifier} = {Value}";
    }
}
