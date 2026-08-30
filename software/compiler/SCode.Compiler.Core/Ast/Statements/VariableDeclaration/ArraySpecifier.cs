namespace SCode.Compiler.Ast.Statements.VariableDeclaration
{
    public class ArraySpecifier : Node
    {
        public int Count { get; set; }
        public int[] Sizes { get; set; }

        public int TotalSize => Sizes.Aggregate(1, (total, size) => total * size);

        public override string ToString() => $"[{string.Join(", ", Sizes)}]";
    }
}
