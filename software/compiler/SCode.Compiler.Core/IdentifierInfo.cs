using SCode.Compiler.Ast;
using SCode.Compiler.Ast.Statements.VariableDeclaration;

namespace SCode.Compiler
{
    public class IdentifierInfo(string name, IdentifierInfo.IdentifierType type, TypeDescriptor? dataType = null, Node? sourceNode = null)
    {
        private string? _uniqueName;

        public string Name { get; set; } = name;
        public IdentifierType Type { get; set; } = type;
        public TypeDescriptor? DataType { get; set; } = dataType;
        public Node? SourceNode { get; set; } = sourceNode;

        public string UniqueName
        {
            get
            {
                if (_uniqueName == null)
                { 
                    _uniqueName = ComputeUniqueName();
                }

                return _uniqueName!;
            }
        }

        public enum IdentifierType
        {
            Function,
            Variable,
            Constant,
            Parameter,
            Label,
            Struct,
            Member
        }

        private string ComputeUniqueName()
        {
            if (Type == IdentifierType.Label)
            {
                return $"__label_{Name}";
            }
            else if (Type == IdentifierType.Variable && SourceNode is VariableDeclarator variableDeclarator && variableDeclarator.Declaration.IsStatic)
            {
                return $"__static_{Name}_{SourceNode.CurrentScope.UniqueId}";
            }
            else
            {
                return Name;
            }
        }
    }
}
