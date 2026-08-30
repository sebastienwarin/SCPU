using SCode.Compiler.Ast.Statements.VariableDeclaration;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    public class SizeOfExpression : Expression
    {
        public int Size { get; set; }

        [ChildNode]
        public TypeDescriptor Type { get; set; }

        protected override void OnPrepare()
        {
            IdentifierInfo? identifier = null;
            if (!Type.IsBaseType && (!CurrentScope.TryGetIdentifier(Type.Name, out identifier) || identifier.DataType == null))
            {
                throw RaiseError($"Invalid argument to 'sizeof' - unknown type or identifier '{Type.Name}'.");
            }
            else if (identifier?.DataType != null)
            {
                if (identifier.SourceNode is VariableDeclarator variable)
                {
                    Size = variable.Size;
                }
                else
                {
                    Size = ((TypeInfo)identifier.DataType).Size;
                }
            }
            else
            {
                Size = ((TypeInfo)Type).Size;
            }
        }

        protected override void OnBuild()
        {
            Context.InstructionBuilder.EmitLoadA(Size);
        }

        public override TypeInfo GetResultType()
        {
            return TypeInfo.Int;
        }

        public override string ToString() => $"Sizeof({Type})";
    }
}
