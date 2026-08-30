namespace SCode.Compiler.Ast
{
    public class Identifier(string identifier) : Node
    {
        public string Name { get; set; } = identifier;

        public override string ToString() => Name;

        public static implicit operator Identifier(string name) => new(name);

        public static implicit operator string(Identifier identifier) =>  identifier.Name;
    }
}
