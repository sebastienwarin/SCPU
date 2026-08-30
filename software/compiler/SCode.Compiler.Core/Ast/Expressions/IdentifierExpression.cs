using SCode.Compiler.Ast.Statements;
using SCode.Compiler.Ast.Statements.VariableDeclaration;
using SCode.Compiler.Type;

namespace SCode.Compiler.Ast.Expressions
{
    public class IdentifierExpression : Expression
    {
        private IdentifierInfo _identifierInfo;

        [ChildNode]
        public Identifier Identifier { get; set; }

        protected override void OnPrepare()
        {
            if (!CurrentScope.TryGetIdentifier(Identifier, out _identifierInfo))
            {
                throw RaiseError($"Undefined identifier '{Identifier}'");
            }
        }

        protected override void OnBuild()
        {
            if (_identifierInfo.IsLocalVariableOrParameter(out var functionDeclaration))
            {
                functionDeclaration!.LoadIdentifierAddress(_identifierInfo, Registers.RPeek);
                Context.InstructionBuilder.EmitLoadA(Registers.RPeek.AsIndirectAddress());
            }
            else if (_identifierInfo.Type == IdentifierInfo.IdentifierType.Constant &&
                    _identifierInfo.SourceNode is AssemblyConstantStatement)
            {
                Context.InstructionBuilder.EmitLoadA(_identifierInfo.UniqueName.AsImmediateValue());
            }
            else
            {
                Context.InstructionBuilder.EmitLoadA(_identifierInfo.UniqueName);
            }
        }

        public void EmitLoadAddress(bool realStringAddress = false)
        {
            if (_identifierInfo.IsLocalVariableOrParameter(out var functionDeclaration))
            {
                functionDeclaration!.LoadIdentifierAddress(_identifierInfo);
            }
            else if (_identifierInfo.Type == IdentifierInfo.IdentifierType.Constant ||
                     _identifierInfo.Type == IdentifierInfo.IdentifierType.Variable)
            {
                if (_identifierInfo.DataType != null && _identifierInfo.DataType.TypeInfo == TypeInfo.String && !realStringAddress && 
                    (_identifierInfo.SourceNode is not VariableDeclarator variableDeclarator || !variableDeclarator.IsArray))
                {
                    Context.InstructionBuilder.EmitLoadA(_identifierInfo.UniqueName);
                }
                else
                {
                    Context.InstructionBuilder.EmitLoadA(_identifierInfo.UniqueName.AsImmediateValue());
                }
            }
            else
            {
                throw RaiseError($"Identifier '{Identifier}' cannot be addressed.");
            }
        }

        public override TypeInfo GetResultType()
        {
            CurrentScope.TryGetIdentifier(Identifier, out var thing);
            return thing?.DataType ?? TypeInfo.Empty;
        }

        public override string ToString() => Identifier.ToString();
    }
}
