using SCode.Compiler.Ast.Statements.VariableDeclaration;

namespace SCode.Compiler.Ast.Statements
{
    public class VariableDeclarationStatement : Statement
    {
        public bool IsConst { get; set; }
        public bool IsStatic { get; set; }
        public bool IsExtern { get; set; }

        [ChildNode]
        public TypeDescriptor Type { get; set; }

        [ChildNode]
        public List<VariableDeclarator> Variables { get; set; }

        public override string ToString()
        {
            return $"Declare{(IsExtern ? " Extern" : "")}{(IsStatic ? " Static" : "")} {(IsConst ? "Constant" : "Variable")} {Type} {string.Join(", ", Variables.Select(c => c.ToString()))}";
        }
    }
}