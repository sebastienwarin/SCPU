namespace SCode.Compiler.Ast.Statements
{
    [NestedScope(OnlyChild = true)]
    public class StructDeclarationStatement : Statement
    {
        [ChildNode]
        public Identifier Identifier { get; set; }

        [ChildNode]
        public List<Member> Members { get; set; }

        public int Size => CalculateSize(this);

        protected override void OnPrepare()
        {
            if (!CurrentScope.RegisterIdentifier(Identifier, IdentifierInfo.IdentifierType.Struct, ToTypeDescriptor(), this))
            {
                throw RaiseError($"The struct '{Identifier}' is already defined in the current scope.");
            }
            PrepareChildren();            
        }

        public int CalculateOffset(string memberName)
        {
            return CalculateSize(this, memberName);        
        }

        public TypeDescriptor ToTypeDescriptor()
        {
            return new TypeDescriptor { Name = Identifier.Name, Source = Source };
        }

        public override string ToString() => $"Struct {Identifier} {{ {string.Join(", ", Members.Select(m => m.ToString()))} }}";

        private static int CalculateSize(StructDeclarationStatement structDeclaration, string? breakOnMemberName = null)
        {
            int size = 0;
            foreach (var member in structDeclaration.Members)
            {
                if (breakOnMemberName != null && member.Identifier == breakOnMemberName)
                {
                    break;
                }
                else if (member.Type.IsBaseType)
                {
                    size += member.Type.TypeInfo.Size;
                }
                else if (structDeclaration.CurrentScope.TryGetIdentifier(member.Type.Name, out var identifierInfo) &&
                    identifierInfo.Type == IdentifierInfo.IdentifierType.Struct &&
                    identifierInfo.SourceNode is StructDeclarationStatement @struct)
                {
                    size += CalculateSize(@struct);
                }
                else
                {
                    throw new InvalidOperationException($"CalculateSize: invalid MemberDeclarations for {structDeclaration.Identifier}");
                }
            }
            return size;
        }

        public class Member : Node
        {
            [ChildNode]
            public TypeDescriptor Type { get; set; }

            [ChildNode]
            public Identifier Identifier { get; set; }

            protected override void OnPrepare()
            {
                if (!CurrentScope.RegisterIdentifier(Identifier, IdentifierInfo.IdentifierType.Member, Type, this))
                {
                    throw RaiseError($"The struct member '{Identifier}' is already defined in the current scope.");
                }
                PrepareChildren();
            }

            public override string ToString() => $"{Type} {Identifier}";
        }
    }
}
